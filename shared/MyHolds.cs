namespace Mercantec.Tech.Contracts;

public sealed record MyHoldCard(Guid HoldId, Guid ForloebId, string HoldName, string ForloebName,
    string? Description, DateTimeOffset CreatedAtUtc, int MemberCount, int UnlinkedMemberCount,
    HoldLectioBadge[] LectioSources, bool CanManage, bool IsFavorite);

public sealed record HoldLectioBadge(string Key, string? Name);
public sealed record RenameHold(string Name);
