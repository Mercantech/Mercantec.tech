namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>A teaching document in a hold collection or a standalone theory collection.</summary>
public sealed class NotionPage
{
    public Guid Id { get; set; }
    public Guid? HoldId { get; set; }
    public Guid? CollectionId { get; set; }
    public NotionCollection? Collection { get; set; }
    public Guid? ParentPageId { get; set; }
    public string Title { get; set; } = "";
    public Guid CreatedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[]? YjsState { get; set; }
    public long DraftRevision { get; set; }
    public string? DraftFingerprint { get; set; }
    public byte[]? PublishedState { get; set; }
    public string? PublishedTitle { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public Guid? PublishedBySub { get; set; }
    public long? PublishedRevision { get; set; }
    public long PublicationVersion { get; set; }
    public string PublishedAssetIdsJson { get; set; } = "[]";
    public string DrawingIdsJson { get; set; } = "[]";
    public string PublishedDrawingsJson { get; set; } = "[]";
    public int SchemaVersion { get; set; } = 1;
}

/// <summary>The ID is also the canonical root page ID, preserving existing collection links.</summary>
public sealed class NotionCollection
{
    public Guid Id { get; set; }
    public Guid? HoldId { get; set; }
    public Guid CreatedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
