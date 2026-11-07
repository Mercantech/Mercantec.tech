using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<CustomEmoji> CustomEmojis => Set<CustomEmoji>();

    private static void ConfigureEmojis(ModelBuilder model)
    {
        model.Entity<CustomEmoji>(e =>
        {
            e.ToTable("custom_emojis");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(32).IsRequired();
            e.Property(x => x.AltText).HasMaxLength(240).HasDefaultValue("").IsRequired();
            e.Property(x => x.Category).HasMaxLength(32).HasDefaultValue("custom").IsRequired();
            e.Property(x => x.KeywordsJson).HasMaxLength(4096).HasDefaultValue("[]").IsRequired();
            e.Property(x => x.Key).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}
