// Registers/unregisters a per-user run-at-logon entry (HKCU ...\CurrentVersion\Run).
// Task Scheduler's ONLOGON task creation is blocked by policy on some machines even for
// standard, non-elevated per-user tasks; the Run key only touches HKCU and always works.
// No artificial startup delay is needed: the app's own exponential backoff (RetryPolicy)
// already absorbs the case where the network isn't up yet right after logon.
using Microsoft.Win32;

namespace WallpaperFetcher;

public static class StartupManager
{
    private const string RunValueName = "WallpaperFetcher";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Install()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve the running executable's path.");

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException($@"Could not open/create HKCU\{RunKeyPath}.");

        key.SetValue(RunValueName, $"\"{exePath}\" --run", RegistryValueKind.String);
        Logger.Log($"Registered logon startup entry (HKCU\\{RunKeyPath}\\{RunValueName} -> \"{exePath}\" --run).");
    }

    public static void Uninstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(RunValueName, throwOnMissingValue: false);
        Logger.Log($"Removed logon startup entry (HKCU\\{RunKeyPath}\\{RunValueName}).");
    }
}
