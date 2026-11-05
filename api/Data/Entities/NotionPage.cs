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
    public int SchemaVersion { get; set; } = 1;
}
