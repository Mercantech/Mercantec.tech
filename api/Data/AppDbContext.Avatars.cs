using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<AvatarProfile> AvatarProfiles => Set<AvatarProfile>();
    public DbSet<AvatarOwnership> AvatarOwnerships => Set<AvatarOwnership>();
    public DbSet<AvatarItemOwnership> AvatarItemOwnerships => Set<AvatarItemOwnership>();

    private static void ConfigureAvatarProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AvatarProfile>(entity =>
        {
            entity.ToTable("avatar_profiles", table =>
            {
                table.HasCheckConstraint("CK_avatar_profiles_gold", "\"Gold\" >= 0");
                table.HasCheckConstraint("CK_avatar_profiles_level", "\"Level\" >= 1");
                table.HasCheckConstraint("CK_avatar_profiles_revision", "\"Revision\" >= 0");
            });
            entity.HasKey(profile => profile.UserSub);
            entity.Property(profile => profile.SelectedAvatarId).HasMaxLength(64).IsRequired();
            entity.Property(profile => profile.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<AvatarOwnership>(entity =>
        {
            entity.ToTable("avatar_ownerships");
            entity.HasKey(ownership => new { ownership.UserSub, ownership.AvatarId });
            entity.Property(ownership => ownership.AvatarId).HasMaxLength(64).IsRequired();
            entity.HasOne(ownership => ownership.Profile).WithMany(profile => profile.Avatars)
                .HasForeignKey(ownership => ownership.UserSub).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AvatarItemOwnership>(entity =>
        {
            entity.ToTable("avatar_item_ownerships");
            entity.HasKey(ownership => new { ownership.UserSub, ownership.ItemId });
            entity.Property(ownership => ownership.ItemId).HasMaxLength(64).IsRequired();
            entity.HasOne(ownership => ownership.Profile).WithMany(profile => profile.Items)
                .HasForeignKey(ownership => ownership.UserSub).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
