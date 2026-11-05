using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Storage;

public sealed record NotionUploadedPart(int PartNumber, string ETag, long SizeBytes, string Sha256);

public static class NotionUploadSessions
{
    public const int DefaultChunkBytes = 8 * 1024 * 1024;
    public const int MaxChunkBytes = 64 * 1024 * 1024;
    public const int MaxParts = 10_000; // S3 protocol constraint, not a product upload quota.

    public static int ChunkSize(long size) => (int)Math.Min(MaxChunkBytes,
        Math.Max(DefaultChunkBytes, 1 + (size - 1) / MaxParts));
    public static long PartCount(NotionAssetUpload upload) => 1 + (upload.SizeBytes - 1) / upload.ChunkSizeBytes;
    public static long PartLength(NotionAssetUpload upload, int part) =>
        Math.Min(upload.ChunkSizeBytes, upload.SizeBytes - (long)(part - 1) * upload.ChunkSizeBytes);
    public static List<NotionUploadedPart> Parts(NotionAssetUpload upload) =>
        JsonSerializer.Deserialize<List<NotionUploadedPart>>(upload.PartsJson) ?? [];

    // Revision is an EF concurrency token: multiple API replicas cannot mutate the same S3 manifest.
    // The bounded request is at most 64 MiB; a crashed request's lease becomes reclaimable.
    public static async Task<bool> TryLease(AppDbContext db, NotionAssetUpload upload, CancellationToken ct)
    {
        if (upload.LeaseUntilUtc > DateTimeOffset.UtcNow) return false;
        upload.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(30); upload.Revision++;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { db.Entry(upload).State = EntityState.Detached; return false; }
    }

    public static async Task Release(AppDbContext db, NotionAssetUpload upload)
    {
        if (db.Entry(upload).State == EntityState.Detached) return;
        upload.LeaseUntilUtc = null; upload.Revision++;
        try { await db.SaveChangesAsync(CancellationToken.None); }
        catch (DbUpdateConcurrencyException) { db.Entry(upload).State = EntityState.Detached; }
    }
}

public sealed class NotionUploadCleanup(IServiceScopeFactory scopes, ILogger<NotionUploadCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await Sweep(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IObjectStorage>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "Could not clean up expired editor uploads."); }
            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    public static async Task Sweep(AppDbContext db, IObjectStorage storage, CancellationToken ct)
    {
        if (!storage.IsConfigured || !storage.SupportsMultipart) return;
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.NotionAssetUploads.Where(x => x.AbortRequested || x.ExpiresAtUtc <= now)
            .OrderBy(x => x.ExpiresAtUtc).Take(100).ToArrayAsync(ct);
        foreach (var upload in candidates)
        {
            if (!await NotionUploadSessions.TryLease(db, upload, ct)) continue;
            try
            {
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                operation.CancelAfter(TimeSpan.FromMinutes(5));
                if (upload.Status is not ("completed" or "aborted") && upload.StorageUploadId.Length > 0)
                {
                    await storage.AbortMultipartAsync(upload.Key, upload.StorageUploadId, operation.Token);
                    // A crash can occur between completing S3 and registering the asset in PostgreSQL.
                    if (!await db.NotionAssets.AnyAsync(a => a.Id == upload.Id, operation.Token))
                        await storage.DeleteAsync(upload.Key, operation.Token);
                }
                db.NotionAssetUploads.Remove(upload);
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                if (db.Entry(upload).State == EntityState.Deleted) db.Entry(upload).State = EntityState.Unchanged;
                await NotionUploadSessions.Release(db, upload); throw;
            }
        }
    }
}
