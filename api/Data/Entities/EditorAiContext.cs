namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Server-private, revision-bound teaching text. Access is always rechecked against its source.</summary>
public sealed class EditorAiTextChunk
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public Guid? EntityId { get; set; }
    public Guid? HoldId { get; set; }
    public Guid? CollectionId { get; set; }
    public string Fingerprint { get; set; } = "";
    public int Ordinal { get; set; }
    public string Title { get; set; } = "";
    public string Href { get; set; } = "";
    public string? Location { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>One versioned text-extraction job, processed by the isolated storage/converter services.</summary>
public sealed class EditorAiFileText
{
    public Guid FileId { get; set; }
    public FileAsset File { get; set; } = null!;
    public string Fingerprint { get; set; } = "";
    public string State { get; set; } = "pending";
    public string Text { get; set; } = "";
    public string? Error { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
