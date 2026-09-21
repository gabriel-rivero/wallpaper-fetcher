// Shared pool of well-known franchise names used to bias "games" queries across providers.
namespace WallpaperFetcher.Providers;

public static class WallpaperTags
{
    // Wallhaven has no "games" category; the Moebooru boorus tag game fan art by franchise.
    private static readonly string[] GameFranchises =
    {
        "video game", "cyberpunk 2077", "elden ring", "the legend of zelda", "final fantasy",
        "genshin impact", "persona 5", "halo", "overwatch", "god of war",
        "horizon zero dawn", "destiny 2", "league of legends", "valorant",
        "starfield", "hollow knight", "hades", "metal gear solid", "resident evil",
    };

    public static string RandomGameFranchise() => GameFranchises[Random.Shared.Next(GameFranchises.Length)];

    // Booru tags are lowercase with underscores ("cyberpunk_2077").
    public static string RandomGameTag() => RandomGameFranchise().Replace(' ', '_');
}
