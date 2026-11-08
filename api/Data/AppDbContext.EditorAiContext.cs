using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<EditorAiTextChunk> EditorAiTextChunks => Set<EditorAiTextChunk>();
    public DbSet<EditorAiFileText> EditorAiFileTexts => Set<EditorAiFileText>();

    private static void ConfigureEditorAiContext(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EditorAiTextChunk>(e =>
        {
            e.ToTable("editor_ai_text_chunks"); e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            e.Property(x => x.SourceId).HasMaxLength(300).IsRequired();
            e.Property(x => x.Fingerprint).HasMaxLength(100).IsRequired();
            e.Property(x => x.Title).HasMaxLength(400).IsRequired();
            e.Property(x => x.Href).HasMaxLength(1500).IsRequired();
            e.Property(x => x.Location).HasMaxLength(400);
            e.Property(x => x.Text).IsRequired();
            e.HasIndex(x => new { x.Kind, x.SourceId, x.Ordinal }).IsUnique();
            e.HasIndex(x => new { x.EntityId, x.Fingerprint });
            e.HasIndex(x => x.HoldId);
        });
        modelBuilder.Entity<EditorAiFileText>(e =>
        {
            e.ToTable("editor_ai_file_texts"); e.HasKey(x => x.FileId);
            e.Property(x => x.Fingerprint).HasMaxLength(100).IsRequired();
            e.Property(x => x.State).HasMaxLength(24).IsRequired();
            e.Property(x => x.Text).IsRequired();
            e.Property(x => x.Error).HasMaxLength(400);
            e.HasOne(x => x.File).WithOne().HasForeignKey<EditorAiFileText>(x => x.FileId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.State, x.UpdatedAtUtc });
        });
    }
}
