// App configuration: loaded from/saved to %LOCALAPPDATA%\WallpaperFetcher\config.json
using System.Text.Json;

namespace WallpaperFetcher;

public enum WallpaperCategory { Anime, Games, Random }

public sealed class AppConfig
{
    public string Category { get; set; } = "random"; // anime | games | random
    public bool PerMonitorWallpaper { get; set; } = true;
    public int MaxRetries { get; set; } = 5;
    public double BaseDelaySeconds { get; set; } = 5;
    public string? WallhavenApiKey { get; set; }
    public bool AutoAccentColor { get; set; } = true;
    public string? PlayniteBackgroundPath { get; set; }

    private static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallpaperFetcher");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");
    public static string CacheDir => Path.Combine(ConfigDir, "wallpapers");
    public static string LogDir => Path.Combine(ConfigDir, "logs");

    public static AppConfig LoadOrCreate()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(LogDir);

        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (loaded is not null)
                    return loaded;
            }
            catch
            {
                // corrupt config: fall through and regenerate defaults
            }
        }

        var config = new AppConfig();
        config.Save();
        return config;
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public WallpaperCategory ResolveCategory() => Category.Trim().ToLowerInvariant() switch
    {
        "anime" => WallpaperCategory.Anime,
        "games" or "game" => WallpaperCategory.Games,
        _ => WallpaperCategory.Random,
    };
}
