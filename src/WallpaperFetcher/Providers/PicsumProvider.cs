// Lorem Picsum backend: keyless generic photos. Not anime/game art, but always up, so the run
// still changes the wallpaper when every niche provider is down.
using System.Text.Json;

namespace WallpaperFetcher.Providers;

public sealed class PicsumProvider : IWallpaperProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public PicsumProvider(HttpClient http) => _http = http;

    public string Name => "picsum";

    public async Task<IReadOnlyList<WallpaperCandidate>> GetCandidatesAsync(WallpaperQuery query, CancellationToken ct)
    {
        // Picsum has ~10 pages of 100 at this limit; older pages return []. Page 1 is the safety net.
        var page = Random.Shared.Next(1, 11);
        var items = await FetchListAsync(page, ct);
        if (items.Count == 0 && page != 1)
            items = await FetchListAsync(1, ct);

        if (items.Count == 0)
            return Array.Empty<WallpaperCandidate>();

        var usable = items.Where(i => i.Width >= query.MinWidth && i.Height >= query.MinHeight).ToList();
        if (usable.Count == 0)
            usable = items; // Picsum's catalogue is mostly large; keep going even if sizes look small.

        return usable.Select(i => new WallpaperCandidate(
            Name, i.Id,
            $"https://picsum.photos/id/{i.Id}/{query.MinWidth}/{query.MinHeight}",
            $"https://picsum.photos/id/{i.Id}/320/180",
            i.Width, i.Height)).ToList();
    }

    private async Task<List<Item>> FetchListAsync(int page, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"https://picsum.photos/v2/list?page={page}&limit=100", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<List<Item>>(stream, JsonOpts, ct) ?? new List<Item>();
    }

    private sealed class Item
    {
        public string Id { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
