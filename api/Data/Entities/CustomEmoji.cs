namespace Mercantec.Tech.Api.Data.Entities;

public sealed class CustomEmoji
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string AltText { get; set; } = "";
    public string Category { get; set; } = "custom";
    public string KeywordsJson { get; set; } = "[]";
    public string Key { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public Guid CreatedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
