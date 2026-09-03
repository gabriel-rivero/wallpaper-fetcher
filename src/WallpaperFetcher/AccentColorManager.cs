// Flips on Windows' built-in "Automatically pick an accent color from my background" setting.
// Windows itself recomputes the accent color whenever the wallpaper changes; we don't compute colors ourselves.
using Microsoft.Win32;

namespace WallpaperFetcher;

public static class AccentColorManager
{
    public static void EnsureAutoAccentEnabled()
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

        var current = key.GetValue("AutoColorization");
        if (current is int i && i == 1)
            return;

        key.SetValue("AutoColorization", 1, RegistryValueKind.DWord);
        Logger.Log("Enabled 'automatically pick accent color from background' (HKCU Themes\\Personalize\\AutoColorization).");
    }
}
