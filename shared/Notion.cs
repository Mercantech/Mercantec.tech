namespace Mercantec.Tech.Contracts;

public sealed record NotionHold(Guid Id, string Name, bool CanManage);
public sealed record NotionPageSummary(Guid Id, Guid HoldId, string Title, Guid? ParentPageId, DateTimeOffset UpdatedAtUtc);
public sealed record CreateNotionPage(Guid HoldId, string Title, Guid? ParentPageId = null);
public sealed record RenameNotionPage(string Title);
public sealed record NotionMember(Guid Id, string DisplayName);
public sealed record NotionPollReference(Guid Id, string Title, string State);
public sealed record NotionPageContext(Guid PageId, Guid HoldId, string Title, bool CanEdit,
    NotionMember[] Members, NotionPollReference[] Polls, DateTimeOffset UpdatedAtUtc);
public sealed record AuthorizeNotionCollaboration(string DocumentName);
public sealed record NotionCollaborationAccess(Guid DocumentId, bool CanEdit);
