using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Small helper classes for tray behavior and Win32 console show/hide
internal static class NativeMethods
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;
}

internal static class TrayIconHolder
{
    public static NotifyIcon? Icon;
}
