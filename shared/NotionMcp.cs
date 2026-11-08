using System.Text.Json;

namespace Mercantec.Tech.Contracts;

public sealed record NotionMcpChangeSummary(Guid OperationId, Guid PageId, string Summary, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? AppliedAtUtc, DateTimeOffset? UndoneAtUtc, Guid? UndoOf,
    bool CanUndo);
public sealed record NotionMcpChangeView(Guid OperationId, Guid PageId, string Summary, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? AppliedAtUtc, DateTimeOffset? UndoneAtUtc, Guid? UndoOf,
    bool CanUndo, JsonElement Before, JsonElement After, string? Error = null);
public sealed record UndoNotionMcpChange(Guid ReversalId);
public sealed record ApplyNotionMcpChanges(Guid PageId, Guid OperationId, JsonElement[] Changes, string? Summary = null);
public sealed record PrepareNotionMcpChange(Guid OperationId, Guid OwnerSub, string RequestHash,
    JsonElement Before, JsonElement After, string? Summary = null, Guid? UndoOf = null);
public sealed record StoreNotionMcpChange(Guid OperationId, Guid OwnerSub, string RequestHash);
