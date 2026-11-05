namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>One hold-scoped document. Yjs binary is the canonical collaborative state.</summary>
public sealed class NotionPage
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
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
    public int SchemaVersion { get; set; } = 1;
}
