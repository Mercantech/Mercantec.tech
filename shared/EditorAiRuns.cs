using System.Text.Json;

namespace Mercantec.Tech.Contracts;

public sealed record EditorAiRunRequest(Guid PageId, string Instruction, string Mode, string Scope,
    JsonElement Document, JsonElement? Target = null, string[]? ExcludedSourceIds = null, Guid? ClientRequestId = null);
public sealed record EditorAiAdjustRequest(string Instruction, JsonElement? Document = null,
    JsonElement? Target = null, Guid[]? HunkIds = null, string[]? ExcludedSourceIds = null);
public sealed record EditorAiContinueRequest(JsonElement? Document = null, JsonElement? Target = null);
public sealed record EditorAiReviewRequest(string Status, int Version);
public sealed record EditorAiSource(string Id, string Kind, string SourceId, string Title, string Href,
    string? Excerpt = null, string? Location = null, string State = "ready", string? Fingerprint = null);
public sealed record EditorAiCoverage(int ActiveBlocks, int TotalBlocks, int ActiveTokens,
    int ExternalTokens, bool Partial, string? Detail = null);
public sealed record EditorAiHunkView(Guid Id, int Version, int Order, string Operation, string[] TargetBlockIds,
    string BaseFingerprint, JsonElement[] Before, JsonElement[] After, string Status,
    string Explanation, string[] SourceIds, JsonElement? Target = null);
public sealed record EditorAiRunView(Guid Id, Guid PageId, string Instruction, string Mode, string Scope,
    string Status, EditorAiHunkView[] Hunks, EditorAiSource[] Sources, EditorAiCoverage Coverage,
    string Progress, string? Error, string Model, long EventCursor, int OutputTokens,
    DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
