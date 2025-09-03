using NLog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using empifisJsonAPI2.JsonObjects;

namespace empifisJsonAPI2
{
    public class Worker : BackgroundService
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly EmpifisComManager _comManager;
        private readonly AppConfig _config;

        public Worker(EmpifisComManager comManager, IOptions<AppConfig> config)
        {
            _comManager = comManager;
            _config = config.Value;
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

            // Execute initial COM object call before starting main loop
            //_logger.Info("Initial COM object call on startup: PrintXReport.");
            //int initialResult = _comManager.PrintXReport();
            //_logger.Info($"Initial COM method returned: {initialResult}");

            // The worker now runs in the background while the ASP.NET Core server handles requests
            _logger.Info("Startup tasks completed. Worker service is now running in the background.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(10000, stoppingToken);
            }

            _logger.Info("Worker stopping.");
        }
    }
}