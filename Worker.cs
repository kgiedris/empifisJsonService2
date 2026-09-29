using NLog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text;
using Newtonsoft.Json;
using empifisJsonAPI2.JsonObjects;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System;

namespace empifisJsonAPI2
{
    public class Worker : BackgroundService
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly EmpifisComManager _comManager;
        private readonly AppConfig _config;
        private readonly ReceiptProcessor _receiptProcessor;

        public Worker(EmpifisComManager comManager, IOptions<AppConfig> config, ReceiptProcessor receiptProcessor)
        {
            _comManager = comManager;
            _config = config.Value;
            _receiptProcessor = receiptProcessor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("Worker starting.");

            // Log the final configuration values
            _logger.Info("--- Final Configuration ---");
            _logger.Info($"Port: {_config.servicePort.port}");
            _logger.Info($"File Mode: {_config.servicePort.file_mode}");
            _logger.Info($"Radisson Error: {_config.servicePort.radison_error}");
            _logger.Info($"COM Timeout (seconds): {_config.servicePort.com_timeout_seconds}");
            _logger.Info($"Input File Path: {_config.JsonPathConfig.InFilePath}");
            _logger.Info($"Output File Path: {_config.JsonPathConfig.OutFilePath}");
            _logger.Info("---------------------------");

            if (_config.servicePort.file_mode?.ToLower() == "on")
            {
                _logger.Info("File processing mode is ON. Starting file monitor loop.");
                await FileMonitorLoop(stoppingToken);
            }
            else
            {
                _logger.Info("File processing mode is OFF. Worker is running in the background but will not process files.");
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(10000, stoppingToken);
                }
            }

