namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Private, durable operation intent and audit. Document receipts contain no audit bodies.</summary>
public sealed class NotionMcpChange
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public Guid OwnerSub { get; set; }
    public string RequestHash { get; set; } = "";
    public string RequestJson { get; set; } = "{}";
    public string Summary { get; set; } = "Codex-ændring";
    public string Status { get; set; } = "pending";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? Error { get; set; }
    public Guid? UndoOf { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AppliedAtUtc { get; set; }
    public DateTimeOffset? UndoneAtUtc { get; set; }
}
