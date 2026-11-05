namespace Mercantec.Tech.Api.Data.Entities;

public sealed class CustomEmoji
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public Guid CreatedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
