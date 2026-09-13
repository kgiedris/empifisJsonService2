using System;
using System.Runtime.InteropServices;
using System.IO;

/// <summary>
/// Helper class for managing console window visibility.
/// The application runs as a Windows application (no console by default).
/// Console can be allocated/freed on demand for debugging.
/// </summary>
public static class ConsoleHelper
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private static bool _consoleRedirected = false;
        private static bool _consoleAllocated = false;

        /// <summary>
        /// Application version, set once by Program.cs at startup, used for the console header.
        /// </summary>
        public static string AppVersion { get; set; } = "unknown";

        /// <summary>
        /// Allocates a console at startup and immediately hides it.
        /// This allows logging output to be captured from the beginning.
        /// </summary>
        public static void AllocateHiddenConsole()
        {
            if (_consoleAllocated) return;

            try
            {
                // Allocate console
                AllocConsole();
                _consoleAllocated = true;

                // Redirect standard output and error to the console
                var outStream = Console.OpenStandardOutput();
                var errStream = Console.OpenStandardError();
                var inStream = Console.OpenStandardInput();
                
                Console.SetOut(new StreamWriter(outStream) { AutoFlush = true });
                Console.SetError(new StreamWriter(errStream) { AutoFlush = true });
                Console.SetIn(new StreamReader(inStream));
                
                _consoleRedirected = true;

                // Hide the console window immediately
                var hWnd = GetConsoleWindow();
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_HIDE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to allocate hidden console: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the console window. Allocates a console if one doesn't exist.
        /// </summary>
        public static void ShowConsole()
        {
            var hWnd = GetConsoleWindow();
            if (hWnd == IntPtr.Zero && !_consoleAllocated)
            {
                // No console exists, allocate one with redirection
                AllocateHiddenConsole();
                hWnd = GetConsoleWindow();
            }
            
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                ShowWindow(hWnd, SW_SHOW);
                
                // Write a header when first shown (only if not written before)
                if (_consoleAllocated && !_headerWritten)
                {
                    Console.WriteLine("\n=== Console Window Opened ===");
                    Console.WriteLine($"Application: empifisJsonService2 v{AppVersion}");
                    Console.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine("Logging output appears below:");
                    Console.WriteLine("================================\n");
                    _headerWritten = true;
                }
            }
        }

        private static bool _headerWritten = false;

        /// <summary>
        /// Hides the console window.
        /// </summary>
        public static void HideConsole()
        {
            var hWnd = GetConsoleWindow();
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_HIDE);
            }
        }
    }