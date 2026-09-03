// Wraps IDesktopWallpaper: monitor enumeration (id + native resolution) and per-monitor/all-monitor wallpaper set.
using System.Runtime.InteropServices;
using WallpaperFetcher.Native;

namespace WallpaperFetcher;

public sealed record MonitorInfo(string DeviceId, int Width, int Height, int Index);

public sealed class WallpaperService
{
    private readonly IDesktopWallpaper _dw = (IDesktopWallpaper)new DesktopWallpaperClass();

    public List<MonitorInfo> GetMonitors()
    {
        // GetMonitorDevicePathCount can include stale device paths for monitors that were
        // previously connected but are now detached; GetMonitorRECT is documented to fail
        // with E_FAIL for those, which is how we tell attached monitors from stale entries.
        var count = _dw.GetMonitorDevicePathCount();
        var monitors = new List<MonitorInfo>();
        for (uint i = 0; i < count; i++)
        {
            var id = _dw.GetMonitorDevicePathAt(i);
            try
            {
                var rect = _dw.GetMonitorRECT(id);
                monitors.Add(new MonitorInfo(id, rect.Width, rect.Height, monitors.Count));
            }
            catch (COMException ex)
            {
                Logger.Log($"Skipping detached monitor device path '{id}' ({ex.Message}).");
            }
        }
        return monitors;
    }

    public void SetWallpaper(string monitorId, string filePath)
    {
        _dw.SetPosition(DesktopWallpaperPosition.Fill);
        _dw.SetWallpaper(monitorId, filePath);
    }

    public void SetWallpaperForAll(string filePath)
    {
        _dw.SetPosition(DesktopWallpaperPosition.Fill);
        _dw.SetWallpaper(null, filePath);
    }
}
