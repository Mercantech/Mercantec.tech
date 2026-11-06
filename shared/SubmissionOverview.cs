namespace Mercantec.Tech.Contracts;

public sealed record SubmissionProgress(int Expected, int Delivered, int Missing, int Overdue,
    int PendingReview, int Approved, int ChangesRequested, int Drafts, int Ungrouped)
{
    public static SubmissionProgress Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public static SubmissionProgress Sum(IEnumerable<SubmissionProgress> items) => items.Aggregate(Empty,
        (a, b) => new(a.Expected + b.Expected, a.Delivered + b.Delivered, a.Missing + b.Missing,
            a.Overdue + b.Overdue, a.PendingReview + b.PendingReview, a.Approved + b.Approved,
            a.ChangesRequested + b.ChangesRequested, a.Drafts + b.Drafts, a.Ungrouped + b.Ungrouped));
}

public sealed record HoldSubmissionOverview(Guid HoldId, DateTimeOffset GeneratedAtUtc,
    SubmissionProgress RequiredProgress, QuestSubmissionOverview[] Quests);
public sealed record QuestSubmissionOverview(Guid QuestId, string Title, string Kind, string SubmissionMode,
    bool Published, bool Required, DateTimeOffset? DeadlineUtc, Guid? GroupSetId, string? GroupSetName,
    SubmissionProgress Progress, MemberSubmissionStatus[] Members, int ReviewTasks = 0,
    bool Upcoming = false, DateOnly? OccurrenceDate = null);
public sealed record MemberSubmissionStatus(Guid MembershipId, string DisplayName, string State,
    bool Overdue, bool SubmittedLate, bool Ungrouped, Guid? SubmissionId, Guid? GroupId,
    string? GroupName, DateTimeOffset? SubmittedAtUtc, int? RevisionNumber, bool IsCurrentRevision);
