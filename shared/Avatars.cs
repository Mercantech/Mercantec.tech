namespace Mercantec.Tech.Contracts;

public sealed record AvatarProfileDto(
    string SelectedAvatarId,
    IReadOnlyList<string> OwnedAvatarIds,
    int Gold,
    int Level,
    IReadOnlyList<string> OwnedItemIds,
    bool CanImportLegacy,
    long Revision,
    IReadOnlyList<AvatarUnlockProgressDto>? UnlockProgress = null);

public sealed record AvatarUnlockProgressDto(
    string AvatarId,
    string RequirementId,
    int Current,
    int Required,
    bool IsMet);

public static class AvatarRequirementKinds
{
    public const string SentGiftsDistinctRecipients = "sent_gifts_distinct_recipients";
    public const string UploadedGames = "uploaded_games";
}

public sealed record AvatarUnlockRequirementDefinition(
    string Id,
    string Kind,
    string Target,
    int RequiredCount,
    string Description);

public sealed record AvatarSelectionRequest(string AvatarId);

public sealed record AvatarLegacyImportRequest(
    IReadOnlyList<string> OwnedAvatarIds,
    string? SelectedAvatarId,
    int Gold);

// These are the existing universe demo's opening balance and level. They are
// server-owned defaults, not a client claim about earned XP or money.
public static class AvatarEconomy
{
    public const string StarterId = "starter";
    public const string Bip01Id = "bip01";
    public const string RaymondId = "raymond";
    public const string SantaId = "santa";
    public const string LuppId = "lupp";
    public const int InitialGold = 320;
    public const int InitialLevel = 7;

    public static IReadOnlyList<AvatarUnlockDefinition> Avatars { get; } =
        Array.AsReadOnly<AvatarUnlockDefinition>(
        [
            new(StarterId, "Din første avatar", 1, 0, LegacyImportEligible: true),
            new(Bip01Id, "BIP-01", 2, 150, LegacyImportEligible: true),
            new(RaymondId, "Raymond", 3, 150),
            new(SantaId, "Julemanden", 4, 150, Requirements:
            [
                new("christmas-gift-recipients", AvatarRequirementKinds.SentGiftsDistinctRecipients,
                    "christmas", 3, "Send gaver i juletemaet til 3 forskellige brugere")
            ]),
            new(LuppId, "LUPP", 1, 0, Requirements:
            [
                new("uploaded-game", AvatarRequirementKinds.UploadedGames, "site", 1, "Upload dit eget spil til LUP")
            ])
        ]);

    public static IReadOnlyList<AvatarShopItemDefinition> Furniture { get; } =
        Array.AsReadOnly<AvatarShopItemDefinition>(
        [
            new("desk", 1, 0),
            new("bed", 1, 0),
            new("chair", 1, 0),
            new("plant", 1, 0),
            new("rug", 2, 50),
            new("lamp", 3, 80),
            new("trophy", 4, 140),
            new("bookcase", 5, 100),
            new("monstera", 6, 85),
            new("sofa", 8, 180),
            new("arcade", 10, 260, "gdd", 2),
            new("workstation", 12, 290, "api", 2)
        ]);

    public static IReadOnlyList<string> InitialItemIds { get; } =
        Array.AsReadOnly<string>(["desk", "bed", "chair", "plant", "board"]);

    public static AvatarUnlockDefinition? FindAvatar(string? id) =>
        Avatars.FirstOrDefault(avatar => avatar.Id == id);

    public static AvatarShopItemDefinition? FindFurniture(string? id) =>
        Furniture.FirstOrDefault(item => item.Id == id);
}

// Only avatars that existed before account-backed ownership can be migrated
// from browser data. New catalog entries must be unlocked on the server.
public sealed record AvatarUnlockDefinition(
    string Id,
    string Name,
    int Level,
    int Cost,
    bool LegacyImportEligible = false,
    IReadOnlyList<AvatarUnlockRequirementDefinition>? Requirements = null);

public sealed record AvatarShopItemDefinition(
    string Id,
    int Level,
    int Cost,
    string? RequiredBadge = null,
    int RequiredBadgeRank = 0);
