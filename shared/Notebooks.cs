namespace Mercantec.Tech.Contracts;

public sealed record NotebookInfo(Guid Id, Guid OwnerSub, string OwnerName, bool CanEdit, bool CanShare);
public sealed record NotebookPageSummary(Guid Id, Guid NotebookId, string Title, Guid? ParentPageId,
    string Kind, string[] Tags, DateOnly? PeriodStart, Guid? HoldId, Guid? TemplateId,
    DateTimeOffset UpdatedAtUtc, long Revision, string? Snippet = null);
public sealed record CreateNotebookPage(string Title, Guid? ParentPageId = null);
public sealed record UpdateNotebookPage(string Title, Guid? ParentPageId, string[] Tags, Guid? HoldId = null);
public sealed record NotebookPageList(NotebookPageSummary[] Items, int Total);
public sealed record NotebookDocumentContext(Guid DocumentId, Guid? NotebookId, string Title, string Kind,
    bool CanRead, bool CanEdit, bool CanComment, bool CanShare, string? StateBase64,
    DateTimeOffset UpdatedAtUtc, long Revision, string? Fingerprint, NotionPageLink[] Pages,
    string[] Tags, DateOnly? PeriodStart = null, Guid? HoldId = null);
public sealed record NotebookGraph(NotebookGraphNode[] Nodes, NotionGraphEdge[] Edges, int Total);
public sealed record NotebookGraphNode(Guid Id, string Title);
public sealed record NotebookUser(Guid Sub, string DisplayName, bool IsTeacher);
public sealed record GrantNotebookShare(Guid RecipientSub, Guid? PageId = null);
public sealed record NotebookShareView(Guid Id, Guid RecipientSub, string RecipientName, Guid? PageId,
    DateTimeOffset CreatedAtUtc);
public sealed record NotebookCommentView(Guid Id, Guid AuthorSub, string AuthorName, string Text,
    DateTimeOffset CreatedAtUtc);
public sealed record AddNotebookComment(string Text);
public sealed record TeacherNotebookView(Guid Id, Guid OwnerSub, string OwnerName, string[] Holds,
    DateOnly? LatestJournalDate, DateTimeOffset UpdatedAtUtc);
public sealed record NotebookTemplateView(Guid Id, Guid DocumentId, string Title, string Cadence,
    bool IsActive, int Version, DateTimeOffset UpdatedAtUtc);
public sealed record CreateNotebookTemplate(string Title, string Cadence);
public sealed record UpdateNotebookTemplate(string Title, string Cadence);
public sealed record ActivateNotebookTemplate(long ExpectedRevision, string ExpectedFingerprint);
public sealed record EnsureJournalEntry(Guid TemplateId, Guid RequestId, DateOnly? PeriodDate = null,
    Guid? HoldId = null);

// These projections are accepted only from the authenticated collaboration service.
public sealed record StoreNotebookDocument(string StateBase64, string Fingerprint, string PlainText,
    Guid[] LinkIds, Guid[] AssetIds);
public sealed record CloneNotebookDocument(string? StateBase64, Guid PageId,
    Dictionary<string, string> AssetMap, bool DefaultTemplate = false);
