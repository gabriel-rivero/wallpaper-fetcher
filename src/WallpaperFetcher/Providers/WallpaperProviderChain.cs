// Resolves the configured providers and acquires a wallpaper from whichever one is up:
// sequential fallback by default, or a parallel hedge (first responder wins) when enabled.
// Retries the whole set with backoff so an offline-at-logon run still recovers.
namespace WallpaperFetcher.Providers;

public sealed class WallpaperProviderChain
{
    // In hedge mode each provider starts this much after the previous one, so a higher-priority
    // provider gets a head start (and wins if it's healthy) without a dead one blocking the rest.
    private static readonly TimeSpan HedgeStagger = TimeSpan.FromMilliseconds(400);

    private readonly List<IWallpaperProvider> _providers;
    private readonly bool _hedge;

    public WallpaperProviderChain(HttpClient http, AppConfig config)
    {
        _hedge = config.HedgeProviders;
        _providers = config.ProviderOrder
            .Select(name => Resolve(name, http, config.WallhavenApiKey))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        if (_providers.Count == 0)
            _providers.Add(new WallhavenProvider(http, config.WallhavenApiKey));
    }

    public IReadOnlyList<string> ProviderNames => _providers.Select(p => p.Name).ToList();

    public async Task<WallpaperCandidate> GetWallpaperAsync(
        WallpaperQuery query, int darkModeMaxBrightness, int lightModeMinBrightness,
        HttpClient http, int maxAttempts, TimeSpan baseDelay, CancellationToken ct)
    {
        Exception? last = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var candidates = _hedge
                    ? await FetchFromFirstResponderAsync(query, ct)
                    : await FetchSequentiallyAsync(query, ct);

                if (candidates.Count > 0)
                    return await WallpaperSelector.PickAsync(
                        candidates, query, darkModeMaxBrightness, lightModeMinBrightness, http, ct);

                last = new InvalidOperationException("No provider returned usable candidates.");
            }
            catch (Exception ex)
            {
                last = ex;
            }

            if (attempt == maxAttempts)
                break;

            var delay = TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
            Logger.Log($"All providers failed (attempt {attempt}/{maxAttempts}); retrying in {delay.TotalSeconds:0}s...");
            await Task.Delay(delay, ct);
        }

        throw new RetryExhaustedException("Every configured wallpaper provider failed.", last!);
    }

    private async Task<IReadOnlyList<WallpaperCandidate>> FetchSequentiallyAsync(WallpaperQuery query, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var candidates = await provider.GetCandidatesAsync(query, ct);
                if (candidates.Count > 0)
                {
                    Logger.Log($"Provider '{provider.Name}' returned {candidates.Count} candidate(s).");
                    return candidates;
                }

                Logger.Log($"Provider '{provider.Name}' returned no usable candidates; trying next.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Provider '{provider.Name}' failed ({ex.GetType().Name}: {ex.Message}); trying next.");
            }
        }

        return Array.Empty<WallpaperCandidate>();
    }

    private async Task<IReadOnlyList<WallpaperCandidate>> FetchFromFirstResponderAsync(WallpaperQuery query, CancellationToken ct)
    {
        var pending = _providers
            .Select((provider, index) => FetchAfterDelayAsync(provider, query, HedgeStagger * index, ct))
            .ToList();
        var failures = new List<string>();

        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending);
            pending.Remove(done);

            var (name, candidates, error) = await done;
            if (candidates.Count > 0)
            {
                Logger.Log($"Hedged request: provider '{name}' won with {candidates.Count} candidate(s).");
                return candidates;
            }

            if (error is not null)
                failures.Add($"{name}: {error.GetType().Name}");
        }

        if (failures.Count > 0)
            Logger.Log($"All hedged providers failed ({string.Join(", ", failures)}).");

        return Array.Empty<WallpaperCandidate>();
    }

    private static async Task<(string Name, IReadOnlyList<WallpaperCandidate> Candidates, Exception? Error)>
        FetchAfterDelayAsync(IWallpaperProvider provider, WallpaperQuery query, TimeSpan delay, CancellationToken ct)
    {
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct);

        return await SafeFetchAsync(provider, query, ct);
    }

    private static async Task<(string Name, IReadOnlyList<WallpaperCandidate> Candidates, Exception? Error)>
        SafeFetchAsync(IWallpaperProvider provider, WallpaperQuery query, CancellationToken ct)
    {
        try
        {
            return (provider.Name, await provider.GetCandidatesAsync(query, ct), null);
        }
        catch (Exception ex)
        {
            return (provider.Name, Array.Empty<WallpaperCandidate>(), ex);
        }
    }

    private static IWallpaperProvider? Resolve(string name, HttpClient http, string? apiKey) =>
        name.Trim().ToLowerInvariant() switch
        {
            "wallhaven" => new WallhavenProvider(http, apiKey),
            "konachan" => new MoebooruProvider("konachan", "https://konachan.net", http),
            "yandere" or "yande.re" => new MoebooruProvider("yandere", "https://yande.re", http),
            "picsum" => new PicsumProvider(http),
            _ => null,
        };
}
