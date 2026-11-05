using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<HoldCalendarEvent> HoldCalendarEvents => Set<HoldCalendarEvent>();

    private static void ConfigureHoldCalendar(ModelBuilder model)
    {
        model.Entity<HoldCalendarEvent>(e =>
        {
            e.ToTable("hold_calendar_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Location).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.HoldId, x.StartUtc });
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
