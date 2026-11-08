using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Web.Courses;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Public;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

/// <summary>Indexes teaching documents only. A call reads at most one selected CRDT body.</summary>
public sealed class EditorAiContextIndexer(AppDbContext db, NotionAiProjectionClient documents)
{
    public async Task IndexNotionAsync(Guid id, CancellationToken ct)
    {
        var page = await db.NotionPages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (page is null) return;
        var fingerprint = EditorAiText.Fingerprint($"notion-v1:{page.DraftRevision}:{page.DraftFingerprint}:{page.Title}:{page.UpdatedAtUtc:O}");
        if (await db.EditorAiTextChunks.AnyAsync(x => x.Kind == "notion-page" && x.EntityId == id && x.Fingerprint == fingerprint, ct)) return;
        var projection = page.YjsState is { Length: > 0 } ? await documents.ProjectAsync(page.YjsState, ct) : new NotionAiProjection("");
        var blocks = projection.Blocks ?? [];
        var content = blocks.Length > 0
            ? blocks.SelectMany(x => EditorAiText.Chunks(x.Text, x.Heading ?? x.Id)).Select(x => (x.Text, (string?)x.Location)).ToArray()
            : EditorAiText.Chunks(projection.PlainText).Select(x => (x.Text, (string?)x.Location)).ToArray();
        await ReplaceAsync("notion-page", id.ToString(), id, page.HoldId, page.CollectionId, page.Title,
            $"/notion/{id}", fingerprint, content, ct, page.UpdatedAtUtc);
    }

    public async Task IndexCatalogAsync(CancellationToken ct)
    {
        var courses = CourseTemplateCatalog.All;
        var lessons = PublicCatalog.Subjects.SelectMany(s => PublicCatalog.Lessons(s).Select(l => new { Subject = s, Lesson = l })).ToArray();
        var version = EditorAiText.Fingerprint(JsonSerializer.Serialize(new { courses, lessons }));
        // One cheap version lookup per run, rather than an N+1 pass over unchanged public content.
        if (await db.EditorAiTextChunks.AnyAsync(x => x.Kind == "catalog-version" && x.SourceId == "teaching-v1" && x.Fingerprint == version, ct)) return;
        foreach (var entry in lessons)
        {
            var lesson = entry.Lesson;
            await IndexTextAsync("lesson", $"{entry.Subject.Id}/{lesson.Id}", null, null, null, lesson.Name,
                $"{entry.Subject.Path}/{lesson.Id}", $"{lesson.Name}\n{lesson.Lead}\n{lesson.Body}\nMål: {lesson.Goal}", ct);
        }
        foreach (var course in courses)
            await IndexTextAsync("learning-plan", course.Slug, null, null, null, course.Title, course.Path,
                $"{course.Title}\n{course.Lead}\n{course.Project}\n{string.Join('\n', course.Goals)}\n{course.Holistic}\n{course.Practice}\n{course.Evaluation}\n"
                + string.Join('\n', (course.Theory ?? []).Select(t => t.Title + "\n" + t.Summary + "\n" + string.Join('\n', t.Points))), ct);
        await ReplaceAsync("catalog-version", "teaching-v1", null, null, null, "Teaching catalogue version", "", version, [("", null)], ct);
    }

    public Task IndexTextAsync(string kind, string sourceId, Guid? entityId, Guid? holdId, Guid? collectionId,
        string title, string href, string text, CancellationToken ct) => ReplaceAsync(kind, sourceId, entityId, holdId,
            collectionId, title, href, EditorAiText.Fingerprint(text), EditorAiText.Chunks(text).Select(x => (x.Text, (string?)x.Location)).ToArray(), ct);

    private async Task ReplaceAsync(string kind, string sourceId, Guid? entityId, Guid? holdId, Guid? collectionId,
        string title, string href, string fingerprint, (string Text, string? Location)[] chunks, CancellationToken ct,
        DateTimeOffset? sourceUpdatedAt = null)
    {
        if (await db.EditorAiTextChunks.AnyAsync(x => x.Kind == kind && x.SourceId == sourceId && x.Fingerprint == fingerprint, ct)) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Build requests and the index worker can select the same page. Serialize only that source.
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({kind + ":" + sourceId}))", ct);
        if (await db.EditorAiTextChunks.AnyAsync(x => x.Kind == kind && x.SourceId == sourceId && x.Fingerprint == fingerprint, ct)) return;
        await db.EditorAiTextChunks.Where(x => x.Kind == kind && x.SourceId == sourceId).ExecuteDeleteAsync(ct);
        if (chunks.Length == 0) chunks = [("", null)]; // A known empty page must not be decoded on every poll.
        // Preserve the exact source revision time, rather than the later indexing time. A page edited
        // while Node projects old bytes must still be selected by the next background pass.
        var now = sourceUpdatedAt ?? DateTimeOffset.UtcNow;
        var inserted = chunks.Select((chunk, ordinal) => new EditorAiTextChunk
        {
            Id = Guid.NewGuid(), Kind = kind, SourceId = sourceId, EntityId = entityId, HoldId = holdId,
            CollectionId = collectionId, Title = title, Href = href, Fingerprint = fingerprint, Ordinal = ordinal,
            Text = chunk.Text, Location = chunk.Location is { Length: > 400 } heading ? heading[..399] + "…" : chunk.Location, UpdatedAtUtc = now
        }).ToArray();
        db.EditorAiTextChunks.AddRange(inserted);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // The caller can have a tracked private AI run in this same scope. Never detach unrelated work.
        foreach (var chunk in inserted) db.Entry(chunk).State = EntityState.Detached;
    }
}

/// <summary>A bounded, restartable index pass; never loads a page catalogue with document blobs.</summary>
public sealed class EditorAiContextIndexWorker(IServiceScopeFactory scopes, ILogger<EditorAiContextIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var catalogReady = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var indexer = scope.ServiceProvider.GetRequiredService<EditorAiContextIndexer>();
                if (!catalogReady) { await indexer.IndexCatalogAsync(stoppingToken); catalogReady = true; }
                var ids = await db.NotionPages.AsNoTracking().Where(p => !db.EditorAiTextChunks.Any(c => c.Kind == "notion-page"
                    && c.EntityId == p.Id && c.UpdatedAtUtc >= p.UpdatedAtUtc)).OrderBy(p => p.UpdatedAtUtc).Select(p => p.Id).Take(2).ToArrayAsync(stoppingToken);
                foreach (var id in ids)
                    try { await indexer.IndexNotionAsync(id, stoppingToken); }
                    catch (Exception error) when (error is not OperationCanceledException) { logger.LogWarning(error, "Teaching page indexing deferred for {PageId}", id); }
                // Deleted source chunks cannot be returned by ACL queries, and are eventually removed here.
                await db.EditorAiTextChunks.Where(c => c.Kind == "notion-page" && !db.NotionPages.Any(p => p.Id == c.EntityId)).ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning(error, "Teaching text index pass deferred"); }
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
