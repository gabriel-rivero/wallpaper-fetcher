// Playnite has no native "use OS wallpaper" feature; some fullscreen themes accept a static custom
// background image file instead. If the user points PlayniteBackgroundPath at that theme file, we
// overwrite it with the primary-monitor wallpaper. Purely best-effort and theme-dependent.
namespace WallpaperFetcher;

public static class PlayniteIntegration
{
    public static void Apply(string sourceImagePath, string? destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(sourceImagePath, destinationPath, overwrite: true);
            Logger.Log($"Copied wallpaper to Playnite background path: {destinationPath}");
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to apply Playnite background (non-fatal): {ex.Message}");
        }
    }
}
