namespace Mercantec.Tech.Api.Data.Entities;

public sealed class ExtensionUploadKey
{
    public Guid Id { get; set; }
    public Guid OwnerSub { get; set; }
    public string OwnerEmail { get; set; } = "";
    public string Label { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
