using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/ai/editor/runs")]
public sealed class EditorAiRunsController(AppDbContext db, CourseAccess courses,
    EditorAiRunStore store, EditorAiResponseStream provider, IEditorAiContextService context) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Create(EditorAiRunRequest request, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!provider.IsConfigured) return StatusCode(503, new { error = "openai_not_configured" });
        try { return Ok(await store.ViewAsync(await store.CreateAsync(request, User, ct), ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (EditorAiAccessChangedException) { return Conflict(new { error = "source_access_changed" }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (request.ClientRequestId is Guid key && UserClaims.GetSub(User) is Guid sub)
            {
                var prior = await db.EditorAiRuns.Include(r => r.Hunks).SingleOrDefaultAsync(r => r.OwnerSub == sub && r.ClientRequestId == key && r.PageId == request.PageId, ct);
                if (prior is not null && await new NotionAccess(db, courses).CanManageAsync(prior.PageId, User, ct))
                {
                    try
                    {
                        var owned = await store.OwnedAsync(prior.Id, User, ct);
                        if (owned is not null) return Ok(await store.ViewAsync(owned, ct));
                    }
                    catch (EditorAiAccessChangedException) { return Conflict(new { error = "source_access_changed" }); }
                }
            }
            return Conflict(new { error = "run_in_progress" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid pageId, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User) || UserClaims.GetSub(User) is not Guid sub) return Forbid();
        if (!await new NotionAccess(db, courses).CanManageAsync(pageId, User, ct)) return Forbid();
        // List metadata only; inaccessible cached source excerpts are never exposed by the list route.
        var rows = await db.EditorAiRuns.AsNoTracking().Where(r => r.OwnerSub == sub && r.PageId == pageId)
            .Select(r => new { r.Id, r.PageId, r.Instruction, r.Mode, r.Scope, r.Status, r.Progress, r.CreatedAtUtc, r.ExpiresAtUtc }).ToListAsync(ct);
        return Ok(rows.Where(r => r.ExpiresAtUtc > DateTimeOffset.UtcNow).OrderByDescending(r => r.CreatedAtUtc).Take(50));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => WithRun(id, ct, async run => Ok(await store.ViewAsync(run, ct)));

    [HttpPost("{id:guid}/cancel")]
    [HttpPost("{id:guid}/stop")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => WithRun(id, ct, async run =>
    {
        run.Status = "cancelled"; run.Progress = "Stoppet. Færdige forslag er bevaret.";
        run.LeaseId = null; run.LeaseExpiresAtUtc = null;
        await store.PublishAsync(run, ct);
        var view = await store.ViewAsync(run, ct);
        foreach (var source in view.Sources)
            if (!await context.CanReadSourceAsync(User, source, ct))
                return Ok(view with { Sources = [], Hunks = [], Coverage = new(0, 0, 0, 0, false),
                    Error = "source_access_changed", Progress = "Opgaven er stoppet. Adgangen til en kilde er ændret." });
        return Ok(view);
    }, checkSources: false);

    [HttpPost("{id:guid}/continue")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> Continue(Guid id, EditorAiContinueRequest request, CancellationToken ct) => WithRun(id, ct, async run =>
    {
        if (EditorAiRunStore.ActiveStates.Contains(run.Status)) return Conflict(new { error = "run_in_progress" });
        var next = EditorAiRunStore.Request(run) with { Document = request.Document ?? EditorAiRunStore.Parse(run.DocumentJson), Target = request.Target ?? EditorAiRunStore.Deserialize<JsonElement?>(run.TargetJson, null) };
        EditorAiRunStore.ValidateRequest(next);
        run.DocumentJson = next.Document.GetRawText(); run.TargetJson = next.Target?.GetRawText() ?? "null";
        var adjusting = EditorAiRunStore.Deserialize<Guid[]>(run.AdjustHunkIdsJson, [])
            .Where(id => run.Hunks.Any(h => h.Id == id && h.Status is "ready" or "conflict")).ToArray();
        Queue(run); run.AdjustHunkIdsJson = EditorAiRunStore.Serialize(adjusting);
        await store.PublishAsync(run, ct); return Ok(await store.ViewAsync(run, ct));
    });

    [HttpPost("{id:guid}/adjust")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> Adjust(Guid id, EditorAiAdjustRequest request, CancellationToken ct) => WithRun(id, ct, async run =>
    {
        if (EditorAiRunStore.ActiveStates.Contains(run.Status)) return Conflict(new { error = "run_in_progress" });
        var ids = request.HunkIds ?? run.Hunks.Where(h => h.Status is "ready" or "conflict").Select(h => h.Id).ToArray();
        if (ids.Length == 0 || ids.Distinct().Count() != ids.Length || ids.Any(id => !run.Hunks.Any(h => h.Id == id && h.Status is "ready" or "conflict")))
            return BadRequest(new { error = "invalid_adjustment_hunks" });
        var next = EditorAiRunStore.Request(run) with { Instruction = request.Instruction, Document = request.Document ?? EditorAiRunStore.Parse(run.DocumentJson),
            Target = request.Target ?? EditorAiRunStore.Deserialize<JsonElement?>(run.TargetJson, null), ExcludedSourceIds = request.ExcludedSourceIds ?? EditorAiRunStore.Deserialize<string[]>(run.ExcludedSourceIdsJson, []) };
        EditorAiRunStore.ValidateRequest(next);
        run.Instruction = next.Instruction; run.DocumentJson = next.Document.GetRawText(); run.TargetJson = next.Target?.GetRawText() ?? "null";
        run.ExcludedSourceIdsJson = EditorAiRunStore.Serialize(next.ExcludedSourceIds);
        Queue(run); run.AdjustHunkIdsJson = EditorAiRunStore.Serialize(ids);
        await store.PublishAsync(run, ct); return Ok(await store.ViewAsync(run, ct));
    });

    [HttpPatch("{id:guid}/hunks/{hunkId:guid}")]
    public Task<IActionResult> Review(Guid id, Guid hunkId, EditorAiReviewRequest request, CancellationToken ct) => WithRun(id, ct, async run =>
    {
        if (request.Status is not ("accepted" or "rejected" or "conflict")) return BadRequest(new { error = "invalid_review_status" });
        var hunk = run.Hunks.SingleOrDefault(h => h.Id == hunkId);
        if (hunk is null) return NotFound();
        if (hunk.Version != request.Version) return Conflict(new { error = "hunk_version_changed" });
        if (hunk.Status == request.Status) return Ok(await store.ViewAsync(run, ct));
        if (hunk.Status is "accepted" or "rejected") return Conflict(new { error = "hunk_already_reviewed" });
        if (EditorAiRunStore.Deserialize<Guid[]>(run.AdjustHunkIdsJson, []).Contains(hunk.Id) && EditorAiRunStore.ActiveStates.Contains(run.Status))
            return Conflict(new { error = "hunk_being_adjusted" });
        var count = await db.EditorAiRunHunks.Where(h => h.Id == hunkId && h.RunId == run.Id && h.Version == request.Version && (h.Status == "ready" || h.Status == "conflict"))
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, request.Status), ct);
        if (count != 1) return Conflict(new { error = "hunk_version_changed" });
        await db.Entry(hunk).ReloadAsync(ct);
        await store.PublishAsync(run, ct); return Ok(await store.ViewAsync(run, ct));
    });

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => WithRun(id, ct, async run =>
    {
        db.EditorAiRuns.Remove(run); await db.SaveChangesAsync(ct); return NoContent();
    }, checkSources: false);

    [HttpGet("{id:guid}/events")]
    public async Task Events(Guid id, [FromQuery] long after = 0, CancellationToken ct = default)
    {
        if (!UserClaims.IsTeacher(User)) { Response.StatusCode = 403; return; }
        EditorAiRun? initial;
        try { initial = await store.OwnedAsync(id, User, ct); }
        catch (EditorAiAccessChangedException) { Response.StatusCode = 409; return; }
        if (initial is null) { Response.StatusCode = 404; return; }
        if (after <= 0 && long.TryParse(Request.Headers["Last-Event-ID"], out var header)) after = header;
        Response.ContentType = "text/event-stream"; Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        var lastHeartbeat = DateTimeOffset.MinValue; var lastAccessCheck = DateTimeOffset.UtcNow;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow >= lastAccessCheck)
                {
                    db.ChangeTracker.Clear();
                    try { if (await store.OwnedAsync(id, User, ct) is null) break; }
                    catch (EditorAiAccessChangedException) { break; }
                    lastAccessCheck = DateTimeOffset.UtcNow;
                }
                var events = await db.EditorAiRunEvents.AsNoTracking().Where(e => e.RunId == id && e.Id > after).OrderBy(e => e.Id).Take(100).ToListAsync(ct);
                foreach (var entry in events)
                {
                    // A historical cursor must not disclose excerpts from sources revoked since that event.
                    if (entry.Type == "run")
                    {
                        var historical = EditorAiRunStore.Deserialize<EditorAiRunView?>(entry.DataJson, null);
                        if (historical is not null)
                            foreach (var source in historical.Sources)
                                if (!await context.CanReadSourceAsync(User, source, ct)) return;
                    }
                    await Response.WriteAsync($"id: {entry.Id}\nevent: {entry.Type}\ndata: {entry.DataJson}\n\n", ct);
                    after = entry.Id;
                }
                if (DateTimeOffset.UtcNow - lastHeartbeat > TimeSpan.FromSeconds(10))
                { await Response.WriteAsync(": heartbeat\n\n", ct); lastHeartbeat = DateTimeOffset.UtcNow; }
                await Response.Body.FlushAsync(ct);
                if (events.Count == 0)
                {
                    var status = await db.EditorAiRuns.AsNoTracking().Where(r => r.Id == id).Select(r => r.Status).SingleOrDefaultAsync(ct);
                    if (status is null || !EditorAiRunStore.ActiveStates.Contains(status)) break;
                }
                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task<IActionResult> WithRun(Guid id, CancellationToken ct, Func<EditorAiRun, Task<IActionResult>> action, bool checkSources = true)
    {
        if (!UserClaims.IsTeacher(User)) return Forbid();
        try
        {
            var run = await store.OwnedAsync(id, User, ct, checkSources);
            return run is null ? NotFound() : await action(run);
        }
        catch (EditorAiAccessChangedException) { return Conflict(new { error = "source_access_changed", message = "Adgangen til en kilde er ændret. Start en ny opgave uden kilden." }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { error = "run_changed" }); }
    }

    private static void Queue(EditorAiRun run)
    {
        run.Status = "queued"; run.Error = null; run.Progress = "Venter på AI…";
        run.LeaseId = null; run.LeaseExpiresAtUtc = null; run.AttemptStartedAtUtc = null;
        run.AttemptTokens = 0; run.AttemptBatchCount = 0; run.AdjustHunkIdsJson = "[]";
    }
}
