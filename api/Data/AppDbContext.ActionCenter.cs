using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<ActionReadReceipt> ActionReadReceipts => Set<ActionReadReceipt>();
    private static void ConfigureActionCenter(ModelBuilder model)
    {
        model.Entity<ActionReadReceipt>(e =>
        {
            e.ToTable("action_read_receipts");
            e.HasKey(r => new { r.UserSub, r.Kind, r.ResourceId });
            e.Property(r => r.Kind).HasMaxLength(32).IsRequired();
        });
    }
}
