// Shared brightness matching: from whichever provider's candidates won, pick one whose average
// luminance fits the current light/dark theme (or the closest, never failing the run over it).
namespace WallpaperFetcher.Providers;

public static class WallpaperSelector
{
    // Bound on thumbnail checks so worst-case extra requests stay small.
    private const int MaxBrightnessCandidates = 6;

    public static async Task<WallpaperCandidate> PickAsync(
        IReadOnlyList<WallpaperCandidate> candidates, WallpaperQuery query,
        int darkModeMaxBrightness, int lightModeMinBrightness,
        HttpClient http, CancellationToken ct)
    {
        if (query.PreferredMode is null)
            return candidates[Random.Shared.Next(candidates.Count)];

        var mode = query.PreferredMode.Value;
        var shuffled = candidates.OrderBy(_ => Random.Shared.Next()).Take(MaxBrightnessCandidates).ToList();

        WallpaperCandidate? closest = null;
        var closestDistance = double.MaxValue;

        foreach (var candidate in shuffled)
        {
            double brightness;
            try
            {
                var bytes = await http.GetByteArrayAsync(candidate.ThumbnailUrl ?? candidate.ImageUrl, ct);
                brightness = ImageProcessor.ComputeAverageBrightness(bytes);
            }
            catch
            {
                continue; // bad thumbnail: skip, don't let one flaky download sink the whole run
            }

            var isMatch = mode == ThemeMode.Dark
                ? brightness <= darkModeMaxBrightness
                : brightness >= lightModeMinBrightness;
            if (isMatch)
            {
                Logger.Log($"Theme match: picked brightness={brightness:0} for {mode} mode (source={candidate.ProviderName}).");
                return candidate;
            }

            var target = mode == ThemeMode.Dark ? 0 : 255;
            var distance = Math.Abs(brightness - target);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        var fallback = closest ?? candidates[Random.Shared.Next(candidates.Count)];
        Logger.Log($"No exact brightness match for {mode} mode; using closest candidate (distance={closestDistance:0}).");
        return fallback;
    }
}
