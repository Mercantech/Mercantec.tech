namespace Mercantec.Tech.Contracts;

public sealed record ArenaInput(long Sequence = 0, double Horizontal = 0, double Vertical = 0,
    bool Jump = false, bool Attack = false, bool Special = false, bool Shield = false,
    bool Grab = false, bool Smash = false, bool Dodge = false);

/// <summary>Cosmetic starter-avatar choices. These never change combat statistics.</summary>
public sealed record ArenaAppearance(string Skin = "#c98d69", string Hair = "#42302a",
    string HairStyle = "short", string Outfit = "#2452e8")
{
    public static ArenaAppearance Sanitize(ArenaAppearance? appearance) => new(
        Color(appearance?.Skin, "#c98d69"),
        Color(appearance?.Hair, "#42302a"),
        appearance?.HairStyle is "short" or "long" or "cap" ? appearance.HairStyle : "short",
        Color(appearance?.Outfit, "#2452e8"));

    private static string Color(string? value, string fallback) =>
        value is { Length: 7 } && value[0] == '#' &&
        value.Skip(1).All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')
            ? value : fallback;
}

public sealed record ArenaPlatform(double X, double Y, double Width, double Height = 18, bool OneWay = true);
public sealed record ArenaStage(string Id, string Name, string Description, string Accent,
    IReadOnlyList<ArenaPlatform> Platforms, double Left = -220, double Right = 1500,
    double Top = -300, double Bottom = 1000);

public static class ArenaCatalog
{
    public const int TickRate = 60;
    public const int Width = 1280;
    public const int Height = 720;
    public static IReadOnlyList<ArenaStage> Stages { get; } = Array.AsReadOnly<ArenaStage>(
    [
        new("rooftops", "Over tagene", "Et åbent tag med tre svævende platforme.", "#a5a6ff",
        [new(215, 565, 850, 35, false), new(310, 415, 230), new(740, 415, 230), new(525, 290, 230)]),
        new("workshop", "Værkstedet", "Tæt kamp på værkstedets brede arbejdsbord.", "#ffc766",
        [new(165, 565, 950, 40, false), new(280, 385, 280), new(720, 385, 280)]),
        new("skyline", "Skyøerne", "Tre adskilte øer. Gem et hop til turen hjem.", "#64d6d2",
        [new(155, 530, 260, 30, false), new(485, 585, 310, 35, false), new(865, 530, 260, 30, false), new(485, 335, 310)])
    ]);
    public static ArenaStage FindStage(string? id) => Stages.FirstOrDefault(x => x.Id == id) ?? Stages[0];
    public static IReadOnlyList<string> Colors { get; } = Array.AsReadOnly<string>(["#ff647c", "#64b5ff", "#ffc766", "#b297ff"]);
}

public sealed record ArenaFighterSnapshot(string Id, string Name, string AvatarId, string Color,
    double X, double Y, double Vx, double Vy, int Facing, double Damage, int Stocks,
    string Action, int ActionFrame, double Shield, bool Invulnerable, bool Connected,
    bool IsBot, bool Grounded, int JumpsRemaining, double Charge, int KOs, int Falls,
    long LastInputSequence, bool Shielding, bool Out, ArenaAppearance? Appearance = null);

public sealed record ArenaProjectileSnapshot(long Id, string OwnerId, double X, double Y,
    double Vx, double Vy, double Radius, string Color);
public sealed record ArenaEvent(long Id, int Tick, string Type, double X, double Y,
    string? ActorId = null, string? TargetId = null, double Strength = 1);
public sealed record ArenaState(int Tick, string Phase, double Countdown, double RemainingSeconds,
    string StageId, IReadOnlyList<ArenaFighterSnapshot> Fighters,
    IReadOnlyList<ArenaProjectileSnapshot> Projectiles, IReadOnlyList<ArenaEvent> Events,
    string? WinnerId, bool SuddenDeath);
