using empifisJsonAPI2;
using empifisJsonAPI2.JsonObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using Newtonsoft.Json;
using NLog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// P/Invoke declarations for Windows API - Removed 'private' modifier
[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

// Constants - Removed 'private' modifier
const int SW_HIDE = 0;
const int SW_SHOW = 5;
const int SW_MINIMIZE = 6;
const int SW_RESTORE = 9;

// Use a unique name for the mutex
const string MutexName = "empifisJsonAPI2-singleton-mutex";

try
{
    using (var mutex = new Mutex(true, MutexName, out bool createdNew))
    {
        if (!createdNew)
        {
            Console.WriteLine("Another instance of the application is already running. Exiting.");
            NLog.LogManager.GetCurrentClassLogger().Error("Another instance of the application is already running. Exiting.");
            return;
        }

        // Create a CancellationTokenSource for graceful shutdown
        using (var cts = new CancellationTokenSource())
        {
            // Set up the application host
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddNLog();
            var logger = NLog.LogManager.GetCurrentClassLogger();

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

            builder.Services.Configure<AppConfig>(builder.Configuration);
            builder.Services.AddControllers();
            builder.Services.AddSingleton<EmpifisComManager>();
            builder.Services.AddSingleton<ReceiptProcessor>();
            builder.Services.AddHostedService<Worker>();
            builder.Services.AddWindowsService();

            var config = builder.Configuration.Get<AppConfig>();
            var port = config.servicePort.port;

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(int.Parse(port));
            });

            var app = builder.Build();

            app.MapFiscalEndpoints();

            // Start the application host in a background task
            _ = Task.Run(() => app.RunAsync(cts.Token));

            // Set up the system tray icon
            SetupTrayIcon(app, cts);

            // Wait for the application to be gracefully shut down
            cts.Token.WaitHandle.WaitOne();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"An unhandled exception occurred: {ex.Message}");
    NLog.LogManager.GetCurrentClassLogger().Fatal(ex, "An unhandled exception occurred during application startup.");
}


void SetupTrayIcon(WebApplication app, CancellationTokenSource cts)
{
    // Use an application context to ensure the tray icon runs correctly
    var applicationContext = new ApplicationContext();

    var notifyIcon = new NotifyIcon
    {
        Icon = new Icon(SystemIcons.Application, 40, 40),
        Visible = true,
        Text = "empifisJsonAPI2"
    };

    // Create a context menu for the tray icon
    var contextMenu = new ContextMenuStrip();
    var maximizeItem = contextMenu.Items.Add("Maximize");
    var minimizeItem = contextMenu.Items.Add("Minimize");
    var exitItem = contextMenu.Items.Add("Exit");

    // Event handlers for the menu items
    maximizeItem.Click += (sender, e) => ShowConsoleWindow();
    minimizeItem.Click += (sender, e) => HideConsoleWindow();
    exitItem.Click += (sender, e) => {
        app.Lifetime.StopApplication();
        notifyIcon.Visible = false;
        applicationContext.ExitThread();
        cts.Cancel();
    };

    // Event handler for double-click
    notifyIcon.DoubleClick += (sender, e) => ShowConsoleWindow();

    notifyIcon.ContextMenuStrip = contextMenu;

    // Hide the console window on startup
    HideConsoleWindow();

    // Run the message loop for the tray icon
    Application.Run(applicationContext);

    // Clean up
    notifyIcon.Dispose();
}

void ShowConsoleWindow()
{
    IntPtr handle = GetConsoleWindow();
    if (handle != IntPtr.Zero)
    {
        ShowWindow(handle, SW_RESTORE);
    }
}

void HideConsoleWindow()
{
    IntPtr handle = GetConsoleWindow();
    if (handle != IntPtr.Zero)
    {
        ShowWindow(handle, SW_HIDE);
    }
}