namespace Mercantec.Tech.Contracts;

public sealed record MoveNotebookPage(Guid? ParentPageId, Guid? BeforePageId = null, Guid? AfterPageId = null);
public sealed record NotebookPageDeletionPreview(int DescendantCount);
public sealed record TrashedNotebookPage(Guid Id, string Title, DateTimeOffset DeletedAtUtc, int DescendantCount);
