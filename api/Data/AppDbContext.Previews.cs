using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<FilePreview> FilePreviews => Set<FilePreview>();

    private static void ConfigurePreviews(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FilePreview>(e =>
        {
            e.ToTable("file_previews");
            e.HasKey(p => p.FileId);
            e.Property(p => p.State).HasMaxLength(20).IsRequired();
            e.Property(p => p.StorageKey).HasMaxLength(1024);
            e.Property(p => p.ContentType).HasMaxLength(200);
            e.Property(p => p.Error).HasMaxLength(200);
            e.HasOne(p => p.File).WithOne().HasForeignKey<FilePreview>(p => p.FileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
