using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<LearningGroup> LearningGroups => Set<LearningGroup>();
    public DbSet<LearningGroupMember> LearningGroupMembers => Set<LearningGroupMember>();
    public DbSet<NotionAsset> NotionAssets => Set<NotionAsset>();

    private static void ConfigureNotionGroupsMedia(ModelBuilder model)
    {
        model.Entity<LearningGroup>(e =>
        {
            e.ToTable("learning_groups"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.HoldId);
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LearningGroupMember>(e =>
        {
            e.ToTable("learning_group_members"); e.HasKey(x => new { x.GroupId, x.MembershipId });
            e.HasOne(x => x.Group).WithMany(x => x.Members).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotionAsset>(e =>
        {
            e.ToTable("notion_assets"); e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(512).IsRequired(); e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.OriginalName).HasMaxLength(240).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.HasIndex(x => x.PageId);
            e.HasOne(x => x.Page).WithMany().HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
