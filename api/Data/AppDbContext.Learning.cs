using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();
    public DbSet<LearningQuest> LearningQuests => Set<LearningQuest>();
    public DbSet<LearningGroup> LearningGroups => Set<LearningGroup>();
    public DbSet<LearningGroupMember> LearningGroupMembers => Set<LearningGroupMember>();
    public DbSet<QuestSubmission> QuestSubmissions => Set<QuestSubmission>();
    public DbSet<SubmissionRevision> SubmissionRevisions => Set<SubmissionRevision>();
    public DbSet<SubmissionParticipant> SubmissionParticipants => Set<SubmissionParticipant>();
    public DbSet<SubmissionFeedback> SubmissionFeedback => Set<SubmissionFeedback>();

    private static void ConfigureLearning(ModelBuilder model)
    {
        model.Entity<FileAsset>(e =>
        {
            e.ToTable("file_assets"); e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(512).IsRequired();
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.OwnerSub);
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CourseMaterial>(e =>
        {
            e.ToTable("course_materials"); e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FileAsset).WithMany().HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LearningQuest>(e =>
        {
            e.ToTable("learning_quests"); e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(20000).IsRequired();
            e.Property(x => x.SubmissionMode).HasMaxLength(16).IsRequired();
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LearningGroup>(e =>
        {
            e.ToTable("learning_groups"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LearningGroupMember>(e =>
        {
            e.ToTable("learning_group_members"); e.HasKey(x => new { x.GroupId, x.MembershipId });
            e.HasOne(x => x.Group).WithMany(x => x.Members).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<QuestSubmission>(e =>
        {
            e.ToTable("quest_submissions"); e.HasKey(x => x.Id);
            e.Property(x => x.OwnerKey).HasMaxLength(40).IsRequired();
            e.Property(x => x.State).HasMaxLength(24).IsRequired();
            e.Property(x => x.DraftText).HasMaxLength(20000).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.QuestId, x.OwnerKey }).IsUnique();
            e.HasOne(x => x.Quest).WithMany().HasForeignKey(x => x.QuestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LearningGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SubmissionDraftFile>(e =>
        {
            e.ToTable("submission_draft_files"); e.HasKey(x => new { x.SubmissionId, x.FileAssetId });
            e.HasOne(x => x.Submission).WithMany(x => x.DraftFiles).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FileAsset).WithMany().HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SubmissionRevision>(e =>
        {
            e.ToTable("submission_revisions"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SubmissionId, x.Number }).IsUnique();
            e.Property(x => x.Text).HasMaxLength(20000).IsRequired();
            e.HasOne(x => x.Submission).WithMany(x => x.Revisions).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<SubmissionRevisionFile>(e =>
        {
            e.ToTable("submission_revision_files"); e.HasKey(x => new { x.RevisionId, x.FileAssetId });
            e.HasOne(x => x.Revision).WithMany(x => x.Files).HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FileAsset).WithMany().HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SubmissionParticipant>(e =>
        {
            e.ToTable("submission_participants"); e.HasKey(x => new { x.RevisionId, x.MembershipId });
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.UserSub);
            e.HasOne(x => x.Revision).WithMany(x => x.Participants).HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<SubmissionFeedback>(e =>
        {
            e.ToTable("submission_feedback"); e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(10000).IsRequired();
            e.Property(x => x.Decision).HasMaxLength(24);
            e.HasOne(x => x.Revision).WithMany(x => x.Feedback).HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<FileAsset>().WithMany().HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SubmissionFeedbackFile>(e =>
        {
            e.ToTable("submission_feedback_files"); e.HasKey(x => new { x.FeedbackId, x.FileAssetId });
            e.HasOne(x => x.Feedback).WithMany(x => x.Attachments).HasForeignKey(x => x.FeedbackId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FileAsset).WithMany().HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
