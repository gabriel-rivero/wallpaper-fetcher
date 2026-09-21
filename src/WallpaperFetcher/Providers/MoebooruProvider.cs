// Moebooru-family backend (Konachan, Yande.re): keyless /post.json, supports order:random and
// rating:safe. Used as an anime/game fallback when Wallhaven is unreachable.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WallpaperFetcher.Providers;

public sealed class MoebooruProvider : IWallpaperProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public MoebooruProvider(string name, string baseUrl, HttpClient http)
    {
        Name = name;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = http;
    }

    public string Name { get; }

    public async Task<IReadOnlyList<WallpaperCandidate>> GetCandidatesAsync(WallpaperQuery query, CancellationToken ct)
    {
        var tags = new List<string> { "rating:safe", "order:random" };
        if (query.Category == WallpaperCategory.Games)
            tags.Add(WallpaperTags.RandomGameTag());

        // The size metatag isn't supported here, so over-fetch random posts and filter by size below.
        var url = $"{_baseUrl}/post.json?limit=100&tags={Uri.EscapeDataString(string.Join(' ', tags))}";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var posts = await JsonSerializer.DeserializeAsync<List<Post>>(stream, JsonOpts, ct);

        return (posts ?? new List<Post>())
            .Where(p => !string.IsNullOrWhiteSpace(p.FileUrl)
                        && p.Width >= query.MinWidth && p.Height >= query.MinHeight)
            .Select(p => new WallpaperCandidate(
                Name, p.Id.ToString(), p.FileUrl,
                string.IsNullOrWhiteSpace(p.PreviewUrl) ? null : p.PreviewUrl,
                p.Width, p.Height))
            .ToList();
    }

    private sealed class Post
    {
        public long Id { get; set; }

        [JsonPropertyName("file_url")]
        public string FileUrl { get; set; } = "";

        [JsonPropertyName("preview_url")]
        public string PreviewUrl { get; set; } = "";

        public int Width { get; set; }
        public int Height { get; set; }
    }
}
