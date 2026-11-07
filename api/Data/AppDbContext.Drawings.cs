using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<Drawing> Drawings => Set<Drawing>();
    public DbSet<DrawingGrant> DrawingGrants => Set<DrawingGrant>();
    public DbSet<DrawingAsset> DrawingAssets => Set<DrawingAsset>();
    public DbSet<DrawingSnapshot> DrawingSnapshots => Set<DrawingSnapshot>();

    private static void ConfigureDrawings(ModelBuilder model)
    {
        model.Entity<Drawing>(e =>
        {
            e.ToTable("drawings", table => table.HasCheckConstraint("CK_drawings_parent", "(\"NotionPageId\" IS NOT NULL AND \"NotebookDocumentId\" IS NULL) OR (\"NotionPageId\" IS NULL AND \"NotebookDocumentId\" IS NOT NULL)"));
            e.HasKey(x => x.Id);
            e.Property(x => x.BlockId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Mode).HasMaxLength(16).IsRequired();
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.PreviewFingerprint).HasMaxLength(64);
            e.Property(x => x.SceneJson).IsRequired(); e.Property(x => x.AssetIdsJson).IsRequired();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.Property(x => x.AccessVersion).IsConcurrencyToken();
            e.HasOne<NotionPage>().WithMany().HasForeignKey(x => x.NotionPageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<NotebookDocument>().WithMany().HasForeignKey(x => x.NotebookDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.NotionPageId, x.BlockId }).IsUnique().HasFilter("\"NotionPageId\" IS NOT NULL");
            e.HasIndex(x => new { x.NotebookDocumentId, x.BlockId }).IsUnique().HasFilter("\"NotebookDocumentId\" IS NOT NULL");
        });
        model.Entity<DrawingGrant>(e =>
        {
            e.ToTable("drawing_grants", table => table.HasCheckConstraint("CK_drawing_grants_principal", "(\"UserSub\" IS NOT NULL AND \"HoldId\" IS NULL) OR (\"UserSub\" IS NULL AND \"HoldId\" IS NOT NULL)"));
            e.HasKey(x => x.Id); e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.HasOne<Drawing>().WithMany().HasForeignKey(x => x.DrawingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DrawingId, x.UserSub }).IsUnique().HasFilter("\"UserSub\" IS NOT NULL");
            e.HasIndex(x => new { x.DrawingId, x.HoldId }).IsUnique().HasFilter("\"HoldId\" IS NOT NULL");
        });
        model.Entity<DrawingAsset>(e =>
        {
            e.ToTable("drawing_assets"); e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(512).IsRequired();
            e.Property(x => x.OriginalName).HasMaxLength(240).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(24).IsRequired();
            e.HasIndex(x => x.DrawingId);
            e.HasOne<Drawing>().WithMany().HasForeignKey(x => x.DrawingId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<DrawingSnapshot>(e =>
        {
            e.ToTable("drawing_snapshots"); e.HasKey(x => x.Id);
            e.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.SceneJson).IsRequired(); e.Property(x => x.AssetIdsJson).IsRequired();
            e.HasIndex(x => new { x.DrawingId, x.Revision }).IsUnique();
            e.HasOne<Drawing>().WithMany().HasForeignKey(x => x.DrawingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DrawingAsset>().WithMany().HasForeignKey(x => x.PreviewLightAssetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DrawingAsset>().WithMany().HasForeignKey(x => x.PreviewDarkAssetId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
