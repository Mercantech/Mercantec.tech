using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Internal Hocuspocus persistence only. Never accepts a browser user's JWT as storage access.</summary>
[ApiController, AllowAnonymous, Route("api/internal/notion/documents")]
public sealed class NotionStorageController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    public const int MaxStateBytes = 16 * 1024 * 1024;

    [HttpGet("{id:guid}/state")]
    public async Task<IActionResult> ReadState(Guid id, CancellationToken ct)
    {
        var denied = CheckServiceKey();
        if (denied is not null) return denied;
        Response.Headers.CacheControl = "no-store";
        var document = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.YjsState }).SingleOrDefaultAsync(ct);
        if (document is null) return NotFound();
        return document.YjsState is { Length: > 0 } bytes ? File(bytes, "application/octet-stream") : NoContent();
    }

    [HttpPut("{id:guid}/state"), RequestSizeLimit(48 * 1024 * 1024)]
    public async Task<IActionResult> WriteState(Guid id, CancellationToken ct)
    {
        var denied = CheckServiceKey();
        if (denied is not null) return denied;
        var contentType = Request.ContentType?.Split(';')[0].Trim();
        var json = string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase);
        if (!json && !string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (Request.ContentLength > (json ? 24 * 1024 * 1024 : MaxStateBytes)) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (!await db.NotionPages.AnyAsync(p => p.Id == id, ct)) return NotFound();
        byte[] bytes;
        string? fingerprint;
        string? drawingManifest = null;
        StoreNotionMcpChange? mcpChange = null;
        if (json)
        {
            StoreNotionDocument? projection;
            try { projection = await Request.ReadFromJsonAsync<StoreNotionDocument>(ct); }
            catch (JsonException) { return BadRequest(new { error = "invalid_document" }); }
            if (projection?.StateBase64 is not { Length: > 0 and <= 24 * 1024 * 1024 }
                || !IsFingerprint(projection.Fingerprint)) return BadRequest(new { error = "invalid_document" });
            try { bytes = Convert.FromBase64String(projection.StateBase64); }
            catch (FormatException) { return BadRequest(new { error = "invalid_document" }); }
            if (bytes.Length is 0 or > MaxStateBytes) return BadRequest(new { error = "invalid_document" });
            var drawingIds = projection.DrawingIds ?? [];
            if (drawingIds.Length > 1000 || drawingIds.Contains(Guid.Empty) || drawingIds.Distinct().Count() != drawingIds.Length
                || await db.Drawings.CountAsync(d => d.NotionPageId == id && drawingIds.Contains(d.Id), ct) != drawingIds.Length)
                return BadRequest(new { error = "invalid_drawing" });
            if (drawingIds.Length > 0)
            {
                var bindings = projection.DrawingBlocks;
                var owned = await db.Drawings.AsNoTracking().Where(d => d.NotionPageId == id && drawingIds.Contains(d.Id))
                    .Select(d => new { d.Id, d.BlockId }).ToArrayAsync(ct);
                if (bindings is null || bindings.Count != drawingIds.Length || owned.Any(d =>
                    !bindings.TryGetValue(d.Id.ToString(), out var blockId) || blockId != d.BlockId))
                    return BadRequest(new { error = "invalid_drawing" });
            }
            fingerprint = projection.Fingerprint;
            drawingManifest = JsonSerializer.Serialize(drawingIds);
            mcpChange = projection.McpChange;
        }
        else
        {
        using var state = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await Request.Body.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (state.Length + count > MaxStateBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);
            await state.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        if (state.Length == 0) return BadRequest(new { error = "empty_state" });
        var hasFingerprint = Request.Headers.ContainsKey("X-Notion-State-Fingerprint");
        fingerprint = hasFingerprint ? Request.Headers["X-Notion-State-Fingerprint"].ToString() : null;
        if (hasFingerprint && !IsFingerprint(fingerprint)) return BadRequest(new { error = "invalid_fingerprint" });
        bytes = state.ToArray();
        }
        var updated = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The source row also serializes its reference diff and durable mention occurrences.
        await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublicationVersion, p => p.PublicationVersion), ct);
        Mercantec.Tech.Api.Data.Entities.NotionMcpChange? operation = null;
        if (mcpChange is not null)
        {
            operation = await db.NotionMcpChanges.SingleOrDefaultAsync(c => c.Id == mcpChange.OperationId, ct);
            if (operation is null || operation.PageId != id || operation.OwnerSub != mcpChange.OwnerSub
                || operation.RequestHash != mcpChange.RequestHash || operation.BeforeJson is null || operation.AfterJson is null)
                return Conflict(new { error = "operation_not_prepared" });
        }
        var references = new Mercantec.Tech.Api.References.ReferenceService(db);
        await references.BaselineNotionAsync(id, ct);
        // Atomic column update leaves concurrently renamed page metadata intact.
        var written = await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.YjsState, bytes).SetProperty(p => p.UpdatedAtUtc, updated)
            // The authenticated v1 service may still flush while Compose replaces it. Preserve
            // those bytes, but require the v2 service to stamp them before publication.
            .SetProperty(p => p.DraftRevision, p => fingerprint != null && p.DraftFingerprint == fingerprint ? p.DraftRevision : p.DraftRevision + 1)
            .SetProperty(p => p.DraftFingerprint, fingerprint)
            .SetProperty(p => p.DrawingIdsJson, p => drawingManifest ?? p.DrawingIdsJson), ct);
        if (written == 0) return NotFound();
        await references.RefreshNotionAsync(id, ct);
        if (operation is { Status: "pending" })
        {
            if (operation.UndoOf is Guid originalId)
            {
                var original = await db.NotionMcpChanges.SingleOrDefaultAsync(c => c.Id == originalId, ct);
                if (original is null || original.PageId != id || original.OwnerSub != operation.OwnerSub || original.Status != "applied")
                    return Conflict(new { error = "undo_conflict" });
                original.Status = "undone";
                original.UndoneAtUtc = updated;
            }
            operation.Status = "applied";
            operation.AppliedAtUtc = updated;
            operation.Error = null;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Ok(new { savedAtUtc = updated, mcpOperationId = operation?.Id });
    }

    public static bool IsFingerprint(string? fingerprint) => fingerprint is { Length: 64 }
        && fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private string? ReadConfiguredSecretFile()
    {
        // This path is deployment configuration only, never supplied by a request.
        var path = configuration["Notion:ServiceSecretFile"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        try
        {
            using var file = System.IO.File.OpenRead(path);
            if (file.Length > 1024) return null;
            using var reader = new StreamReader(file, Encoding.UTF8, true);
            return reader.ReadToEnd().Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private IActionResult? CheckServiceKey()
    {
        var expected = configuration["Notion:ServiceSecret"];
        if (string.IsNullOrWhiteSpace(expected)) expected = ReadConfiguredSecretFile();
        // A missing/weak deployment secret cannot accidentally make persistence public.
        if (string.IsNullOrEmpty(expected) || expected.Length is < 32 or > 1024)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "notion_not_configured" });
        if (!Request.Headers.TryGetValue("X-Notion-Service-Key", out var values) || values.Count != 1)
            return Unauthorized();
        var supplied = values[0] ?? "";
        if (supplied.Length > 1024) return Unauthorized();
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied))) ? null : Unauthorized();
    }
}
