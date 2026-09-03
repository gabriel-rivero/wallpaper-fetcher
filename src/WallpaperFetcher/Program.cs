// Entry point: --install/--uninstall manage the logon scheduled task; default/--run does one
// fetch-and-set pass (per-monitor wallpapers, accent color, optional Playnite background copy).
// [STAThread] is required: IDesktopWallpaper.GetMonitorRECT fails with E_FAIL from an MTA thread.
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

        var wallhaven = new WallhavenClient(http, config.WallhavenApiKey);
        var wallpaperService = new WallpaperService();

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
                    var imagePath = await FetchAndPrepareAsync(monitor.Width, monitor.Height, monitor.Index, config, wallhaven, http);
                    wallpaperService.SetWallpaper(monitor.DeviceId, imagePath);
                    Logger.Log($"Set wallpaper for monitor {monitor.Index} ({monitor.Width}x{monitor.Height}).");
                    primaryImagePath ??= imagePath;
                }
            }
            else
            {
                var primary = monitors[0];
                var imagePath = await FetchAndPrepareAsync(primary.Width, primary.Height, 0, config, wallhaven, http);
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
            Logger.Log($"Giving up: no usable connection to Wallhaven after retries ({ex.InnerException?.Message}).");
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
        int width, int height, int monitorIndex, AppConfig config, WallhavenClient wallhaven, HttpClient http)
    {
        var category = config.ResolveCategory();
        if (category == WallpaperCategory.Random)
            category = Random.Shared.Next(2) == 0 ? WallpaperCategory.Anime : WallpaperCategory.Games;

        ThemeMode? themeMode = config.MatchWallpaperToTheme ? ThemeDetector.GetCurrentMode() : null;
        if (themeMode is not null)
            Logger.Log($"Windows theme is {themeMode}; biasing wallpaper search toward a matching image.");

        return await RetryPolicy.RunWithBackoffAsync(
            async () =>
            {
                var result = await wallhaven.GetRandomWallpaperAsync(
                        category, width, height, themeMode,
                        config.DarkModeMaxBrightness, config.LightModeMinBrightness, CancellationToken.None)
                    ?? throw new HttpRequestException("No results returned from Wallhaven for this query.");

                var rawPath = Path.Combine(AppConfig.CacheDir, $"raw_{monitorIndex}{Path.GetExtension(result.ImageUrl)}");
                var bytes = await http.GetByteArrayAsync(result.ImageUrl);
                await File.WriteAllBytesAsync(rawPath, bytes);

                var finalPath = Path.Combine(AppConfig.CacheDir, $"monitor_{monitorIndex}.jpg");
                ImageProcessor.CropResizeToFill(rawPath, finalPath, width, height);
                File.Delete(rawPath);

                Logger.Log($"Fetched {category} wallpaper (id={result.Id}) for monitor {monitorIndex}.");
                return finalPath;
            },
            maxAttempts: config.MaxRetries,
            baseDelay: TimeSpan.FromSeconds(config.BaseDelaySeconds),
            onRetry: (attempt, ex, delay) => Logger.Log(
                $"Attempt {attempt}/{config.MaxRetries} failed ({ex.GetType().Name}: {ex.Message}). Retrying in {delay.TotalSeconds:0}s..."),
            ct: CancellationToken.None);
    }
}
