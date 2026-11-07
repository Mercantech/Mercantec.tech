using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Notebooks;

public static class NotebookDefaults
{
    public static readonly Guid TemplateId = new("da654be2-5d50-4e61-9bd6-b643b33c3330");
    public static readonly Guid VersionId = new("da654be2-5d50-4e61-9bd6-b643b33c3331");
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.NotebookTemplates.AnyAsync(t => t.Id == TemplateId, ct)) return;
        using var source = typeof(NotebookDefaults).Assembly.GetManifestResourceStream("Mercantec.Tech.Api.DefaultNotebookTemplate.json")!;
        var seed = (await JsonSerializer.DeserializeAsync<StoreNotebookDocument>(source,
            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct))!;
        if (!NotebookDocumentValidation.ValidProjection(seed, out var state)) throw new InvalidDataException("Invalid notebook template seed.");
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.NotebookDocuments.Add(new NotebookDocument { Id = TemplateId, Kind = "template", Title = "Dagens refleksion",
                YjsState = state, Fingerprint = seed.Fingerprint, Revision = 1, SearchText = seed.PlainText,
                CreatedAtUtc = now, UpdatedAtUtc = now });
            var template = new NotebookTemplate { Id = TemplateId, Cadence = "daily", Version = 1 };
            db.NotebookTemplates.Add(template);
            await db.SaveChangesAsync(ct);
            db.NotebookTemplateVersions.Add(new NotebookTemplateVersion { Id = VersionId, TemplateId = TemplateId,
                Number = 1, Title = "Dagens refleksion", Cadence = "daily", State = state, CreatedAtUtc = now });
            await db.SaveChangesAsync(ct);
            template.ActiveVersionId = VersionId; template.IsActive = true;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (NotebookPeriods.IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            if (!await db.NotebookTemplates.AnyAsync(t => t.Id == TemplateId, ct)) throw;
        }
    }
}
