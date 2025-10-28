using empifisJsonAPI2;
using System.Threading;
using Microsoft.Extensions.Hosting.WindowsServices;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using empifisJsonAPI2.JsonObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
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
// Log application version on startup
try
{
    var assembly = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly();
    var version = assembly.GetName().Version?.ToString() ?? "unknown";
    var informational = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;
    var infoVersion = informational?.InformationalVersion ?? version;
    logger.Info($"Starting empifisJsonService2 version {infoVersion}");
}
catch (Exception ex)
{
    logger.Warn(ex, "Failed to read application version.");
}

// Single-instance guard: ensure only one instance of this process runs at a time.
// We keep the Mutex instance alive for the lifetime of the process to hold the lock.
Mutex? _singleInstanceMutex = null;
try
{
    // Use a reasonably unique name. Avoid Global\ prefix to prevent requiring extra privileges.
    var mutexName = "empifisJsonService2_single_instance";
    bool createdNew;
    _singleInstanceMutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out createdNew);
    if (!createdNew)
    {
        logger.Error("Another instance of empifisJsonService2 is already running. Exiting.");
        // Allow logger flush for NLog
        NLog.LogManager.Flush(TimeSpan.FromSeconds(2));
        // Exit the process immediately with a non-zero code.
        Environment.Exit(1);
    }
}
catch (Exception ex)
{
    // If the mutex creation fails for some reason, log and continue starting —
    // this is a best-effort single-instance guard.
    logger.Warn(ex, "Failed to create single-instance mutex. Continuing startup.");
}

// Set default configuration values
var defaultSettings = new Dictionary<string, string?>
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

// Ensure JSON responses preserve C# PascalCase property names (ErrorCode, ErrorMessage)
// by disabling the default camel-casing policy for both controllers and minimal API responses.
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.PropertyNamingPolicy = null;
}).AddNewtonsoftJson(opts =>
{
    opts.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    opts.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = null;
});


// Get the port from configuration
var config = builder.Configuration.Get<AppConfig>();
if (config?.servicePort?.port == null)
{
    logger.Fatal("servicePort or port is not configured properly. Application cannot start.");
    return;
}
var port = config.servicePort.port;

// Configure Kestrel to listen on the specified port and only use HTTP.
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

var app = builder.Build();

// Use custom JSON response middleware to normalize \uXXXX escaping
app.UseMiddleware<CustomJsonResponseMiddleware>();



// Middleware: normalize request paths by collapsing multiple consecutive slashes into a single slash.
// Use the raw request target when available so we can detect duplicates that were normalized
// by lower-level listeners before Request.Path was populated.
app.Use(async (context, next) =>
{
    // Capture raw request target (may include query string) if available
    var reqFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestFeature>();
    var rawTarget = reqFeature?.RawTarget ?? context.Request.Path.Value ?? string.Empty;

    // Store both the raw target and the initial Request.Path for diagnostics
    context.Items["originalRawTarget"] = rawTarget;
    context.Items["originalPath"] = context.Request.Path.Value;

    // Extract path part (strip query string) so we can normalize only the path
    var pathPart = rawTarget;
    var qIdx = rawTarget.IndexOf('?');
    if (qIdx >= 0)
    {
        pathPart = rawTarget.Substring(0, qIdx);
    }

    if (!string.IsNullOrEmpty(pathPart) && pathPart.Contains("//"))
    {
        var newPath = System.Text.RegularExpressions.Regex.Replace(pathPart, "/{2,}", "/");
        logger.Info($"Normalized request path from raw '{pathPart}' to '{newPath}'");
        context.Request.Path = new Microsoft.AspNetCore.Http.PathString(newPath);
    }

    await next();
});

app.MapFiscalEndpoints();

