using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<ExtensionUploadKey> ExtensionUploadKeys => Set<ExtensionUploadKey>();

    private static void ConfigureExtensionUploads(ModelBuilder model)
    {
        model.Entity<ExtensionUploadKey>(e =>
        {
            e.ToTable("extension_upload_keys");
            e.HasKey(x => x.Id);
            e.Property(x => x.OwnerEmail).HasMaxLength(254).IsRequired();
            e.Property(x => x.Label).HasMaxLength(80).IsRequired();
            e.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => x.OwnerSub);
        });
    }
}
