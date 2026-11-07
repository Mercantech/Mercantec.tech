using System.ComponentModel.DataAnnotations.Schema;

namespace Mercantec.Tech.Api.Data.Entities;

public sealed class Drawing
{
    public Guid Id { get; set; }
    public Guid? NotionPageId { get; set; }
    public Guid? NotebookDocumentId { get; set; }
    public string BlockId { get; set; } = "";
    public string Mode { get; set; } = "snapshot";
    public byte[]? YjsState { get; set; }
    public string SceneJson { get; set; } = "{\"elements\":[],\"files\":{},\"appState\":{\"viewBackgroundColor\":\"#ffffff\"}}";
    public string? Fingerprint { get; set; }
    public string AssetIdsJson { get; set; } = "[]";
    public long Revision { get; set; }
    public long AccessVersion { get; set; } = 1;
    public Guid? PreviewLightAssetId { get; set; }
    public Guid? PreviewDarkAssetId { get; set; }
    public string? PreviewFingerprint { get; set; }
    public long? PreviewRevision { get; set; }
    public Guid CreatedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    [NotMapped] public string Domain => NotionPageId is null ? "notebook" : "notion";
    [NotMapped] public Guid DocumentId => (NotionPageId ?? NotebookDocumentId)!.Value;
}

public sealed class DrawingGrant
{
    public Guid Id { get; set; }
    public Guid DrawingId { get; set; }
    public Guid? UserSub { get; set; }
    public Guid? HoldId { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class DrawingAsset
{
    public Guid Id { get; set; }
    public Guid DrawingId { get; set; }
    public string Key { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "image/png";
    public string Kind { get; set; } = "image";
    public long SizeBytes { get; set; }
    public Guid UploadedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class DrawingSnapshot
{
    public Guid Id { get; set; }
    public Guid DrawingId { get; set; }
    public long Revision { get; set; }
    public string Fingerprint { get; set; } = "";
    public byte[] YjsState { get; set; } = [];
    public string SceneJson { get; set; } = "";
    public string AssetIdsJson { get; set; } = "[]";
    public Guid PreviewLightAssetId { get; set; }
    public Guid PreviewDarkAssetId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