// Diagnostic endpoint: test PrintX, Unload COM, verify error, Load COM, PrintX again
app.MapGet("/diag/test-printx-unload-reload", (EmpifisComManager comManager) =>
{
    var results = new Dictionary<string, object?>();

    logger.Info("Starting diagnostic: PrintX -> Unload -> PrintX -> Load -> PrintX");

    // 1) First PrintX
    int first = comManager.PrintXReport();
    results["firstPrintX"] = first;

    // 2) Unload COM
    comManager.Unload();
    results["afterUnload_isLoaded"] = comManager.IsLoaded();

    // 3) Attempt PrintX after unload (should return 999 or similar error)
    int second = comManager.PrintXReport();
    results["secondPrintX_afterUnload"] = second;

    // 4) Load COM explicitly
    bool loaded = comManager.Load();
    results["afterLoad_isLoaded"] = loaded;

    // 5) PrintX after load
    int third = comManager.PrintXReport();
    results["thirdPrintX_afterLoad"] = third;

    logger.Info($"Diagnostic completed. Results: first={first}, second={second}, third={third}, loaded={loaded}");

    return Results.Json(results);
});

// Diagnostic path echo endpoint
app.MapGet("/diag/echo-path", (HttpContext context) =>
{
    var original = context.Items.ContainsKey("originalPath") ? context.Items["originalPath"]?.ToString() : null;
    var normalized = context.Request.Path.Value;
    var full = context.Request.Scheme + "://" + context.Request.Host + context.Request.Path + context.Request.QueryString;
    return Results.Json(new { originalPath = original, normalizedPath = normalized, fullRequest = full });
});

// Fallback diagnostic GET: expose raw target for requests that do not match other routes.
// This helps detect whether duplicate slashes (e.g. //fiscalCommand) arrive at Kestrel
// or are normalized/removed earlier by the client/proxy.
app.MapGet("/{**catchall}", (HttpContext context) =>
{
    var reqFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestFeature>();
    var rawTarget = reqFeature?.RawTarget ?? context.Request.Path.Value ?? string.Empty;
    var original = context.Items.ContainsKey("originalPath") ? context.Items["originalPath"]?.ToString() : null;
    var normalized = context.Request.Path.Value;
    var full = context.Request.Scheme + "://" + context.Request.Host + context.Request.Path + context.Request.QueryString;
    return Results.Json(new { originalRawTarget = rawTarget, originalPath = original, normalizedPath = normalized, fullRequest = full });
});

// If running interactively (console) create a NotifyIcon and hide the console window so
// the app starts minimized to the system tray. For services or non-interactive runs we skip this.
if (Environment.UserInteractive)
{
    try
    {
        var hWnd = NativeMethods.GetConsoleWindow();

        // Create NotifyIcon
        var notifyIcon = new NotifyIcon();

        // Try to use the application's associated icon, fallback to system information icon
        try
        {
            var asmPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(asmPath))
            {
                notifyIcon.Icon = Icon.ExtractAssociatedIcon(asmPath);
            }
        }
        catch { /* ignore, use default icon */ }

        if (notifyIcon.Icon == null)
        {
            notifyIcon.Icon = SystemIcons.Application;
        }

        notifyIcon.Text = "empifisJsonService2";

        // Context menu: Show Console / Exit
        var menu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Show Console");
        showItem.Click += (s, e) =>
        {
            if (hWnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }
        };
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) =>
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            NLog.LogManager.Flush(TimeSpan.FromSeconds(2));
            Application.Exit();
        };

        menu.Items.Add(showItem);
        menu.Items.Add(exitItem);
        notifyIcon.ContextMenuStrip = menu;

        notifyIcon.DoubleClick += (s, e) =>
        {
            if (hWnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }
        };

        notifyIcon.Visible = true;

        // Hide console window
        if (hWnd != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
        }

        // Ensure icon is disposed on exit
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            try { notifyIcon.Visible = false; notifyIcon.Dispose(); }
            catch { }
        };

        // Keep a reference so GC won't collect the NotifyIcon while running
        TrayIconHolder.Icon = notifyIcon;

        // Start the Kestrel app in a background thread so we can run the Windows message pump
        var appTask = Task.Run(() => app.Run());

        // Run Windows Forms message loop to handle tray icon events (right-click, etc.)
        Application.Run();
    }
    catch (Exception ex)
    {
        logger.Warn(ex, "Failed to initialize system tray icon.");
        app.Run();
    }
}
else
{
    app.Run();
}