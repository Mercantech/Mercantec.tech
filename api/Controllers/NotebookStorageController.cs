using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/internal/notebooks/documents")]
public sealed class NotebookStorageController(AppDbContext db, NotebookServiceKey key) : ControllerBase
{
    [HttpGet("{id:guid}/state")]
    public async Task<IActionResult> ReadState(Guid id, CancellationToken ct)
    {
        if (!key.Accepts(Request)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        var doc = await db.NotebookDocuments.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new { d.YjsState }).SingleOrDefaultAsync(ct);
        if (doc is null) return NotFound();
        return doc.YjsState is { Length: > 0 } state ? File(state, "application/octet-stream") : NoContent();
    }

    [HttpPut("{id:guid}/state"), RequestSizeLimit(48 * 1024 * 1024)]
    public async Task<IActionResult> WriteState(Guid id, StoreNotebookDocument request, CancellationToken ct)
    {
        if (!key.Accepts(Request)) return Unauthorized();
        if (!NotebookDocumentValidation.ValidProjection(request, out var state)) return BadRequest(new { error = "invalid_document" });
        // Serialize state + its derived projection in the same transaction. No client-provided search index is trusted.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var doc = await db.NotebookDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound();
        var assets = request.AssetIds.Distinct().ToArray();
        if (assets.Length != await db.NotebookAssets.CountAsync(a => a.DocumentId == id && assets.Contains(a.Id), ct))
            return BadRequest(new { error = "invalid_document_asset" });
        var drawingIds = request.DrawingIds ?? [];
        if (drawingIds.Length != await db.Drawings.CountAsync(d => d.NotebookDocumentId == id && drawingIds.Contains(d.Id), ct))
            return BadRequest(new { error = "invalid_drawing" });
        if (drawingIds.Length > 0)
        {
            var owned = await db.Drawings.AsNoTracking().Where(d => d.NotebookDocumentId == id && drawingIds.Contains(d.Id))
                .Select(d => new { d.Id, d.BlockId }).ToArrayAsync(ct);
            if (request.DrawingBlocks is null || request.DrawingBlocks.Count != drawingIds.Length || owned.Any(d =>
                !request.DrawingBlocks.TryGetValue(d.Id.ToString(), out var blockId) || blockId != d.BlockId))
                return BadRequest(new { error = "invalid_drawing" });
        }
        if (doc.Fingerprint == request.Fingerprint)
            return Ok(new { savedAtUtc = doc.UpdatedAtUtc });
        var updated = DateTimeOffset.UtcNow;
        var manifest = JsonSerializer.Serialize(assets);
        var drawingManifest = JsonSerializer.Serialize(drawingIds);
        // Atomic increments preserve metadata revisions arriving between our read and this write.
        await db.NotebookDocuments.Where(d => d.Id == id).ExecuteUpdateAsync(update => update
            .SetProperty(d => d.YjsState, state).SetProperty(d => d.Fingerprint, request.Fingerprint)
            .SetProperty(d => d.Revision, d => d.Revision + 1).SetProperty(d => d.SearchText, request.PlainText)
            .SetProperty(d => d.AssetIdsJson, manifest).SetProperty(d => d.DrawingIdsJson, drawingManifest)
            .SetProperty(d => d.UpdatedAtUtc, updated), ct);
        var oldLinks = await db.NotebookLinks.Where(l => l.FromId == id).ToArrayAsync(ct);
        db.NotebookLinks.RemoveRange(oldLinks);
        // Targets must be in this notebook; inaccessible and cross-domain IDs cannot enter its graph.
        var requestedLinks = request.LinkIds.Distinct().Where(target => target != id).ToArray();
        var targets = doc.NotebookId is Guid notebookId
            ? await db.NotebookDocuments.Where(d => d.NotebookId == notebookId && requestedLinks.Contains(d.Id))
                .Select(d => d.Id).ToArrayAsync(ct) : [];
        db.NotebookLinks.AddRange(targets.Select(target => new NotebookLink { FromId = id, ToId = target }));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { savedAtUtc = updated });
    }
}
