// Dual console + rolling file logger; scheduled-task runs have no console so the file is the source of truth.
namespace WallpaperFetcher;

public static class Logger
{
    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        try { Console.WriteLine(line); } catch { /* no console attached */ }
        try
        {
            Directory.CreateDirectory(AppConfig.LogDir);
            File.AppendAllText(Path.Combine(AppConfig.LogDir, "wallpaperfetcher.log"), line + Environment.NewLine);
        }
        catch { /* best-effort logging */ }
    }
}
