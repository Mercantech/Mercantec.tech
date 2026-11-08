namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Private writing task. Its proposal state never belongs to the shared Yjs document.</summary>
public sealed class EditorAiRun
{
    public Guid Id { get; set; }
    public Guid OwnerSub { get; set; }
    public string OwnerRole { get; set; } = "Teacher";
    public Guid PageId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Instruction { get; set; } = "";
    public string Mode { get; set; } = "generate";
    public string Scope { get; set; } = "selection";
    public string Status { get; set; } = "queued";
    public string DocumentJson { get; set; } = "[]";
    public string TargetJson { get; set; } = "null";
    public string ExcludedSourceIdsJson { get; set; } = "[]";
    public string AdjustHunkIdsJson { get; set; } = "[]";
    public string SourcesJson { get; set; } = "[]";
    public string CoverageJson { get; set; } = "{}";
    public string? ContextCursorBlockId { get; set; }
    public string Progress { get; set; } = "Venter på AI…";
    public string? Error { get; set; }
    public string Model { get; set; } = "gpt-6.1-sol";
    public int OutputTokens { get; set; }
    public int AttemptTokens { get; set; }
    public int BatchCount { get; set; }
    public int AttemptBatchCount { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? AttemptStartedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public ICollection<EditorAiRunHunk> Hunks { get; set; } = [];
}

public sealed class EditorAiRunHunk
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public EditorAiRun Run { get; set; } = null!;
    public int Version { get; set; } = 1;
    public int Order { get; set; }
    public string Operation { get; set; } = "replace";
    public string TargetBlockIdsJson { get; set; } = "[]";
    public string BaseFingerprint { get; set; } = "";
    public string BeforeJson { get; set; } = "[]";
    public string AfterJson { get; set; } = "[]";
    public string TargetJson { get; set; } = "null";
    public string SourceIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "ready";
    public string Explanation { get; set; } = "";
    public string Signature { get; set; } = "";
}

public sealed class EditorAiRunEvent
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public string Type { get; set; } = "run";
    public string DataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
