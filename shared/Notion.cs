namespace Mercantec.Tech.Contracts;

public sealed record NotionHold(Guid Id, string Name, bool CanManage);
public sealed record NotionPageSummary(Guid Id, Guid? HoldId, string Title, Guid? ParentPageId, DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc = null, bool HasUnpublishedChanges = false, Guid? CollectionId = null, int? SortOrder = null);
public sealed record CreateNotionPage(Guid? HoldId, string Title, Guid? ParentPageId = null, Guid? CollectionId = null,
    bool AtRoot = false);
public sealed record RenameNotionPage(string Title);
public sealed record NotionMember(Guid Id, string DisplayName);
public sealed record NotionPollReference(Guid Id, string Title, string State);
public sealed record NotionPageLink(Guid Id, string Title, Guid? ParentPageId, int? SortOrder = null);
public sealed record NotionEditorResources(NotionMember[] Members, NotionPollReference[] Polls);
public sealed record NotionNavigationContext(Guid WorkspaceId, Guid HoldId, Guid[] CourseIds);
public sealed record NotionPageContext(Guid PageId, Guid? HoldId, string Title, bool CanEdit,
    NotionMember[] Members, NotionPollReference[] Polls, DateTimeOffset UpdatedAtUtc,
    NotionPageLink[]? Pages = null, DateTimeOffset? PublishedAtUtc = null, long DraftRevision = 0,
    long? PublishedRevision = null, string? PublishedStateBase64 = null, string? PublishedTitle = null,
    DrawingPublicationBinding[]? Drawings = null, Guid? CollectionId = null, long LifecycleVersion = 0);
public sealed record PublishNotionPage(long ExpectedRevision, string ExpectedFingerprint, Guid[] AssetIds,
    DrawingPublicationRequest[]? Drawings = null);
public sealed record StoreNotionDocument(string StateBase64, string Fingerprint, Guid[]? DrawingIds = null,
    Dictionary<string, string>? DrawingBlocks = null, EntityReference[]? References = null,
    StoreNotionMcpChange? McpChange = null, string? EditorName = null, bool PreserveEditMetadata = false, long LifecycleVersion = 0);
public sealed record AuthorizeNotionCollaboration(string DocumentName);
public sealed record NotionCollaborationAccess(Guid DocumentId, bool CanEdit, string? EditorName = null, long LifecycleVersion = 0);
public sealed record NotionGraph(Guid? HoldId, Guid CollectionId, string Title, bool CanRename,
    NotionGraphNode[] Nodes, NotionGraphEdge[] Edges, bool WholeHold = false);
public sealed record NotionGraphNode(Guid Id, string Title);
public sealed record NotionGraphEdge(Guid FromId, Guid ToId);
