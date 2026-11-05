using System.Security.Cryptography;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notion/pages/{pageId:guid}/assets/uploads")]
public sealed class NotionAssetUploadsController(AppDbContext db, CourseAccess access, IObjectStorage storage) : ControllerBase
{
    public sealed record CreateUpload(string OriginalName, long SizeBytes, string? ContentType = null);
    public sealed record UploadCreated(Guid Id, int ChunkSizeBytes);

    [HttpPost]
    public async Task<IActionResult> Create(Guid pageId, CreateUpload input, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await CanManage(pageId, ct)) return NotFound();
        if (!storage.IsConfigured || !storage.SupportsMultipart) return StatusCode(503, new { error = "multipart_storage_unavailable" });
        var name = Path.GetFileName((input.OriginalName ?? "").Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name.Any(char.IsControl))
            return BadRequest(new { error = "invalid_filename" });
        var type = NotionMediaPolicy.Classify(name);
        if (type is null) return BadRequest(new { error = "unsupported_file_type" });
        if (input.SizeBytes <= 0) return BadRequest(new { error = "empty_file" });
        var chunk = NotionUploadSessions.ChunkSize(input.SizeBytes);
        if (1 + (input.SizeBytes - 1) / chunk > NotionUploadSessions.MaxParts)
            return BadRequest(new { error = "unsupported_storage_size" });
        var id = Guid.NewGuid();
        var upload = new NotionAssetUpload
        {
            Id = id, PageId = pageId, UploadedBySub = sub, OriginalName = name, SizeBytes = input.SizeBytes,
            ContentType = type.Value.ContentType, Kind = type.Value.Kind, ChunkSizeBytes = chunk,
            Key = $"notion-private/{pageId:N}/{id:N}", CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24), LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        db.NotionAssetUploads.Add(upload);
        await db.SaveChangesAsync(ct);
        try
        {
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            operation.CancelAfter(TimeSpan.FromMinutes(5));
            upload.StorageUploadId = await storage.InitiateMultipartAsync(upload.Key, upload.ContentType, operation.Token);
            upload.Status = "uploading"; upload.LeaseUntilUtc = null; upload.Revision++;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            if (upload.StorageUploadId.Length > 0)
                await storage.AbortMultipartAsync(upload.Key, upload.StorageUploadId, CancellationToken.None);
            db.NotionAssetUploads.Remove(upload); await db.SaveChangesAsync(CancellationToken.None); throw;
        }
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new UploadCreated(id, chunk));
    }

    [HttpPut("{id:guid}/parts/{partNumber:int}")]
    [RequestSizeLimit(NotionUploadSessions.MaxChunkBytes)]
    public async Task<IActionResult> Part(Guid pageId, Guid id, int partNumber, CancellationToken ct)
    {
        var upload = await Authorized(pageId, id, ct);
        if (upload is null) return NotFound();
        if (upload.Status != "uploading" || upload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Conflict(new { error = "upload_not_active" });
        if (partNumber < 1 || partNumber > NotionUploadSessions.PartCount(upload))
            return BadRequest(new { error = "invalid_part_number" });
        var expected = NotionUploadSessions.PartLength(upload, partNumber);
        if (Request.ContentLength is long length && length != expected)
            return BadRequest(new { error = "invalid_part_size", expectedBytes = expected });

        // Only one bounded part is on local disk. Never buffer a complete video, or trust Content-Length.
        var path = Path.Combine(Path.GetTempPath(), $"notion-part-{Guid.NewGuid():N}");
        await using var part = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920]; int read;
        while ((read = await Request.Body.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, expected - part.Length + 1)), ct)) > 0)
        {
            if (part.Length + read > expected) return BadRequest(new { error = "invalid_part_size", expectedBytes = expected });
            hash.AppendData(buffer, 0, read); await part.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (part.Length != expected) return BadRequest(new { error = "incomplete_part", expectedBytes = expected });
        if (partNumber == 1)
        {
            part.Position = 0; var header = new byte[32]; var headerLength = await part.ReadAsync(header, ct);
            if (!NotionMediaPolicy.MatchesSignature(upload.OriginalName, header.AsSpan(0, headerLength)))
                return BadRequest(new { error = "invalid_file_signature" });
        }
        var digest = Convert.ToHexString(hash.GetHashAndReset());
        // Another part may have finished while this request was spooling; refresh before leasing.
        await db.Entry(upload).ReloadAsync(ct);
        if (upload.Status != "uploading" || upload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Conflict(new { error = "upload_not_active" });
        if (!await NotionUploadSessions.TryLease(db, upload, ct)) return Busy();
        try
        {
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            operation.CancelAfter(TimeSpan.FromMinutes(5));
            if (await CancellationRequested(upload.Id)) return await CancelLeased(upload);
            var parts = NotionUploadSessions.Parts(upload);
            var existing = parts.SingleOrDefault(p => p.PartNumber == partNumber);
            if (existing is not null)
                return existing.Sha256 == digest ? Ok(new { partNumber }) : Conflict(new { error = "part_already_uploaded" });
            part.Position = 0;
            var etag = await storage.UploadPartAsync(upload.Key, upload.StorageUploadId, partNumber, part, operation.Token);
            if (await CancellationRequested(upload.Id)) return await CancelLeased(upload);
            parts.Add(new(partNumber, etag, part.Length, digest));
            upload.PartsJson = JsonSerializer.Serialize(parts.OrderBy(p => p.PartNumber));
            upload.ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24);
            // Persist an accepted S3 ETag even when the browser cancelled after the S3 request completed.
            await db.SaveChangesAsync(CancellationToken.None);
            return Ok(new { partNumber });
        }
        finally { await NotionUploadSessions.Release(db, upload); }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid pageId, Guid id, CancellationToken ct)
    {
        var upload = await Authorized(pageId, id, ct);
        if (upload is null) return NotFound();
        if (upload.Status == "completed") return await Asset(upload.Id, ct);
        if (upload.Status != "uploading" || upload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Conflict(new { error = "upload_not_active" });
        if (!await NotionUploadSessions.TryLease(db, upload, ct)) return Busy();
        try
        {
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            operation.CancelAfter(TimeSpan.FromMinutes(5));
            if (await CancellationRequested(upload.Id)) return await CancelLeased(upload);
            var parts = NotionUploadSessions.Parts(upload).OrderBy(p => p.PartNumber).ToArray();
            if (parts.Length != NotionUploadSessions.PartCount(upload)
                || parts.Where((p, i) => p.PartNumber != i + 1 || p.SizeBytes != NotionUploadSessions.PartLength(upload, i + 1)).Any())
                return BadRequest(new { error = "incomplete_upload" });
            try
            {
                await storage.CompleteMultipartAsync(upload.Key, upload.StorageUploadId,
                    parts.Select(p => new ObjectUploadPart(p.PartNumber, p.ETag)).ToArray(), operation.Token);
            }
            catch
            {
                // Completing can succeed at S3 just before a connection is lost. The key is private,
                // unique and server-owned; only this session can have created the object.
                using var recovery = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await using var existing = await storage.GetAsync(upload.Key, recovery.Token);
                if (existing?.ContentLength != upload.SizeBytes) throw;
            }
            if (await CancellationRequested(upload.Id)) return await CancelLeased(upload);
            db.NotionAssets.Add(new()
            {
                Id = upload.Id, PageId = upload.PageId, Key = upload.Key, OriginalName = upload.OriginalName,
                ContentType = upload.ContentType, Kind = upload.Kind, SizeBytes = upload.SizeBytes,
                UploadedBySub = upload.UploadedBySub, CreatedAtUtc = DateTimeOffset.UtcNow
            });
            upload.Status = "completed"; upload.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7);
            await db.SaveChangesAsync(CancellationToken.None);
            return await Asset(upload.Id, CancellationToken.None);
        }
        finally { await NotionUploadSessions.Release(db, upload); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Abort(Guid pageId, Guid id, CancellationToken ct)
    {
        var upload = await Authorized(pageId, id, ct);
        if (upload is null) return NotFound();
        if (upload.Status is "completed" or "aborted") return NoContent();
        if (!await NotionUploadSessions.TryLease(db, upload, ct))
        {
            // Cancellation is an independent intent flag, not a competing manifest mutation. Do not
            // bump Revision: the active request must be able to persist its ETag and release its lease.
            await db.NotionAssetUploads.Where(u => u.Id == id && u.PageId == pageId && u.Status != "completed")
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.AbortRequested, true), ct);
            return NoContent();
        }
        try
        {
            await CancelLeased(upload);
            return NoContent();
        }
        finally { await NotionUploadSessions.Release(db, upload); }
    }

    private async Task<bool> CancellationRequested(Guid id)
    {
        using var operation = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        return await db.NotionAssetUploads.Where(u => u.Id == id).Select(u => u.AbortRequested).SingleAsync(operation.Token);
    }

    private async Task<IActionResult> CancelLeased(NotionAssetUpload upload)
    {
        using var operation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await storage.AbortMultipartAsync(upload.Key, upload.StorageUploadId, operation.Token);
        if (!await db.NotionAssets.AnyAsync(a => a.Id == upload.Id, operation.Token))
            await storage.DeleteAsync(upload.Key, operation.Token);
        upload.Status = "aborted"; upload.AbortRequested = true;
        upload.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1);
        await db.SaveChangesAsync(operation.Token);
        return Conflict(new { error = "upload_cancelled" });
    }

    private async Task<IActionResult> Asset(Guid id, CancellationToken ct)
    {
        var asset = await db.NotionAssets.AsNoTracking().SingleAsync(a => a.Id == id, ct);
        return Ok(new NotionAssetView(asset.Id, asset.OriginalName, asset.ContentType, asset.Kind, asset.SizeBytes));
    }

    private IActionResult Busy()
    {
        Response.Headers.RetryAfter = "2";
        return Conflict(new { error = "upload_busy" });
    }

    private async Task<bool> CanManage(Guid pageId, CancellationToken ct)
    {
        var holdId = await db.NotionPages.AsNoTracking().Where(p => p.Id == pageId).Select(p => (Guid?)p.HoldId).SingleOrDefaultAsync(ct);
        return holdId is Guid id && await access.CanManageAsync(id, User, ct);
    }

    private async Task<NotionAssetUpload?> Authorized(Guid pageId, Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub || !await CanManage(pageId, ct)) return null;
        return await db.NotionAssetUploads.SingleOrDefaultAsync(u => u.Id == id && u.PageId == pageId && u.UploadedBySub == sub, ct);
    }
}
