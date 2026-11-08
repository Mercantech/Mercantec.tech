using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<NotionPage> NotionPages => Set<NotionPage>();
    public DbSet<NotionCollection> NotionCollections => Set<NotionCollection>();

    private static void ConfigureNotion(ModelBuilder model)
    {
        model.Entity<NotionCollection>(e =>
        {
            e.ToTable("notion_collections");
            e.HasKey(x => x.Id);
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotionPage>(e =>
        {
            e.ToTable("notion_pages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.HoldId, x.ParentPageId });
            e.HasIndex(x => x.ParentPageId);
            e.HasIndex(x => new { x.CollectionId, x.ParentPageId });
            e.Property(x => x.DraftFingerprint).HasMaxLength(64);
            e.Property(x => x.PublishedTitle).HasMaxLength(200);
            e.HasIndex(x => new { x.HoldId, x.PublishedAtUtc });
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Collection).WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<NotionPage>().WithMany().HasForeignKey(x => x.ParentPageId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
