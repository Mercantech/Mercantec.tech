using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<QuestAudienceMembership> QuestAudienceMemberships => Set<QuestAudienceMembership>();
    public DbSet<QuestAudienceGroup> QuestAudienceGroups => Set<QuestAudienceGroup>();
    private static void ConfigureQuestAudience(ModelBuilder model)
    {
        model.Entity<LearningQuest>(e =>
        {
            e.Property(x => x.AudienceMode).HasMaxLength(12).HasDefaultValue("hold").IsRequired();
            e.HasIndex(x => new { x.SeriesId, x.OccurrenceDate }).IsUnique();
        });
        model.Entity<QuestAudienceMembership>(e =>
        {
            e.ToTable("quest_audience_memberships"); e.HasKey(x => new { x.QuestId, x.MembershipId });
            e.HasOne(x => x.Quest).WithMany().HasForeignKey(x => x.QuestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<QuestAudienceGroup>(e =>
        {
            e.ToTable("quest_audience_groups"); e.HasKey(x => new { x.QuestId, x.GroupId });
            e.HasOne(x => x.Quest).WithMany().HasForeignKey(x => x.QuestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
