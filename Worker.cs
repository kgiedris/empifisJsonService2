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
                // The worker still runs, so the application won't stop.
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(10000, stoppingToken);
                }
            }

            _logger.Info("Worker stopping.");
        }

        private async Task FileMonitorLoop(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (Directory.Exists(_config.JsonPathConfig.InFilePath))
                    {
                        var files = Directory.EnumerateFiles(_config.JsonPathConfig.InFilePath, "inReceipt*.json");

                        foreach (var filePath in files)
                        {
                            _logger.Info($"Processing file: {filePath}");
                            await ProcessFileAsync(filePath);
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

                // Wait for 10 seconds before checking again
                await Task.Delay(10000, stoppingToken);
            }
        }

        private async Task ProcessFileAsync(string filePath)
        {
            string jsonContent;
            try
            {
                // Read the JSON content from the file
                jsonContent = await File.ReadAllTextAsync(filePath);
                _logger.Info($"Read JSON from file:\n{jsonContent}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to read file: {filePath}");
                return;
            }

            ResponseJson jsonResponse = new ResponseJson();
            try
            {
                var jsonReceipt = JsonConvert.DeserializeObject<ReceiptJson>(jsonContent);
                if (jsonReceipt == null)
                {
                    jsonResponse.ErrorCode = 999;
                    jsonResponse.ErrorMessage = "Invalid JSON format.";
                }
                else
                {
                    jsonResponse.ErrorCode = _receiptProcessor.ProcessReceipt(jsonReceipt);
                    jsonResponse.ErrorMessage = jsonResponse.ErrorCode == 0 ? "Success" : "Error";
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to process JSON from file: {filePath}");
                jsonResponse.ErrorCode = 999;
                jsonResponse.ErrorMessage = ex.Message;
            }

            // Log the entire outgoing JSON response before writing to file
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };
            string responseJsonString = JsonConvert.SerializeObject(jsonResponse, jsonSerializerSettings);
            _logger.Info($"Response for file '{Path.GetFileName(filePath)}':\n{responseJsonString}");

            await WriteResponseFile(filePath, responseJsonString);

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

        private async Task WriteResponseFile(string originalFilePath, string responseJson)
        {
            try
            {
                // Clear the output directory first
                if (Directory.Exists(_config.JsonPathConfig.OutFilePath))
                {
                    var existingFiles = Directory.EnumerateFiles(_config.JsonPathConfig.OutFilePath);
                    foreach (var file in existingFiles)
                    {
                        File.Delete(file);
                    }
                }
                else
                {
                    Directory.CreateDirectory(_config.JsonPathConfig.OutFilePath);
                }

                string originalFileName = Path.GetFileName(originalFilePath);
                string newFileName = originalFileName.Replace("inReceipt", "outReceipt");
                string newFilePath = Path.Combine(_config.JsonPathConfig.OutFilePath, newFileName);

                await File.WriteAllTextAsync(newFilePath, responseJson);
                _logger.Info($"Response written to file: {newFilePath}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to write response file.");
            }
        }
    }
}