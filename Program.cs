using empifisJsonAPI2;
using System.Threading;
using Microsoft.Extensions.Hosting.WindowsServices;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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

// Allocate console at startup to capture all logging, but keep it hidden
ConsoleHelper.AllocateHiddenConsole();

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddNLog();
var logger = NLog.LogManager.GetCurrentClassLogger();

// Get application version for logging and UI
string appVersion = "unknown";
try
{
    var assembly = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly();
    var version = assembly.GetName().Version?.ToString() ?? "unknown";
    var informational = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;
    var fullVersion = informational?.InformationalVersion ?? version;
    // Strip metadata suffix (e.g., "2.0.2+e123456" -> "2.0.2")
    appVersion = fullVersion.Split('+')[0];
    logger.Info($"Starting empifisJsonService2 version {appVersion}");
}
catch (Exception ex)
{
    logger.Warn(ex, "Failed to read application version.");
}
ConsoleHelper.AppVersion = appVersion;

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
    ["JsonPathConfig:OutFilePath"] = "C:\\Altera\\json\\out\\",
    // Empty = no cross-origin browser access by default; set to "*" or a comma-separated
    // list of origins in config.json to allow specific web clients.
    ["Cors:AllowedOrigins"] = ""
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
builder.Services.AddSingleton<EmpifisComManager>();
builder.Services.AddSingleton<IFiscalDevice>(sp => sp.GetRequiredService<EmpifisComManager>());
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

// Record start, stop and crashes in json2.log (the framework's own lifetime messages are filtered out
// by nlog.config), so it's always possible to tell when and how the service stopped.
string runMode = Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService() ? "Windows service" : "interactive (tray)";
app.Lifetime.ApplicationStarted.Register(() => logger.Info($"empifisJsonService2 {appVersion} started as {runMode}, listening on port {port}."));
app.Lifetime.ApplicationStopping.Register(() => logger.Info($"empifisJsonService2 is stopping (stop requested: {(runMode == "Windows service" ? "Windows service stop" : "tray Exit or console close")})."));
app.Lifetime.ApplicationStopped.Register(() =>
{
    logger.Info("empifisJsonService2 stopped.");
    NLog.LogManager.Flush(TimeSpan.FromSeconds(2));
});
AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception; the process is terminating.");
    NLog.LogManager.Flush(TimeSpan.FromSeconds(2));
};

// Use custom JSON response middleware to normalize \uXXXX escaping
app.UseMiddleware<CustomJsonResponseMiddleware>();

// Add CORS middleware to handle preflight OPTIONS requests and add CORS headers.
// Only requests carrying a browser "Origin" header are affected; server-to-server
// callers (POS integrations, curl, etc.) don't send one and pass through untouched.
// The configured origin list is resolved once via IOptionsMonitor (cheap, live-reload
// aware) instead of re-binding the whole AppConfig via reflection on every request.
var corsOptionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<AppConfig>>();
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();
    if (!string.IsNullOrEmpty(origin))
    {
        var allowedOrigins = corsOptionsMonitor.CurrentValue.Cors?.AllowedOrigins ?? string.Empty;
        var isWildcard = allowedOrigins.Trim() == "*";
        var isAllowed = isWildcard || allowedOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(o => o.Trim())
            .Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));

        if (isAllowed)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = isWildcard ? "*" : origin;
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            context.Response.Headers["Access-Control-Max-Age"] = "3600";
        }
    }

    // Handle preflight OPTIONS requests
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        await context.Response.CompleteAsync();
        return;
    }

    await next();
});


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

// Diagnostic endpoint: test PrintX, Unload COM, verify error, Load COM, PrintX again.
// POST only: it prints three X reports, so a browser visit or link preview must not trigger it.
app.MapPost("/diag/test-printx-unload-reload", async (EmpifisComManager comManager) =>
{
    using var deviceLock = await comManager.AcquireDeviceLockAsync();
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

        // Try to load custom icon from PNG file, then fallback to ICO or system icon
        Icon? customIcon = null;
        try
        {
            // Try to load PNG logo and convert to icon
            var pngPath = Path.Combine(AppContext.BaseDirectory, "empirija-logo.png");
            if (File.Exists(pngPath))
            {
                using (var bmp = new Bitmap(pngPath))
                using (var resized = new Bitmap(bmp, new Size(16, 16)))
                {
                    // Icon.FromHandle wraps the HICON without taking ownership of it, so the
                    // handle must be freed explicitly (Icon.Dispose() won't do it). Clone into
                    // a self-owned Icon first, then destroy the raw handle.
                    var hIcon = resized.GetHicon();
                    try
                    {
                        using (var tempIcon = Icon.FromHandle(hIcon))
                        {
                            customIcon = (Icon)tempIcon.Clone();
                        }
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(hIcon);
                    }
                }
            }
            else
            {
                // Fallback to .ico file
                var iconPath = Path.Combine(AppContext.BaseDirectory, "empirija.ico");
                if (File.Exists(iconPath))
                {
                    customIcon = new Icon(iconPath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to load custom icon: {ex.Message}");
        }

        // Fallback to application icon or system icon
        if (customIcon != null)
        {
            notifyIcon.Icon = customIcon;
        }
        else
        {
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
        }

        notifyIcon.Text = $"empifisJsonService2 v{appVersion}";
        notifyIcon.Visible = true; // Ensure icon is always visible in main system tray

        // Context menu: Show Console / Hide Console / Exit
        var menu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Show Console");
        showItem.Click += (s, e) =>
        {
            ConsoleHelper.ShowConsole();
        };
        var hideItem = new ToolStripMenuItem("Hide Console");
        hideItem.Click += (s, e) =>
        {
            ConsoleHelper.HideConsole();
        };
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += async (s, e) =>
        {
            exitItem.Enabled = false;
            // Stop the host first so an in-flight receipt (HTTP request or file) finishes
            // instead of being cut off halfway through printing.
            try
            {
                logger.Info("Exit requested from tray. Stopping the service...");
                await app.StopAsync();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Error while stopping the host on exit.");
            }
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            NLog.LogManager.Flush(TimeSpan.FromSeconds(2));
            Application.Exit();
        };

        menu.Items.Add(showItem);
        menu.Items.Add(hideItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        notifyIcon.ContextMenuStrip = menu;

        notifyIcon.DoubleClick += (s, e) =>
        {
            ConsoleHelper.ShowConsole();
        };

        notifyIcon.Visible = true;

        // Application runs as Windows application (WinExe) - no console window by default
        // Use tray icon menu to show/hide console on demand

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

        // If Kestrel fails to start (e.g. port already in use) or crashes later, the tray icon
        // would otherwise keep running silently with no HTTP service and no visible indication
        // anything is wrong. Surface it and shut the app down instead.
        _ = appTask.ContinueWith(t =>
        {
            logger.Fatal(t.Exception, "Kestrel host terminated unexpectedly. Shutting down.");
            NLog.LogManager.Flush(TimeSpan.FromSeconds(2));
            try { Application.Exit(); } catch { }
        }, TaskContinuationOptions.OnlyOnFaulted);

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