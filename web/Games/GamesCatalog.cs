namespace Mercantec.Tech.Web.Games;

public sealed record ClassicGame(string Id, string Name, string Description, string Artwork, string LegacyPath)
{
    public string Path => GamesCatalog.Path + "/" + Id;
    public string ArtworkUrl => "/assets/games/classics/" + Artwork;
}

public sealed record ClassicGamePage(string Title, string LegacyPath)
{
    public string Url => GamesCatalog.LegacyOrigin + "/" + LegacyPath;
}

public static class GamesCatalog
{
    public const string Path = "/games";
    public const string ProjectsPath = "/projects/mine";
    public const string GameCategory = "spil";
    public const string GameProjectsPath = ProjectsPath + "?category=" + GameCategory;
    public const string UploadSection = "upload-spil";
    public const string UploadGameProjectPath = GameProjectsPath + "#" + UploadSection;
    public const string GuidePath = Path + "/guide";
    public const string LegacyOrigin = "https://games.mercantec.tech";

    public static IReadOnlyList<ClassicGame> Classics { get; } =
    [
        new("bomberman", "Bomberman", "Læg bomber, saml bonusser, og bliv den sidste på banen.", "bomberman-nes.jpg", "Bomberman/"),
        new("wizard", "Wizard Duel", "Brug besværgelser og mana i en troldmandsduel.", "wizard-duel.jpg", "Wizard/"),
        new("tetris", "Tetris Battle", "Ryd linjer, og send dem videre til dine modspillere.", "tetris-thumb.jpg", "Tetris/"),
        new("pong", "Pong", "Hold bolden i spil, og nå først til 11 point.", "pong-thumb.jpg", "Pong/"),
        new("tower-defense", "Tower Defense", "Byg tårne langs stien, og hold stand i tolv bølger.", "tower-defense-thumb.jpg", "TowerDefense/")
    ];

    public static ClassicGamePage? FindClassicPage(string? id, string? manual = null)
    {
        if (manual is not null)
            return manual switch
            {
                "bomber" or "tetris" or "pong" or "tower" => new("Spilmanual", "manual/" + manual),
                _ => null
            };

        if (Classics.FirstOrDefault(game => game.Id == id) is { } classic)
            return new(classic.Name, classic.LegacyPath);

        return id switch
        {
            "guide" => new("Arduino Oplà-guide", "guide"),
            "manual" => new("Spilmanualer", "manual"),
            "bracket" => new("Turnering", "bracket"),
            "status" => new("Spilstatus", "status"),
            _ => null
        };
    }

    public static bool IsPublicClassicPath(string path) => path.Split('/') switch
    {
        ["games", var id] => FindClassicPage(id) is not null,
        ["games", "manual", var manual] => FindClassicPage(null, manual) is not null,
        _ => false
    };
}
