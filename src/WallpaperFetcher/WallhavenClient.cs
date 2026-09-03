// Thin client for the keyless Wallhaven search API, biased toward anime art and video-game fan art.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WallpaperFetcher;

public sealed record WallhavenResult(string Id, string ImageUrl, int Width, int Height);

public sealed class WallhavenClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Wallhaven has no dedicated "games" category; "general" tagged with a rotating title/franchise
    // gets us game fan-art/screenshots instead of the category's usual nature/car/misc noise.
    private static readonly string[] GameTags =
    {
        "video game", "cyberpunk 2077", "elden ring", "the legend of zelda", "final fantasy",
        "genshin impact", "persona 5", "halo", "overwatch", "god of war",
        "horizon zero dawn", "destiny 2", "league of legends", "valorant",
        "starfield", "hollow knight", "hades", "metal gear solid", "resident evil",
    };

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public WallhavenClient(HttpClient http, string? apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<WallhavenResult?> GetRandomWallpaperAsync(
        WallpaperCategory category, int minWidth, int minHeight, CancellationToken ct)
    {
        var query = new List<string>
        {
            "sorting=random",
            "purity=100", // sfw only
            $"atleast={minWidth}x{minHeight}",
        };

        switch (category)
        {
            case WallpaperCategory.Anime:
                query.Add("categories=010");
                break;
            case WallpaperCategory.Games:
            default:
                query.Add("categories=100");
                query.Add($"q={Uri.EscapeDataString(GameTags[Random.Shared.Next(GameTags.Length)])}");
                break;
        }

        if (!string.IsNullOrWhiteSpace(_apiKey))
            query.Add($"apikey={_apiKey}");

        var url = $"https://wallhaven.cc/api/v1/search?{string.Join('&', query)}";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var doc = await JsonSerializer.DeserializeAsync<WallhavenSearchResponse>(stream, JsonOpts, ct);

        var candidates = doc?.Data;
        if (candidates is null || candidates.Count == 0)
            return null;

        var pick = candidates[Random.Shared.Next(candidates.Count)];
        return new WallhavenResult(pick.Id, pick.Path, pick.DimensionX, pick.DimensionY);
    }

    private sealed class WallhavenSearchResponse
    {
        public List<WallhavenItem> Data { get; set; } = new();
    }

    private sealed class WallhavenItem
    {
        public string Id { get; set; } = "";
        public string Path { get; set; } = "";

        [JsonPropertyName("dimension_x")]
        public int DimensionX { get; set; }

        [JsonPropertyName("dimension_y")]
        public int DimensionY { get; set; }
    }
}
