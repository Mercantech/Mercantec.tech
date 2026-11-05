using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Storage;

/// <summary>Runs in the dedicated conversion container, never in an HTTP request.</summary>
public sealed class PreviewWorker(IServiceScopeFactory scopes, ILogger<PreviewWorker> logger, IConfiguration configuration) : BackgroundService
{
    private static readonly string[] Documents = [".doc", ".docx", ".odt", ".rtf", ".ppt", ".pptx", ".odp", ".xls", ".xlsx", ".ods"];
    private static readonly string[] Videos = [".mp4", ".mov", ".mkv", ".avi", ".wmv", ".mpeg", ".mpg", ".m4v"];

    public static bool CanConvert(string name) =>
        Documents.Contains(Path.GetExtension(name).ToLowerInvariant()) || IsVideo(name);

    internal static bool IsVideo(string name) => Videos.Contains(Path.GetExtension(name).ToLowerInvariant());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var spool = configuration["PreviewWorker:SpoolPath"] ?? "/spool";
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                PreviewSpool.CleanupAbandoned(spool, DateTime.UtcNow, logger);
                if (await ProcessNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Preview worker iteration failed"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        if (!storage.IsConfigured) return false;
        var expired = DateTimeOffset.UtcNow.AddMinutes(-15);
        // Reclaim interrupted jobs. Database time conversion is kept out of the query for SQLite tests.
        var candidates = await db.FileAssets.AsNoTracking()
            .Where(f => !db.FilePreviews.Any(p => p.FileId == f.Id))
            .OrderBy(f => f.CreatedAtUtc).Take(100).ToListAsync(ct);
        var asset = candidates.FirstOrDefault(f => CanConvert(f.OriginalName));
        foreach (var unsupported in candidates.Where(f => !CanConvert(f.OriginalName)))
            db.FilePreviews.Add(new FilePreview
            {
                FileId = unsupported.Id, State = "unsupported", UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        FilePreview? preview = null;
        if (asset is null)
        {
            var running = await db.FilePreviews.Where(p => p.State == "processing").ToListAsync(ct);
            preview = running.FirstOrDefault(p => p.UpdatedAtUtc < expired);
            if (preview is null) return candidates.Count > 0;
            var claimed = await db.FilePreviews.Where(p => p.FileId == preview.FileId && p.UpdatedAtUtc == preview.UpdatedAtUtc)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow), ct);
            if (claimed == 0) return true;
            asset = await db.FileAssets.AsNoTracking().SingleAsync(f => f.Id == preview.FileId, ct);
            await db.Entry(preview).ReloadAsync(ct);
        }
        else
        {
            preview = new FilePreview { FileId = asset.Id, UpdatedAtUtc = DateTimeOffset.UtcNow };
            db.FilePreviews.Add(preview);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return true; } // Another worker claimed this file.
        }

        var spool = configuration["PreviewWorker:SpoolPath"] ?? "/spool";
        var directory = Path.Combine(spool, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "input" + Path.GetExtension(asset.OriginalName).ToLowerInvariant());
            await using (var source = await storage.GetAsync(asset.Key, ct))
            {
                if (source is null) throw new IOException("Original file missing");
                await using var target = new FileStream(input, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await source.Content.CopyToAsync(target, ct);
            }

            var video = IsVideo(asset.OriginalName);
            var output = Path.Combine(directory, video ? "preview.mp4" : "input.pdf");
            await File.WriteAllTextAsync(Path.Combine(directory, "ready"), "", ct);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wait.CancelAfter(TimeSpan.FromMinutes(12));
            while (!File.Exists(Path.Combine(directory, "done")))
            {
                if (File.Exists(Path.Combine(directory, "error"))) throw new IOException("Converter failed");
                await Task.Delay(TimeSpan.FromSeconds(2), wait.Token);
            }
            var info = new FileInfo(output);
            if (!info.Exists || info.Length == 0) throw new IOException("Converter produced no preview");
            var key = $"previews/{asset.Id:N}/" + (video ? "preview.mp4" : "preview.pdf");
            await using var converted = File.OpenRead(output);
            var contentType = video ? "video/mp4" : "application/pdf";
            await storage.PutAsync(key, converted, contentType, ct);
            preview.State = "ready";
            preview.StorageKey = key;
            preview.ContentType = contentType;
            preview.SizeBytes = info.Length;
            preview.Error = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogWarning(error, "Preview conversion failed for file {FileId}", asset.Id);
            preview.State = "failed";
            preview.Error = "Forhåndsvisningen kunne ikke dannes. Originalfilen kan stadig hentes.";
        }
        finally
        {
            // Cleanup failure must never prevent persisting a ready or failed preview result.
            PreviewSpool.TryDeleteJob(spool, directory, logger);
        }
        preview.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

}
