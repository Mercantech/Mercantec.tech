namespace Mercantec.Tech.Api.Data.Entities;

public sealed class FileAsset
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public Guid OwnerSub { get; set; }
    public Guid? HoldId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CourseMaterial
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public Guid FileAssetId { get; set; }
    public FileAsset FileAsset { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool Published { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class LearningQuest
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SubmissionMode { get; set; } = "individual";
    public string Kind { get; set; } = "main";
    public bool Published { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class LearningGroup
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public ICollection<LearningGroupMember> Members { get; set; } = new List<LearningGroupMember>();
}

public sealed class LearningGroupMember
{
    public Guid GroupId { get; set; }
    public LearningGroup Group { get; set; } = null!;
    public Guid MembershipId { get; set; }
    public Membership Membership { get; set; } = null!;
}

public sealed class QuestSubmission
{
    public Guid Id { get; set; }
    public Guid QuestId { get; set; }
    public LearningQuest Quest { get; set; } = null!;
    public Guid? GroupId { get; set; }
    public Guid CreatedBySub { get; set; }
    public string OwnerKey { get; set; } = "";
    public string State { get; set; } = "draft";
    public int Version { get; set; }
    public string DraftText { get; set; } = "";
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<SubmissionDraftFile> DraftFiles { get; set; } = new List<SubmissionDraftFile>();
    public ICollection<SubmissionRevision> Revisions { get; set; } = new List<SubmissionRevision>();
}

public sealed class SubmissionDraftFile
{
    public Guid SubmissionId { get; set; }
    public QuestSubmission Submission { get; set; } = null!;
    public Guid FileAssetId { get; set; }
    public FileAsset FileAsset { get; set; } = null!;
}

public sealed class SubmissionRevision
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public QuestSubmission Submission { get; set; } = null!;
    public int Number { get; set; }
    public string Text { get; set; } = "";
    public Guid SubmittedBySub { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public ICollection<SubmissionRevisionFile> Files { get; set; } = new List<SubmissionRevisionFile>();
    public ICollection<SubmissionParticipant> Participants { get; set; } = new List<SubmissionParticipant>();
    public ICollection<SubmissionFeedback> Feedback { get; set; } = new List<SubmissionFeedback>();
}

public sealed class SubmissionRevisionFile
{
    public Guid RevisionId { get; set; }
    public SubmissionRevision Revision { get; set; } = null!;
    public Guid FileAssetId { get; set; }
    public FileAsset FileAsset { get; set; } = null!;
}

public sealed class SubmissionParticipant
{
    public Guid RevisionId { get; set; }
    public SubmissionRevision Revision { get; set; } = null!;
    public Guid MembershipId { get; set; }
    public Guid? UserSub { get; set; }
    public string DisplayName { get; set; } = "";
}

public sealed class SubmissionFeedback
{
    public Guid Id { get; set; }
    public Guid RevisionId { get; set; }
    public SubmissionRevision Revision { get; set; } = null!;
    public Guid TeacherSub { get; set; }
    public Guid? FileAssetId { get; set; }
    public string Text { get; set; } = "";
    public string? Decision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ICollection<SubmissionFeedbackFile> Attachments { get; set; } = new List<SubmissionFeedbackFile>();
}

public sealed class SubmissionFeedbackFile
{
    public Guid FeedbackId { get; set; }
    public SubmissionFeedback Feedback { get; set; } = null!;
    public Guid FileAssetId { get; set; }
    public FileAsset FileAsset { get; set; } = null!;
}
