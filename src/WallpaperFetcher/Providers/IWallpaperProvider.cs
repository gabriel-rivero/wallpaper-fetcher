// Provider abstraction: one wallpaper backend (Wallhaven, Konachan, ...) behind a common
// candidate-listing API, so the chain can fall back or hedge across several services.
namespace WallpaperFetcher.Providers;

public sealed record WallpaperQuery(
    WallpaperCategory Category, int MinWidth, int MinHeight, ThemeMode? PreferredMode);

public sealed record WallpaperCandidate(
    string ProviderName, string Id, string ImageUrl, string? ThumbnailUrl, int Width, int Height);

public interface IWallpaperProvider
{
    string Name { get; }

    Task<IReadOnlyList<WallpaperCandidate>> GetCandidatesAsync(WallpaperQuery query, CancellationToken ct);
}
