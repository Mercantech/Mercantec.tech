namespace Mercantec.Tech.Api.Data.Entities;

// Only completed, validated browser-game uploads enter this table. A draft,
// arbitrary file or client-supplied URL is not proof of an uploaded game.
public sealed class UploadedGame
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid OwnerSub { get; set; }
    public Guid FileAssetId { get; set; }
    public FileAsset FileAsset { get; set; } = null!;
    public string Title { get; set; } = "";
    public string EntryPoint { get; set; } = "";
    public string ContentSha256 { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
