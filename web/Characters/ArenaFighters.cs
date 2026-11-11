using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Demo;

namespace Mercantec.Tech.Web.Characters;

public static class ArenaFighters
{
    public const string RosterSectionId = "arena-fighters";
    public const string RosterPath = "/characters#" + RosterSectionId;
    public const string PlayPath = "/arena?view=fighters";

    public static AvatarDefinition? ForCharacter(string? path) => path is null
        ? null
        : AvatarCatalog.All.FirstOrDefault(avatar => string.Equals(avatar.CharacterProfilePath, path, StringComparison.OrdinalIgnoreCase));

    public static string CollectionPath(string avatarId) => "/profile/character?avatar=" + Uri.EscapeDataString(avatarId);

    public static IReadOnlyList<string> Requirements(string avatarId)
    {
        var unlock = AvatarEconomy.FindAvatar(avatarId);
        if (unlock is null)
            return [];
        if (unlock.Id == AvatarEconomy.StarterId)
            return ["Tilgængelig fra start"];

        var cost = unlock.Cost == 0 ? "Gratis" : $"{unlock.Cost} guld";
        return
        [
            unlock.Level > 1 ? $"Level {unlock.Level} · {cost}" : cost,
            .. (unlock.Requirements ?? []).Select(requirement => requirement.Kind == AvatarRequirementKinds.UploadedGames
                ? $"{requirement.Description} og få det valideret"
                : requirement.Description)
        ];
    }
}
