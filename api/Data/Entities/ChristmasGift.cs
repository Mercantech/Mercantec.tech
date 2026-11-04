namespace Mercantec.Tech.Api.Data.Entities;

public sealed class ChristmasGift
{
    public Guid Id { get; set; }
    public Guid SenderSub { get; set; }
    public Guid RecipientSub { get; set; }
    public string SenderName { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string Kind { get; set; } = "cheer";
    public string Wrapping { get; set; } = "berry";
    public string Message { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
}
