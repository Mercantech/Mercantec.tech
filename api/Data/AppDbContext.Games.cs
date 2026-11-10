using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<UploadedGame> UploadedGames => Set<UploadedGame>();

    private static void ConfigureUploadedGames(ModelBuilder model)
    {
        model.Entity<UploadedGame>(entity =>
        {
            entity.ToTable("uploaded_games");
            entity.HasKey(game => game.Id);
            entity.Property(game => game.Title).HasMaxLength(120).IsRequired();
            entity.Property(game => game.EntryPoint).HasMaxLength(512).IsRequired();
            entity.Property(game => game.ContentSha256).HasMaxLength(64).IsRequired();
            entity.HasIndex(game => new { game.OwnerSub, game.RequestId }).IsUnique();
            entity.HasIndex(game => game.FileAssetId).IsUnique();
            entity.HasOne(game => game.FileAsset).WithMany().HasForeignKey(game => game.FileAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
