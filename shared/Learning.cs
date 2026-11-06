namespace Mercantec.Tech.Contracts;

public sealed record LearningHold(Guid Id, string Name, bool CanManage);
public sealed record LearningMember(Guid Id, Guid? UserSub, string DisplayName);
public sealed record QuestView(Guid Id, Guid HoldId, string Title, string Description, string SubmissionMode,
    bool Published, DateTimeOffset? DeadlineUtc, bool CanManage, string Kind = "main", Guid? GroupSetId = null, bool Required = true,
    QuestAudienceSettings? Audience = null, Guid? SeriesId = null, DateOnly? OccurrenceDate = null, Guid[]? AllowedGroupIds = null);
public sealed record SaveQuest(Guid HoldId, string Title, string Description, string SubmissionMode,
    bool Published, DateTimeOffset? DeadlineUtc, string Kind = "main", Guid? GroupSetId = null, bool Required = true,
    QuestAudienceSettings? Audience = null);
public sealed record QuestAudienceSettings(string Mode = "hold", Guid[]? MembershipIds = null, Guid[]? GroupIds = null);
public sealed record QuestRepeatPlan(DateOnly StartsOn, DateOnly EndsOn, string Frequency, TimeOnly DeadlineLocal);
public sealed record CreateQuestSeries(SaveQuest Quest, QuestRepeatPlan Repeat, Guid? SeriesId = null);
public sealed record QuestSeriesView(Guid SeriesId, QuestView[] Quests);
public sealed record LearningGroupView(Guid Id, Guid HoldId, string Name, int Version, LearningMember[] Members, Guid? GroupSetId = null);
public sealed record SaveLearningGroup(Guid HoldId, string Name, Guid[] MembershipIds, int Version = 0, Guid? GroupSetId = null);
public sealed record SubmissionFileView(Guid Id, string OriginalName, string ContentType, long SizeBytes);
public sealed record SubmissionParticipantView(Guid MembershipId, Guid? UserSub, string DisplayName);
public sealed record SubmissionFeedbackView(Guid Id, Guid TeacherSub, Guid? FileAssetId, string Text,
    string? Decision, DateTimeOffset CreatedAtUtc, SubmissionFileView[] Attachments);
public sealed record SubmissionRevisionView(Guid Id, int Number, string Text, Guid SubmittedBySub,
    DateTimeOffset SubmittedAtUtc, SubmissionFileView[] Files, SubmissionParticipantView[] Participants,
    SubmissionFeedbackView[] Feedback);
public sealed record SubmissionView(Guid Id, Guid QuestId, Guid? GroupId, string State, int Version,
    string DraftText, SubmissionFileView[] DraftFiles, SubmissionRevisionView[] Revisions, bool CanEdit, bool CanReview);
public sealed record StartSubmission(Guid QuestId, Guid? GroupId);
public sealed record SaveSubmissionDraft(int Version, string Text, Guid[] FileIds);
public sealed record SubmitSubmission(int Version);
public sealed record ReviewSubmission(Guid RevisionId, int Version, string Text, Guid? FileAssetId,
    string? Decision, Guid[] AttachmentIds);
