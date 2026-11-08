using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<EditorAiRun> EditorAiRuns => Set<EditorAiRun>();
    public DbSet<EditorAiRunHunk> EditorAiRunHunks => Set<EditorAiRunHunk>();
    public DbSet<EditorAiRunEvent> EditorAiRunEvents => Set<EditorAiRunEvent>();

    private static void ConfigureEditorAi(ModelBuilder model)
    {
        model.Entity<EditorAiRun>(e =>
        {
            e.ToTable("editor_ai_runs"); e.HasKey(x => x.Id);
            e.Property(x => x.Instruction).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsConcurrencyToken();
            e.Property(x => x.LeaseId).IsConcurrencyToken();
            e.Property(x => x.Mode).HasMaxLength(16); e.Property(x => x.Scope).HasMaxLength(16);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.OwnerRole).HasMaxLength(20);
            e.Property(x => x.ContextCursorBlockId).HasMaxLength(200);
            e.HasIndex(x => new { x.OwnerSub, x.ClientRequestId }).IsUnique();
            e.HasIndex(x => new { x.OwnerSub, x.PageId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            e.HasIndex(x => x.ExpiresAtUtc);
            e.HasIndex(x => new { x.OwnerSub, x.PageId }).IsUnique()
                .HasFilter("\"Status\" IN ('queued','gathering','generating')");
            e.HasOne<NotionPage>().WithMany().HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<EditorAiRunHunk>(e =>
        {
            e.ToTable("editor_ai_run_hunks"); e.HasKey(x => x.Id);
            e.Property(x => x.BaseFingerprint).HasMaxLength(64);
            e.Property(x => x.Signature).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(20).IsConcurrencyToken();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.Operation).HasMaxLength(24);
            e.HasIndex(x => new { x.RunId, x.Order });
            e.HasOne(x => x.Run).WithMany(x => x.Hunks).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<EditorAiRunEvent>(e =>
        {
            e.ToTable("editor_ai_run_events"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Type).HasMaxLength(20);
            e.HasIndex(x => new { x.RunId, x.Id });
            e.HasOne<EditorAiRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
