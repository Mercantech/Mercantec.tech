namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Private creation receipt survives deletion of either page, preventing replay resurrection.</summary>
public sealed class NotionMcpPageCreation
{
    public Guid Id { get; set; }
    public Guid OwnerSub { get; set; }
    public Guid ParentPageId { get; set; }
    public Guid PageId { get; set; }
    public string RequestHash { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
