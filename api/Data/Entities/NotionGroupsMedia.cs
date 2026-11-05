namespace Mercantec.Tech.Api.Data.Entities;

public sealed class NotionAsset
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public NotionPage Page { get; set; } = null!;
    public string Key { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string Kind { get; set; } = "download";
    public long SizeBytes { get; set; }
    public Guid UploadedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

// The browser never receives storage keys, multipart IDs or ETags. This server-owned manifest also
// makes a retried final request safe after a browser timeout or API restart.
public sealed class NotionAssetUpload
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public NotionPage Page { get; set; } = null!;
    public Guid UploadedBySub { get; set; }
    public string Key { get; set; } = "";
    public string StorageUploadId { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Kind { get; set; } = "";
    public long SizeBytes { get; set; }
    public int ChunkSizeBytes { get; set; }
    public string PartsJson { get; set; } = "[]";
    public string Status { get; set; } = "initializing";
    public bool AbortRequested { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public long Revision { get; set; }
}
