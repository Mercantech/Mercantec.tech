using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<EntityReferenceLink> EntityReferenceLinks => Set<EntityReferenceLink>();
    public DbSet<ReferenceIndexState> ReferenceIndexStates => Set<ReferenceIndexState>();
    public DbSet<ReferenceMentionEvent> ReferenceMentionEvents => Set<ReferenceMentionEvent>();

    private static void ConfigureReferences(ModelBuilder model)
    {
        model.Entity<EntityReferenceLink>(e =>
        {
            e.ToTable("entity_reference_links");
            e.HasKey(x => new { x.SourceKind, x.SourceId, x.Visibility, x.TargetKind, x.TargetId });
            e.Property(x => x.SourceKind).HasMaxLength(32);
            e.Property(x => x.SourceId).HasMaxLength(300);
            e.Property(x => x.Visibility).HasMaxLength(16);
            e.Property(x => x.TargetKind).HasMaxLength(32);
            e.Property(x => x.TargetId).HasMaxLength(300);
            e.Property(x => x.IsMention).HasDefaultValue(false);
            e.HasIndex(x => new { x.TargetKind, x.TargetId });
        });
        model.Entity<ReferenceIndexState>(e =>
        {
            e.ToTable("reference_index_states");
            e.HasKey(x => new { x.SourceKind, x.SourceId, x.Visibility });
            e.Property(x => x.SourceKind).HasMaxLength(32);
            e.Property(x => x.SourceId).HasMaxLength(300);
            e.Property(x => x.Visibility).HasMaxLength(16);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
        });
        model.Entity<ReferenceMentionEvent>(e =>
        {
            e.ToTable("reference_mention_events"); e.HasKey(x => x.Id);
            e.Property(x => x.SourceKind).HasMaxLength(32);
            e.Property(x => x.SourceId).HasMaxLength(300);
            e.Property(x => x.Visibility).HasMaxLength(16);
            e.HasIndex(x => new { x.RecipientSub, x.CreatedAtUtc });
        });
    }
}
