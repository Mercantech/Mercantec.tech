namespace Mercantec.Tech.Contracts;

public sealed record NotionCollectionView(Guid Id, Guid? RootPageId, string Title, Guid? HoldId,
    string? HoldName, bool CanManage, DateTimeOffset UpdatedAtUtc,
    Guid? IconEmojiId = null, string? LastEditedByName = null);
public sealed record CreateNotionCollection(string Title, Guid? HoldId = null, Guid? IconEmojiId = null);
public sealed record SetNotebookIcon(Guid? IconEmojiId);
