using empifisJsonAPI2;
using Microsoft.Extensions.Hosting.WindowsServices;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using empifisJsonAPI2.JsonObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddNLog();
var logger = NLog.LogManager.GetCurrentClassLogger();

// Set default configuration values
var defaultSettings = new Dictionary<string, string>
{
    ["servicePort:port"] = "5006",
    ["servicePort:file_mode"] = "on",
    ["servicePort:radison_error"] = "off",
    ["servicePort:com_timeout_seconds"] = "45",
    ["JsonPathConfig:InFilePath"] = "C:\\Altera\\json\\in\\",
    ["JsonPathConfig:OutFilePath"] = "C:\\Altera\\json\\out\\"
};
builder.Configuration.AddInMemoryCollection(defaultSettings);

// Overwrite with values from config.json if the file exists
var configFilePath = @"C:\Altera\EmpifisJsonAPI\config.json";
try
{
    if (File.Exists(configFilePath))
    {
        builder.Configuration.AddJsonFile(configFilePath, optional: false, reloadOnChange: true);
        logger.Info($"Configuration file found and loaded from '{configFilePath}'.");
    }
    else
    {
        logger.Warn($"Configuration file not found at '{configFilePath}'. Using default settings.");
    }
}
catch (Exception ex)
{
    logger.Fatal(ex, $"An error occurred while loading the configuration file from '{configFilePath}'. Application cannot start.");
    return;
}

// Bind configuration
builder.Services.Configure<AppConfig>(builder.Configuration);

// Add services for the API and worker
builder.Services.AddControllers();
builder.Services.AddSingleton<EmpifisComManager>();
builder.Services.AddSingleton<ReceiptProcessor>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService();

// Get the port from configuration
var config = builder.Configuration.Get<AppConfig>();
var port = config.servicePort.port;

// Configure Kestrel to listen on the specified port and only use HTTP.
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

var app = builder.Build();

app.MapFiscalEndpoints();

app.Run();