using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<NotionMcpChange> NotionMcpChanges => Set<NotionMcpChange>();
    private static void ConfigureNotionMcp(ModelBuilder model)
    {
        model.Entity<NotionMcpChange>(e =>
        {
            e.ToTable("notion_mcp_changes");
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Error).HasMaxLength(120);
            e.HasIndex(x => new { x.OwnerSub, x.PageId, x.CreatedAtUtc });
            e.HasIndex(x => x.UndoOf).IsUnique();
            e.HasOne<NotionPage>().WithMany().HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