            _logger.Info("Worker stopping.");
        }

        private async Task FileMonitorLoop(CancellationToken stoppingToken)
        {
            await QuarantineOrphanedProcessingFilesAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (Directory.Exists(_config.JsonPathConfig.InFilePath))
                    {
                        var files = Directory.GetFiles(_config.JsonPathConfig.InFilePath, "inReceipt*.json");

                        foreach (var filePath in files)
                        {
                            string processingPath = filePath + ".processing";
                            try
                            {
                                File.Move(filePath, processingPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn(ex, $"Could not lock file for processing, skipping: {filePath}");
                                continue;
                            }

                            _logger.Info($"Processing file: {filePath}");
                            await ProcessFileAsync(processingPath, filePath);
                        }
                    }
                    else
                    {
                        _logger.Warn($"Input directory not found: {_config.JsonPathConfig.InFilePath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "An error occurred in the file monitor loop.");
                }

                await Task.Delay(2000, stoppingToken);
            }
        }

        // Handles "*.json.processing" files left behind by a previous run that crashed or was killed
        // mid-processing. The receipt may already have been printed, so resending it could produce a
        // duplicate fiscal receipt. Instead the request is moved to an "unconfirmed" folder for a person
        // to check, and the POS gets a 557 response (unless a real response was already written).
        private async Task QuarantineOrphanedProcessingFilesAsync()
        {
            try
            {
                if (!Directory.Exists(_config.JsonPathConfig.InFilePath)) return;

                var orphans = Directory.GetFiles(_config.JsonPathConfig.InFilePath, "inReceipt*.json.processing");
                if (orphans.Length == 0) return;

                var unconfirmedDir = Path.Combine(_config.JsonPathConfig.InFilePath, "unconfirmed");
                Directory.CreateDirectory(unconfirmedDir);

                foreach (var processingPath in orphans)
                {
                    var originalPath = processingPath.Substring(0, processingPath.Length - ".processing".Length);
                    var quarantinePath = Path.Combine(unconfirmedDir,
                        $"{Path.GetFileNameWithoutExtension(originalPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    try
                    {
                        File.Move(processingPath, quarantinePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to quarantine interrupted receipt file: {processingPath}");
                        continue;
                    }

                    _logger.Error($"Receipt request '{Path.GetFileName(originalPath)}' was being processed when the service stopped. " +
                        $"It may or may not have been printed, so it was NOT resent. Moved to '{quarantinePath}' - check the fiscal device and resend manually if needed.");

                    // If the service died after writing the response but before deleting the request,
                    // the POS already has the real result; don't overwrite it.
                    if (File.Exists(GetResponseFilePath(originalPath)))
                    {
                        _logger.Info($"A response for '{Path.GetFileName(originalPath)}' already exists; leaving it in place.");
                        continue;
                    }

                    var response = new ResponseJson
                    {
                        ErrorCode = 557,
                        ErrorMessage = "Receipt processing was interrupted by a service restart. The receipt may or may not have been printed - check the fiscal device before resending."
                    };
                    string responseJsonString;
                    using (await _comManager.AcquireDeviceLockAsync())
                    {
                        responseJsonString = SerializeResponse(response);
                    }
                    await WriteResponseFile(originalPath, responseJsonString);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred while handling interrupted receipt files.");
            }
        }

        private async Task ProcessFileAsync(string filePath, string originalFilePath)
        {
            string jsonContent;
            try
            {
                jsonContent = await File.ReadAllTextAsync(filePath);
                _logger.Info($"Read JSON from file:\n{jsonContent}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to read file: {filePath}");

                // Restore the original name so it gets retried on a later pass instead of being orphaned.
                try
                {
                    File.Move(filePath, originalFilePath);
                }
                catch (Exception moveEx)
                {
                    _logger.Error(moveEx, $"Failed to restore file after read failure: {filePath}");
                }
                return;
            }

            // One device operation at a time; held through the Radison fiscal-info reads below.
            using var deviceLock = await _comManager.AcquireDeviceLockAsync();

            ResponseJson jsonResponse = new ResponseJson();
            try
            {
                var jsonReceipt = JsonInput.Deserialize<ReceiptJson>(jsonContent, Path.GetFileName(originalFilePath));
                if (jsonReceipt == null)
                {
                    jsonResponse.ErrorCode = 999;
                    jsonResponse.ErrorMessage = "Invalid JSON format.";
                }
                else
                {
                    // FIX: Capture the full tuple result (errorCode and message)
                    var result = _receiptProcessor.ProcessReceipt(jsonReceipt);
                    jsonResponse.ErrorCode = result.errorCode;
                    jsonResponse.ErrorMessage = result.message;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to process JSON from file: {filePath}");
                jsonResponse.ErrorCode = 999;
                jsonResponse.ErrorMessage = ex.Message;
            }

            string responseJsonString = SerializeResponse(jsonResponse);

            _logger.Info($"Response for file '{Path.GetFileName(originalFilePath)}':\n{responseJsonString}");
            await WriteResponseFile(originalFilePath, responseJsonString);

            // Delete the original file
            try
            {
                File.Delete(filePath);
                _logger.Info($"Original file deleted: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to delete original file: {filePath}");
            }
        }

        // Builds the response file content; in Radison mode this reads fiscal info from the device,
        // so the caller must hold the device lock.
        private string SerializeResponse(ResponseJson jsonResponse)
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            if (_config.servicePort.radison_error?.ToLower() != "on")
            {
                return JsonConvert.SerializeObject(jsonResponse, jsonSerializerSettings);
            }

            _logger.Info("Radison error mode is ON. Creating ResponseJsonRadison.");
            var jsonResponseRadison = new ResponseJsonRadison
            {
                ErrorCode = jsonResponse.ErrorCode,
                ErrorMessage = jsonResponse.ErrorMessage
            };

            // Get Fiscal Info for CashRegisterNo
            var fiscalInfoCashRegister = _comManager.GetFiscalInfo(3);
            jsonResponseRadison.CashRegisterNo = fiscalInfoCashRegister.message;

            // Get Fiscal Info for ReceiptNo
            var fiscalInfoReceiptNo = _comManager.GetFiscalInfo(2);
            if (int.TryParse(fiscalInfoReceiptNo.message, out int recNo))
            {
                jsonResponseRadison.ReceiptNo = (recNo - 1).ToString();
            }
            else
            {
                _logger.Warn($"Could not parse ReceiptNo from COM object: '{fiscalInfoReceiptNo.message}'");
                jsonResponseRadison.ReceiptNo = "N/A";
            }

            return JsonConvert.SerializeObject(jsonResponseRadison, jsonSerializerSettings);
        }

        // inReceipt123.json -> <OutFilePath>\outReceipt123.json
        private string GetResponseFilePath(string originalFilePath)
        {
            string originalFileName = Path.GetFileName(originalFilePath);
            string newFileName = originalFileName.StartsWith("in", StringComparison.OrdinalIgnoreCase)
                ? "out" + originalFileName.Substring(2)
                : originalFileName;
            return Path.Combine(_config.JsonPathConfig.OutFilePath, newFileName);
        }

        private async Task WriteResponseFile(string originalFilePath, string responseJsonString)
        {
            try
            {
                Directory.CreateDirectory(_config.JsonPathConfig.OutFilePath);
                DeleteUncollectedResponseFiles();

                string newFilePath = GetResponseFilePath(originalFilePath);

                // Write under a temporary name and rename, so the POS never reads a half-written file.
                string tempFilePath = newFilePath + ".tmp";
                await File.WriteAllTextAsync(tempFilePath, responseJsonString);
                File.Move(tempFilePath, newFilePath, overwrite: true);
                _logger.Info($"Response written to file: {newFilePath}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to write response file.");
            }
        }

        // Responses stay in the out folder until the POS collects them. Only files nobody has
        // picked up for a day are cleared, so the folder can't grow without limit.
        private void DeleteUncollectedResponseFiles()
        {
            var cutoff = DateTime.Now.AddDays(-1);
            foreach (var file in Directory.EnumerateFiles(_config.JsonPathConfig.OutFilePath))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                        _logger.Info($"Deleted uncollected response file older than a day: {file}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, $"Failed to delete old response file: {file}");
                }
            }
        }
    }
}