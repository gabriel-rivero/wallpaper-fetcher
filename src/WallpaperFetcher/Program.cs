// Entry point: --install/--uninstall manage the logon startup entry; default/--run does one
// fetch-and-set pass (per-monitor wallpapers, accent color, optional Playnite background copy),
// acquiring the image from the first of several wallpaper providers that answers.
// [STAThread] is required: IDesktopWallpaper.GetMonitorRECT fails with E_FAIL from an MTA thread.
using WallpaperFetcher.Providers;

namespace WallpaperFetcher;

public static class Program
{
    [STAThread]
    public static int Main(string[] args) => RunAsync(args).GetAwaiter().GetResult();

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Contains("--install"))
        {
            StartupManager.Install();
            return 0;
        }

        if (args.Contains("--uninstall"))
        {
            StartupManager.Uninstall();
            return 0;
        }

        var config = AppConfig.LoadOrCreate();

        var categoryOverride = GetArgValue(args, "--category");
        if (!string.IsNullOrWhiteSpace(categoryOverride))
            config.Category = categoryOverride;

        Logger.Log("=== WallpaperFetcher run started ===");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WallpaperFetcher/1.0");

        var providerChain = new WallpaperProviderChain(http, config);
        var wallpaperService = new WallpaperService();

        Logger.Log($"Wallpaper providers (in order): {string.Join(", ", providerChain.ProviderNames)}" +
            (config.HedgeProviders ? " [hedged]" : ""));

        try
        {
            var monitors = wallpaperService.GetMonitors();
            if (monitors.Count == 0)
            {
                Logger.Log("No monitors detected via IDesktopWallpaper; aborting.");
                return 1;
            }

            Logger.Log($"Detected {monitors.Count} monitor(s): " +
                string.Join(", ", monitors.Select(m => $"[{m.Index}] {m.Width}x{m.Height}")));

            string? primaryImagePath;

            if (config.PerMonitorWallpaper && monitors.Count > 1)
            {
                primaryImagePath = null;
                foreach (var monitor in monitors)
                {
                    var imagePath = await FetchAndPrepareAsync(monitor.Width, monitor.Height, monitor.Index, config, providerChain, http);
                    wallpaperService.SetWallpaper(monitor.DeviceId, imagePath);
                    Logger.Log($"Set wallpaper for monitor {monitor.Index} ({monitor.Width}x{monitor.Height}).");
                    primaryImagePath ??= imagePath;
                }
            }
            else
            {
                var primary = monitors[0];
                var imagePath = await FetchAndPrepareAsync(primary.Width, primary.Height, 0, config, providerChain, http);
                wallpaperService.SetWallpaperForAll(imagePath);
                Logger.Log($"Set wallpaper for all {monitors.Count} monitor(s) using a {primary.Width}x{primary.Height} image.");
                primaryImagePath = imagePath;
            }

            if (config.AutoAccentColor)
                AccentColorManager.EnsureAutoAccentEnabled();

            if (primaryImagePath is not null)
                PlayniteIntegration.Apply(primaryImagePath, config.PlayniteBackgroundPath);

            Logger.Log("=== WallpaperFetcher run completed successfully ===");
            return 0;
        }
        catch (RetryExhaustedException ex)
        {
            Logger.Log($"Giving up: no wallpaper provider succeeded after retries ({ex.InnerException?.Message}).");
            return 2;
        }
        catch (Exception ex)
        {
            Logger.Log($"Unhandled error: {ex}");
            return 1;
        }
    }

    private static string? GetArgValue(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private static async Task<string> FetchAndPrepareAsync(
        int width, int height, int monitorIndex, AppConfig config, WallpaperProviderChain chain, HttpClient http)
    {
        var category = config.ResolveCategory();
        if (category == WallpaperCategory.Random)
            category = Random.Shared.Next(2) == 0 ? WallpaperCategory.Anime : WallpaperCategory.Games;

        ThemeMode? themeMode = config.MatchWallpaperToTheme ? ThemeDetector.GetCurrentMode() : null;
        if (themeMode is not null)
            Logger.Log($"Windows theme is {themeMode}; biasing wallpaper search toward a matching image.");

        var query = new WallpaperQuery(category, width, height, themeMode);
        var candidate = await chain.GetWallpaperAsync(
            query, config.DarkModeMaxBrightness, config.LightModeMinBrightness, http,
            config.MaxRetries, TimeSpan.FromSeconds(config.BaseDelaySeconds), CancellationToken.None);

        var rawPath = Path.Combine(AppConfig.CacheDir, $"raw_{monitorIndex}{Path.GetExtension(candidate.ImageUrl)}");
        var bytes = await RetryPolicy.RunWithBackoffAsync(
            () => http.GetByteArrayAsync(candidate.ImageUrl),
            maxAttempts: config.MaxRetries,
            baseDelay: TimeSpan.FromSeconds(config.BaseDelaySeconds),
            onRetry: (attempt, ex, delay) => Logger.Log(
                $"Image download attempt {attempt}/{config.MaxRetries} failed ({ex.GetType().Name}: {ex.Message}). Retrying in {delay.TotalSeconds:0}s..."),
            ct: CancellationToken.None);
        await File.WriteAllBytesAsync(rawPath, bytes);

        var finalPath = Path.Combine(AppConfig.CacheDir, $"monitor_{monitorIndex}.jpg");
        ImageProcessor.CropResizeToFill(rawPath, finalPath, width, height);
        File.Delete(rawPath);

        Logger.Log($"Fetched {category} wallpaper (id={candidate.Id}, source={candidate.ProviderName}) for monitor {monitorIndex}.");
        return finalPath;
    }
}
