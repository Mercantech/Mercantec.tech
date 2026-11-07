namespace Mercantec.Tech.Contracts;

public sealed record ActionCenterSnapshot(DateTimeOffset GeneratedAtUtc, ActionCenterItem[] Items);
public sealed record ActionCenterItem(string Kind, string Severity, Guid HoldId, Guid WorkspaceId,
    Guid? CourseId, Guid ResourceId, Guid? SubmissionId, string Title, string HoldName,
    DateTimeOffset? DeadlineUtc, int Count = 1, string? State = null);
