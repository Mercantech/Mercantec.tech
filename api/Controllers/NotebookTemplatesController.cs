using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notebooks")]
public sealed class NotebookTemplatesController(AppDbContext db, NotebookAccess access, NotebookEngineClient engine, DrawingService drawings) : ControllerBase
{
    [HttpGet("today")]
    public IActionResult Today()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(NotebookPeriods.LocalDay(DateTimeOffset.UtcNow));
    }

    [HttpGet("templates")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        await NotebookDefaults.EnsureAsync(db, ct);
        var teacher = UserClaims.IsTeacher(User);
        var templates = await (from t in db.NotebookTemplates.AsNoTracking()
            join d in db.NotebookDocuments on t.Id equals d.Id
            join v in db.NotebookTemplateVersions on t.ActiveVersionId equals (Guid?)v.Id into versions
            from v in versions.DefaultIfEmpty()
            where teacher || t.IsActive && v != null
            select new NotebookTemplateView(t.Id, t.Id, teacher ? d.Title : v!.Title,
                teacher ? t.Cadence : v!.Cadence, t.IsActive, t.Version, d.UpdatedAtUtc)).ToArrayAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(templates.OrderBy(t => t.Title).ToArray());
    }

    [HttpPost("templates")]
    public async Task<IActionResult> Create(CreateNotebookTemplate request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!ValidTitle(request.Title) || !NotebookPeriods.ValidCadence(request.Cadence)) return BadRequest(new { error = "invalid_template" });
        var id = Guid.NewGuid();
        StoreNotebookDocument projection;
        try { projection = await engine.CloneAsync(new(null, id, []), ct); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        NotebookDocumentValidation.ValidProjection(projection, out var state);
        var now = DateTimeOffset.UtcNow;
        var doc = new NotebookDocument { Id = id, Kind = "template", Title = request.Title.Trim(),
            YjsState = state, Revision = 1, Fingerprint = projection.Fingerprint, SearchText = projection.PlainText,
            DrawingIdsJson = JsonSerializer.Serialize(projection.DrawingIds ?? []),
            CreatedAtUtc = now, UpdatedAtUtc = now };
        var template = new NotebookTemplate { Id = id, Cadence = request.Cadence };
        db.NotebookDocuments.Add(doc); db.NotebookTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Ok(View(template, doc));
    }

    [HttpGet("templates/{id:guid}/context")]
    public async Task<IActionResult> Context(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var doc = await db.NotebookDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.Kind == "template", ct);
        if (doc is null) return NotFound();
        if (!await access.CanEditAsync(id, User, ct)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotebookDocumentContext(doc.Id, null, doc.Title, doc.Kind, true, true, false, false,
            doc.YjsState is null ? null : Convert.ToBase64String(doc.YjsState), doc.UpdatedAtUtc, doc.Revision,
            doc.Fingerprint, [], [], null, null));
    }

    [HttpPatch("templates/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNotebookTemplate request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!ValidTitle(request.Title) || !NotebookPeriods.ValidCadence(request.Cadence)) return BadRequest(new { error = "invalid_template" });
        var template = await db.NotebookTemplates.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();
        var doc = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == id, ct);
        if (doc.Title != request.Title.Trim() || template.Cadence != request.Cadence)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var title = request.Title.Trim();
            await db.NotebookDocuments.Where(d => d.Id == id).ExecuteUpdateAsync(update => update
                .SetProperty(d => d.Title, title).SetProperty(d => d.Revision, d => d.Revision + 1)
                .SetProperty(d => d.UpdatedAtUtc, DateTimeOffset.UtcNow), ct);
            template.Cadence = request.Cadence;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Conflict(new { error = "template_changed" }); }
            await transaction.CommitAsync(ct);
            doc = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == id, ct);
        }
        return Ok(View(template, doc));
    }

    [HttpPost("templates/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, ActivateNotebookTemplate request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.ExpectedFingerprint)) return BadRequest(new { error = "save_template_first" });
        var savedDrawingIds = await db.NotebookDocuments.AsNoTracking().Where(d => d.Id == id && d.Kind == "template")
            .Select(d => d.DrawingIdsJson).SingleOrDefaultAsync(ct);
        if (savedDrawingIds is null) return NotFound();
        var drawingIds = JsonSerializer.Deserialize<Guid[]>(savedDrawingIds) ?? [];
        // Checkpoints may store a drawing and touch its parent, so flush before taking the parent row lock.
        try { await drawings.CheckpointAsync(drawingIds, ct); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // A conditional row lock makes checking and copying the saved draft one atomic action.
        var matching = await db.NotebookDocuments.Where(d => d.Id == id && d.Kind == "template"
            && d.Revision == request.ExpectedRevision && d.Fingerprint == request.ExpectedFingerprint && d.YjsState != null)
            .ExecuteUpdateAsync(update => update.SetProperty(d => d.UpdatedAtUtc, d => d.UpdatedAtUtc), ct);
        if (matching != 1) return Conflict(new { error = "template_changed", message = "Skabelonen er ændret. Opdatér, og prøv igen." });
        var template = await db.NotebookTemplates.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();
        var doc = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == id, ct);
        DrawingPublicationBinding[] drawingBindings;
        try { drawingBindings = await drawings.PrepareTemplateAsync(id, JsonSerializer.Deserialize<Guid[]>(doc.DrawingIdsJson) ?? [], request.Drawings, ct); }
        catch (DrawingException error) { return DrawingFailure(error); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        var version = new NotebookTemplateVersion { Id = Guid.NewGuid(), TemplateId = id, Number = template.Version + 1,
            Title = doc.Title, Cadence = template.Cadence, State = doc.YjsState!, AssetIdsJson = doc.AssetIdsJson,
            DrawingsJson = JsonSerializer.Serialize(drawingBindings),
            CreatedAtUtc = DateTimeOffset.UtcNow };
        db.NotebookTemplateVersions.Add(version);
        await db.SaveChangesAsync(ct);
        template.Version = version.Number; template.ActiveVersionId = version.Id; template.IsActive = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "template_changed" }); }
        await transaction.CommitAsync(ct);
        return Ok(View(template, doc));
    }

    [HttpPost("templates/{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var template = await db.NotebookTemplates.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();
        template.IsActive = false;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "template_changed" }); }
        return Ok(View(template, await db.NotebookDocuments.SingleAsync(d => d.Id == id, ct)));
    }

    [HttpPost("entries")]
    public async Task<IActionResult> EnsureEntry(EnsureJournalEntry request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (request.RequestId == Guid.Empty) return BadRequest(new { error = "missing_request_id" });
        if (request.HoldId is Guid holdId && !await new CourseAccess(db).CanAccessAsync(holdId, User, ct)) return NotFound();
        var notebook = await db.Notebooks.SingleOrDefaultAsync(n => n.OwnerSub == sub, ct);
        if (notebook is null) return BadRequest(new { error = "open_notebook_first" });
        // A repeat of an acknowledged free-entry request remains idempotent even after archive/update.
        var prior = await db.NotebookDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.NotebookId == notebook.Id && d.RequestId == request.RequestId, ct);
        if (prior is not null) return prior.TemplateId == request.TemplateId ? Ok(PageView(prior)) : Conflict(new { error = "request_id_reused" });
        var version = await (from template in db.NotebookTemplates.AsNoTracking()
            join v in db.NotebookTemplateVersions on template.ActiveVersionId equals (Guid?)v.Id
            where template.Id == request.TemplateId && template.IsActive select v).SingleOrDefaultAsync(ct);
        if (version is null) return NotFound();
        var day = request.PeriodDate ?? NotebookPeriods.LocalDay(DateTimeOffset.UtcNow);
        var start = NotebookPeriods.Start(version.Cadence, day);
        var periodKey = NotebookPeriods.Key(version.Cadence, day);
        if (periodKey is not null)
        {
            var existing = await FindPeriod(notebook.Id, version.TemplateId, periodKey, ct);
            if (existing is not null) return Ok(PageView(existing));
        }
        var oldIds = JsonSerializer.Deserialize<Guid[]>(version.AssetIdsJson) ?? [];
        var originals = await db.NotebookAssets.AsNoTracking().Where(a => a.DocumentId == version.TemplateId && oldIds.Contains(a.Id)).ToArrayAsync(ct);
        if (originals.Length != oldIds.Length) return Unavailable();
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var copies = originals.Select(a => new NotebookAsset { Id = Guid.NewGuid(), DocumentId = id,
            Key = a.Key, OriginalName = a.OriginalName, ContentType = a.ContentType, Kind = a.Kind,
            SizeBytes = a.SizeBytes, UploadedBySub = sub, CreatedAtUtc = now }).ToArray();
        var map = originals.Zip(copies).ToDictionary(pair => pair.First.Id.ToString(), pair => pair.Second.Id.ToString());
        StoreNotebookDocument projection;
        DrawingCloneResult drawingCopies;
        try
        {
            var bindings = JsonSerializer.Deserialize<DrawingPublicationBinding[]>(version.DrawingsJson) ?? [];
            drawingCopies = await drawings.CloneTemplateAsync(version.TemplateId, id, bindings, ct);
            projection = await engine.CloneAsync(new(Convert.ToBase64String(version.State), id, map, DrawingMap: drawingCopies.DrawingMap), ct);
        }
        catch (DrawingException error) { return DrawingFailure(error); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        NotebookDocumentValidation.ValidProjection(projection, out var state);
        if (!projection.AssetIds.ToHashSet().SetEquals(copies.Select(a => a.Id))) return Unavailable();
        if (!(projection.DrawingIds ?? []).ToHashSet().SetEquals(drawingCopies.DrawingMap.Values.Select(Guid.Parse))) return Unavailable();
        try { drawings.BindClonedBlocks(projection.DrawingBlocks); }
        catch (DrawingException error) { return DrawingFailure(error); }
        var title = version.Cadence == "free" ? version.Title : $"{version.Title} · {start:dd.MM.yyyy}";
        var doc = new NotebookDocument { Id = id, NotebookId = notebook.Id, Kind = "journal", Title = title.Length > 200 ? title[..200] : title,
            TemplateId = version.TemplateId, TemplateVersionId = version.Id, RequestId = request.RequestId,
            PeriodKey = periodKey, PeriodStart = start, HoldId = request.HoldId, YjsState = state,
            Fingerprint = projection.Fingerprint, SearchText = projection.PlainText,
            AssetIdsJson = JsonSerializer.Serialize(projection.AssetIds), DrawingIdsJson = JsonSerializer.Serialize(projection.DrawingIds ?? []), Revision = 1,
            CreatedAtUtc = now, UpdatedAtUtc = now };
        var linkedTargets = await db.NotebookDocuments.AsNoTracking().Where(d => d.NotebookId == notebook.Id
            && projection.LinkIds.Contains(d.Id)).Select(d => d.Id).ToArrayAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.NotebookDocuments.Add(doc); db.NotebookAssets.AddRange(copies);
            db.NotebookLinks.AddRange(linkedTargets.Distinct().Select(target => new NotebookLink { FromId = id, ToId = target }));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (NotebookPeriods.IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            var winner = await db.NotebookDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.NotebookId == notebook.Id
                && d.RequestId == request.RequestId, ct);
            if (winner is not null && winner.TemplateId != request.TemplateId) return Conflict(new { error = "request_id_reused" });
            if (winner is null && periodKey is not null) winner = await FindPeriod(notebook.Id, version.TemplateId, periodKey, ct);
            if (winner is null) throw;
            return Ok(PageView(winner));
        }
        return Ok(PageView(doc));
    }

    private Task<NotebookDocument?> FindPeriod(Guid notebookId, Guid templateId, string period, CancellationToken ct) =>
        db.NotebookDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.NotebookId == notebookId && d.TemplateId == templateId && d.PeriodKey == period, ct);
    private static bool ValidTitle(string? title) => !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= 200;
    private static NotebookTemplateView View(NotebookTemplate t, NotebookDocument d) => new(t.Id, t.Id, d.Title, t.Cadence, t.IsActive, t.Version, d.UpdatedAtUtc);
    private static NotebookPageSummary PageView(NotebookDocument d) => new(d.Id, d.NotebookId!.Value, d.Title, d.ParentPageId,
        d.Kind, JsonSerializer.Deserialize<string[]>(d.TagsJson) ?? [], d.PeriodStart, d.HoldId, d.TemplateId, d.UpdatedAtUtc, d.Revision);
    private ObjectResult Unavailable() => StatusCode(503, new { error = "document_service_unavailable", message = "Dokumentet kunne ikke oprettes. Prøv igen om lidt." });
    private ObjectResult DrawingFailure(DrawingException error) => StatusCode(error.Status, new { error = error.Code, message = error.Message });
}
