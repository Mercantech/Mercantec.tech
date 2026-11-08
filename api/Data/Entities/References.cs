namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Projection of live CRDT references; labels and permissions are always resolved at read time.</summary>
public sealed class EntityReferenceLink
{
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Visibility { get; set; } = "";
    public string TargetKind { get; set; } = "";
    public string TargetId { get; set; } = "";
    public bool IsMention { get; set; }
}

public sealed class ReferenceIndexState
{
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Visibility { get; set; } = "";
    public string Fingerprint { get; set; } = "";
}

/// <summary>Durable occurrence; contains no copied title, body, person name or access grant.</summary>
public sealed class ReferenceMentionEvent
{
    public Guid Id { get; set; }
    public Guid RecipientSub { get; set; }
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Visibility { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
