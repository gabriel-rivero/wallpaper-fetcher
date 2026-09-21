// Wallhaven search backend (keyless by default); biases anime art and game fan art by category.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WallpaperFetcher.Providers;

public sealed class WallhavenProvider : IWallpaperProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Wallhaven's dominant-color search only accepts colors from its fixed swatch list.
    private const string DarkSwatch = "000000";
    private const string LightSwatch = "ffffff";

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public WallhavenProvider(HttpClient http, string? apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public string Name => "wallhaven";

    public async Task<IReadOnlyList<WallpaperCandidate>> GetCandidatesAsync(WallpaperQuery query, CancellationToken ct)
    {
        var url = BuildSearchUrl(query);
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var doc = await JsonSerializer.DeserializeAsync<SearchResponse>(stream, JsonOpts, ct);

        return (doc?.Data ?? new List<Item>())
            .Select(i => new WallpaperCandidate(
                Name, i.Id, i.Path, string.IsNullOrWhiteSpace(i.Thumbs.Small) ? null : i.Thumbs.Small,
                i.DimensionX, i.DimensionY))
            .ToList();
    }

    private string BuildSearchUrl(WallpaperQuery query)
    {
        var parts = new List<string>
        {
            "sorting=random",
            "purity=100", // sfw only
            $"atleast={query.MinWidth}x{query.MinHeight}",
        };

        if (query.Category == WallpaperCategory.Anime)
        {
            parts.Add("categories=010");
        }
        else
        {
            parts.Add("categories=100");
            parts.Add($"q={Uri.EscapeDataString(WallpaperTags.RandomGameFranchise())}");
        }

        if (query.PreferredMode is not null)
            parts.Add($"colors={(query.PreferredMode == ThemeMode.Dark ? DarkSwatch : LightSwatch)}");

        if (!string.IsNullOrWhiteSpace(_apiKey))
            parts.Add($"apikey={_apiKey}");

        return $"https://wallhaven.cc/api/v1/search?{string.Join('&', parts)}";
    }

    private sealed class SearchResponse
    {
        public List<Item> Data { get; set; } = new();
    }

    private sealed class Item
    {
        public string Id { get; set; } = "";
        public string Path { get; set; } = "";

        [JsonPropertyName("dimension_x")]
        public int DimensionX { get; set; }

        [JsonPropertyName("dimension_y")]
        public int DimensionY { get; set; }

        public Thumbs Thumbs { get; set; } = new();
    }

    private sealed class Thumbs
    {
        public string Small { get; set; } = "";
    }
}
