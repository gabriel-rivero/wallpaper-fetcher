# WallpaperFetcher

Fetches a random anime/video-game wallpaper from [Wallhaven](https://wallhaven.cc), crops/resizes it
to exactly match each connected display's resolution, and sets it as the desktop wallpaper. Runs once
at logon.

## Build & publish

```
dotnet publish src/WallpaperFetcher -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Produces `publish/WallpaperFetcher.exe` — a single self-contained exe, no .NET runtime required on
the target machine.

## Usage

- `WallpaperFetcher.exe --run` — fetch + set wallpaper(s) once (this is what runs at logon).
- `WallpaperFetcher.exe` (no args) — same as `--run`.
- `WallpaperFetcher.exe --category anime|games|random` — one-off override, not persisted.
- `WallpaperFetcher.exe --install` — register a per-user logon startup entry pointing at *this exe's
  current path*. Re-run `--install` if you move/republish the exe elsewhere.
- `WallpaperFetcher.exe --uninstall` — remove the startup entry.

## Config

`%LOCALAPPDATA%\WallpaperFetcher\config.json`, created with defaults on first run:

```jsonc
{
  "Category": "random",           // "anime" | "games" | "random" (random alternates per monitor)
  "PerMonitorWallpaper": true,    // distinct wallpaper per display vs. one image spanned to all
  "MaxRetries": 5,                // attempts before giving up on a no-internet/API-down run
  "BaseDelaySeconds": 5,          // exponential backoff base (5s, 10s, 20s, 40s, ...)
  "WallhavenApiKey": null,        // optional; raises Wallhaven's anonymous rate limit
  "AutoAccentColor": true,        // toggle Windows' "pick accent color from background"
  "PlayniteBackgroundPath": null, // optional, see Playnite section below
  "MatchWallpaperToTheme": true,  // bias fetches to match current Windows light/dark mode
  "DarkModeMaxBrightness": 95,    // 0-255 average luminance; at/below this counts as "dark"
  "LightModeMinBrightness": 150   // 0-255 average luminance; at/above this counts as "light"
}
```

Logs: `%LOCALAPPDATA%\WallpaperFetcher\logs\wallpaperfetcher.log` (there's no console window when
launched at logon, so this file is the only record of what happened).

## How it works

- **Wallpaper source**: Wallhaven's public search API, keyless by default (45 req/min anonymous
  limit, plenty for this use case). `"anime"` uses Wallhaven's dedicated anime category; `"games"`
  has no dedicated category on Wallhaven, so it searches the general category against a rotating
  list of well-known game titles/franchises. SFW purity only.
- **Multi-monitor**: uses the shell's `IDesktopWallpaper` COM interface to enumerate monitors and set
  a wallpaper per monitor ID, each fetched/cropped to that monitor's exact native resolution. Set
  `PerMonitorWallpaper: false` to instead fetch one image sized to the first monitor and apply it to
  all displays.
- **Light/dark theme matching**: reads Windows' own theme setting
  (`HKCU\...\Themes\Personalize\SystemUsesLightTheme`) and, when enabled, biases the Wallhaven query
  toward matching dominant colors and checks up to 6 candidates' thumbnails for actual average
  brightness before downloading the full-res image, picking the first one within the configured
  threshold (or the closest one seen, if none qualify — it never fails a run over this).
- **Accent color**: Windows has a built-in "Automatically pick an accent color from my background"
  setting (Settings > Personalization > Colors). We just flip that registry switch on
  (`HKCU\...\Themes\Personalize\AutoColorization`) — Windows recomputes the accent color itself every
  time the wallpaper changes, so there's no color-extraction code here. This is a single system-wide
  color, not per-monitor.
- **Startup**: a `HKCU\...\CurrentVersion\Run` entry, not a Task Scheduler task. Task Scheduler's
  per-user `ONLOGON` task creation is blocked by policy on some machines (confirmed on this one:
  `schtasks /Create` returns Access Denied even for a non-elevated, `/RL LIMITED` task); the registry
  Run key only touches `HKCU` and always works. No artificial startup delay is needed — the retry/
  backoff below already covers "network isn't up yet right after logon".
- **No internet**: transient failures (DNS/connect/timeout) trigger exponential backoff
  (`BaseDelaySeconds * 2^attempt`, so 5s/10s/20s/40s/80s by default) up to `MaxRetries` attempts, then
  the process exits with a non-zero code and logs why. It does not retry forever.

## Playnite

Playnite doesn't read the OS desktop wallpaper — it has no built-in "use my wallpaper" toggle. Some
fullscreen themes (Darkbird, Stardust, and similar) do support pointing at a static custom background
image file in their own theme settings. If you set `PlayniteBackgroundPath` in the config to that
theme's expected file path, WallpaperFetcher will overwrite it with the same image it used for your
primary monitor after every run. This is best-effort and depends entirely on which theme you have
installed — check your theme's settings/docs for whether (and where) it accepts a custom background.
