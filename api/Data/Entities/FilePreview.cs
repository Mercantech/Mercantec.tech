namespace Mercantec.Tech.Api.Data.Entities;

public sealed class FilePreview
{
    public Guid FileId { get; set; }
    public FileAsset File { get; set; } = null!;
    public string State { get; set; } = "processing";
    public string? StorageKey { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
