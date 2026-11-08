namespace Mercantec.Tech.Api.Data.Entities;

public sealed class ActionReadReceipt
{
    public Guid UserSub { get; set; }
    public string Kind { get; set; } = "";
    public Guid ResourceId { get; set; }
    public DateTimeOffset ReadAtUtc { get; set; }
}
