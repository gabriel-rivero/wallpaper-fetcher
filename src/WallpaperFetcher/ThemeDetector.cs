// Reads Windows' own light/dark mode setting so wallpaper fetches can be biased to match it.
using Microsoft.Win32;

namespace WallpaperFetcher;

public enum ThemeMode { Light, Dark }

public static class ThemeDetector
{
    public static ThemeMode GetCurrentMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            // SystemUsesLightTheme (taskbar/start/system chrome) rather than AppsUseLightTheme:
            // closer to the overall desktop mood a wallpaper sits behind than per-app chrome is.
            if (key?.GetValue("SystemUsesLightTheme") is int value)
                return value == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            // fall through to the default below
        }

        return ThemeMode.Light;
    }
}
