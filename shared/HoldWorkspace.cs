namespace Mercantec.Tech.Contracts;

public sealed record HoldWorkspaceView(Guid CourseId, Guid HoldId, string Name, string? Description,
    bool CanManage, int MemberCount, int GroupCount, int MaterialCount, int PendingReviews,
    int QuestCount, int DraftQuestCount, WorkspacePage[] Pages, WorkspaceAgendaItem[] Agenda,
    WorkspaceReview[] Reviews, string[] LectioKeys, SubmissionProgress? Progress = null,
    WorkspaceCourse[]? Courses = null, int GroupSetCount = 0);

public sealed record WorkspaceCourse(Guid Id, string Title, string Description, DateOnly? StartsOn,
    DateOnly? EndsOn, bool Published);

public sealed record WorkspacePage(Guid Id, string Title, DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc, bool HasUnpublishedChanges);
public sealed record WorkspaceAgendaItem(Guid Id, string Title, DateTimeOffset StartUtc,
    string Kind, string? Location);
public sealed record WorkspaceReview(Guid QuestId, string Title, int Count);
