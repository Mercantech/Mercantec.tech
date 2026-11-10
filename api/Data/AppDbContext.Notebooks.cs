using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<Notebook> Notebooks => Set<Notebook>();
    public DbSet<NotebookProfile> NotebookProfiles => Set<NotebookProfile>();
    public DbSet<NotebookDocument> NotebookDocuments => Set<NotebookDocument>();
    public DbSet<NotebookShare> NotebookShares => Set<NotebookShare>();
    public DbSet<NotebookComment> NotebookComments => Set<NotebookComment>();
    public DbSet<NotebookLink> NotebookLinks => Set<NotebookLink>();
    public DbSet<NotebookTemplate> NotebookTemplates => Set<NotebookTemplate>();
    public DbSet<NotebookTemplateVersion> NotebookTemplateVersions => Set<NotebookTemplateVersion>();
    public DbSet<NotebookAsset> NotebookAssets => Set<NotebookAsset>();

    private static void ConfigureNotebooks(ModelBuilder model)
    {
        model.Entity<Notebook>(e =>
        {
            e.ToTable("notebooks"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.OwnerSub).IsUnique();
        });
        model.Entity<NotebookProfile>(e =>
        {
            e.ToTable("notebook_profiles"); e.HasKey(x => x.Sub);
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        });
        model.Entity<NotebookDocument>(e =>
        {
            e.ToTable("notebook_documents"); e.HasKey(x => x.Id);
            e.HasQueryFilter(x => x.DeletedAtUtc == null);
            e.HasIndex(x => x.TrashRootId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.LastEditedByName).HasMaxLength(200);
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.PeriodKey).HasMaxLength(80);
            e.Property(x => x.TagsJson).IsRequired();
            e.Property(x => x.SearchText).IsRequired();
            e.Property(x => x.AssetIdsJson).IsRequired();
            e.HasOne(x => x.Notebook).WithMany().HasForeignKey(x => x.NotebookId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<NotebookDocument>().WithMany().HasForeignKey(x => x.ParentPageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.NotebookId, x.ParentPageId });
            e.HasIndex(x => new { x.NotebookId, x.TemplateId, x.PeriodKey }).IsUnique()
                .HasFilter("\"PeriodKey\" IS NOT NULL");
            e.HasIndex(x => new { x.NotebookId, x.RequestId }).IsUnique()
                .HasFilter("\"RequestId\" IS NOT NULL");
        });
        model.Entity<NotebookShare>(e =>
        {
            e.ToTable("notebook_shares"); e.HasKey(x => x.Id);
            e.HasOne<Notebook>().WithMany().HasForeignKey(x => x.NotebookId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<NotebookDocument>().WithMany().HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.NotebookId, x.RecipientSub, x.PageId }).IsUnique()
                .HasFilter("\"PageId\" IS NOT NULL");
            e.HasIndex(x => new { x.NotebookId, x.RecipientSub }).IsUnique()
                .HasFilter("\"PageId\" IS NULL");
            e.HasIndex(x => x.RecipientSub);
        });
        model.Entity<NotebookComment>(e =>
        {
            e.ToTable("notebook_comments"); e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            e.HasOne<NotebookDocument>().WithMany().HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.PageId);
        });
        model.Entity<NotebookLink>(e =>
        {
            e.ToTable("notebook_links"); e.HasKey(x => new { x.FromId, x.ToId });
            e.HasOne<NotebookDocument>().WithMany().HasForeignKey(x => x.FromId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<NotebookDocument>().WithMany().HasForeignKey(x => x.ToId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotebookTemplate>(e =>
        {
            e.ToTable("notebook_templates"); e.HasKey(x => x.Id);
            e.Property(x => x.Cadence).HasMaxLength(16).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<NotebookDocument>().WithOne().HasForeignKey<NotebookTemplate>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<NotebookTemplateVersion>().WithMany().HasForeignKey(x => x.ActiveVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotebookTemplateVersion>(e =>
        {
            e.ToTable("notebook_template_versions"); e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Cadence).HasMaxLength(16).IsRequired();
            e.Property(x => x.State).IsRequired();
            e.Property(x => x.AssetIdsJson).IsRequired();
            e.HasOne<NotebookTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TemplateId, x.Number }).IsUnique();
        });
        model.Entity<NotebookAsset>(e =>
        {
            e.ToTable("notebook_assets"); e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(512).IsRequired();
            e.Property(x => x.OriginalName).HasMaxLength(240).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.HasIndex(x => x.Key);
            e.HasOne<NotebookDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
