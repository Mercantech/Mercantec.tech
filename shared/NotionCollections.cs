namespace Mercantec.Tech.Contracts;

public sealed record NotionCollectionView(Guid Id, Guid RootPageId, string Title, Guid? HoldId,
    string? HoldName, bool CanManage, DateTimeOffset UpdatedAtUtc);
public sealed record CreateNotionCollection(string Title, Guid? HoldId = null);
