using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<NotionAsset> NotionAssets => Set<NotionAsset>();

    private static void ConfigureNotionGroupsMedia(ModelBuilder model)
    {
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
