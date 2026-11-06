using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<LearningGroupSet> LearningGroupSets => Set<LearningGroupSet>();

    private static void ConfigureGroupSets(ModelBuilder model)
    {
        model.Entity<LearningGroupSet>(e =>
        {
            e.ToTable("learning_group_sets"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.HoldId, x.Archived });
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LearningGroup>().HasOne<LearningGroupSet>().WithMany()
            .HasForeignKey(x => x.GroupSetId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LearningGroup>().Property(x => x.BatchFingerprint).HasMaxLength(64);
        model.Entity<LearningGroup>().HasIndex(x => new { x.HoldId, x.BatchRequestId, x.BatchOrdinal }).IsUnique();
        model.Entity<LearningQuest>().HasOne<LearningGroupSet>().WithMany()
            .HasForeignKey(x => x.GroupSetId).OnDelete(DeleteBehavior.Restrict);
    }
}
