using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

/// <summary>Database-backed work leases make generation independent of requests and browser lifetime.</summary>
public sealed class EditorAiRunWorker(IServiceScopeFactory scopes, IOptions<OpenAiOptions> options,
    ILogger<EditorAiRunWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        Enumerable.Range(0, Math.Clamp(options.Value.EditorWorkerConcurrency, 1, 8)).Select(_ => Loop(stoppingToken)));

    private async Task Loop(CancellationToken ct)
    {
        var nextRecovery = DateTimeOffset.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow >= nextRecovery)
                { await RecoverAsync(ct); nextRecovery = DateTimeOffset.UtcNow.AddSeconds(30); }
                if (!await ProcessNextAsync(ct)) await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Editor AI queue error: {ExceptionType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    public async Task RecoverAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<EditorAiRunStore>();
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.EditorAiRuns.Include(r => r.Hunks).Where(r => r.Status == "gathering" || r.Status == "generating").ToListAsync(ct);
        foreach (var run in candidates.Where(r => r.LeaseExpiresAtUtc is null || r.LeaseExpiresAtUtc < now))
        {
            var lease = run.LeaseId;
            var expiryAtRead = run.LeaseExpiresAtUtc;
            var changed = await db.EditorAiRuns.Where(r => r.Id == run.Id && r.LeaseId == lease && r.LeaseExpiresAtUtc == expiryAtRead && (r.Status == "gathering" || r.Status == "generating"))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "needs_continuation").SetProperty(r => r.LeaseId, (Guid?)null)
                    .SetProperty(r => r.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(r => r.Progress, "Serveren blev genstartet. Færdige forslag er bevaret; vælg Fortsæt."), ct);
            if (changed == 1)
            {
                db.Entry(run).Reload(); await store.PublishAsync(run, ct);
            }
        }
        if (db.Database.IsNpgsql()) await db.EditorAiRuns.Where(r => r.ExpiresAtUtc <= now).ExecuteDeleteAsync(ct);
        else
        {
            // SQLite cannot translate DateTimeOffset ordering/comparison; keep the production cleanup in SQL.
            var expiry = await db.EditorAiRuns.AsNoTracking().Select(r => new { r.Id, r.ExpiresAtUtc }).ToListAsync(ct);
            var expired = expiry.Where(r => r.ExpiresAtUtc <= now).Select(r => r.Id).ToArray();
            if (expired.Length > 0) await db.EditorAiRuns.Where(r => expired.Contains(r.Id)).ExecuteDeleteAsync(ct);
        }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queued = db.EditorAiRuns.AsNoTracking().Where(r => r.Status == "queued");
        if (db.Database.IsNpgsql()) queued = queued.OrderBy(r => r.CreatedAtUtc);
        var candidateIds = await queued.Select(r => new { r.Id, r.CreatedAtUtc }).Take(100).ToListAsync(ct);
        var lease = Guid.NewGuid(); Guid? id = null;
        foreach (var candidate in candidateIds.OrderBy(r => r.CreatedAtUtc))
        {
            var now = DateTimeOffset.UtcNow;
            if (await db.EditorAiRuns.Where(r => r.Id == candidate.Id && r.Status == "queued")
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "gathering").SetProperty(r => r.LeaseId, lease)
                    .SetProperty(r => r.LeaseExpiresAtUtc, now.AddSeconds(60)).SetProperty(r => r.AttemptStartedAtUtc, now), ct) == 1)
            { id = candidate.Id; break; }
        }
        if (id is null) return false;
        await Generate(scope.ServiceProvider, id.Value, lease, ct);
        return true;
    }

    private async Task Generate(IServiceProvider services, Guid id, Guid lease, CancellationToken hostCt)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var store = services.GetRequiredService<EditorAiRunStore>();
        var context = services.GetRequiredService<IEditorAiContextService>();
        var provider = services.GetRequiredService<EditorAiResponseStream>();
        var run = await db.EditorAiRuns.Include(r => r.Hunks).SingleAsync(r => r.Id == id, hostCt);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", run.OwnerSub.ToString()), new Claim("role", run.OwnerRole)], "editor-worker"));
        var settings = options.Value;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        deadline.CancelAfter(TimeSpan.FromMinutes(Math.Clamp(settings.EditorRunMinutes, 1, 60)));
        using var monitorStop = new CancellationTokenSource();
        var pulse = Monitor(id, lease, deadline, monitorStop.Token);
        var ct = deadline.Token;
        try
        {
            await CheckAccess(services, user, run, lease, ct);
            run.Progress = "Læser den aktive side og relevante kilder…"; await store.PublishAsync(run, ct);
            async Task<EditorAiContextBundle> BuildBundle()
            {
                var request = EditorAiRunStore.Request(run);
                var target = JsonNode.Parse(run.TargetJson) as JsonObject ?? new JsonObject();
                target.Remove("contextCursorBlockId"); // Only the durable server cursor may choose a context window.
                var adjustmentIds = EditorAiRunStore.Deserialize<Guid[]>(run.AdjustHunkIdsJson, []);
                var cursor = run.ContextCursorBlockId;
                if (adjustmentIds.Length > 0)
                {
                    var ids = run.Hunks.Where(h => adjustmentIds.Contains(h.Id)).SelectMany(h => EditorAiRunStore.Deserialize<string[]>(h.TargetBlockIdsJson, [])).ToHashSet();
                    target["blockIds"] = JsonSerializer.SerializeToNode(ids);
                    cursor = EditorAiContextService.FindSectionCursor(request.Document, ids);
                }
                if (run.Scope == "page" && cursor is not null) target["contextCursorBlockId"] = cursor;
                var value = await context.BuildAsync(user, run.PageId, request with { Target = JsonSerializer.SerializeToElement(target) }, ct);
                var used = EditorAiRunStore.Deserialize<EditorAiSource[]>(run.SourcesJson, []).Concat(value.Sources).GroupBy(s => s.Id).Select(g => g.Last()).ToArray();
                foreach (var source in used)
                    if (!await context.CanReadSourceAsync(user, source, ct)) throw new EditorAiAccessChangedException();
                run.SourcesJson = EditorAiRunStore.Serialize(used); run.CoverageJson = EditorAiRunStore.Serialize(value.Coverage);
                return value;
            }
            var bundle = await BuildBundle();
            run.Status = "generating"; run.Progress = "Skriver forslag direkte til siden…"; await store.PublishAsync(run, ct);
            var blocks = EditorAiChangeValidation.Blocks(EditorAiRunStore.Parse(run.DocumentJson));
            var tokenBudget = Math.Clamp(settings.EditorRunTokens, 1024, 1048576);
            var maxBatches = Math.Clamp(settings.EditorMaxBatches, 1, 512);
            var done = false; var noProgressBatches = 0; var revised = new HashSet<Guid>(); string? pauseMessage = null;
            while (run.AttemptTokens < tokenBudget && run.AttemptBatchCount < maxBatches)
            {
                if (bundle.ActiveSectionBlocked)
                {
                    run.Error = "active_section_too_large";
                    pauseMessage = "En blok er større end læsebudgettet. Færdige forslag er gemt; opdel blokken i mindre blokke for at arbejde videre.";
                    break;
                }
                await CheckAccess(services, user, run, lease, ct);
                foreach (var source in bundle.Sources)
                    if (!await context.CanReadSourceAsync(user, source, ct)) throw new EditorAiAccessChangedException();
                var prior = History(run);
                if (run.Hunks.Count > 0)
                {
                    var coverage = EditorAiRunStore.Deserialize(run.CoverageJson, new EditorAiCoverage(0, 0, 0, 0, false));
                    const string summary = "Tidligere forslag er opsummeret i fortsættelseskonteksten; de fulde forslag er bevaret.";
                    if (!(coverage.Detail ?? "").Contains(summary, StringComparison.Ordinal))
                        run.CoverageJson = EditorAiRunStore.Serialize(coverage with { Detail = ((coverage.Detail ?? "") + " " + summary).Trim() });
                }
                var adjustment = EditorAiRunStore.Deserialize<Guid[]>(run.AdjustHunkIdsJson, []);
                var instruction = "EDITOR TASK: " + EditorAiRunStore.Serialize(new { mode = run.Mode, scope = run.Scope }) + "\n"
                    + (run.Scope == "selection" ? "Brug kun replace_selection med præcis de ordnede markerede blockIds; bevar alt uden for markeringen.\n" : "Arbejd kun inden for det angivne redigeringsområde.\n")
                    + (run.Mode == "mermaid" ? "Udarbejd et gyldigt Mermaid-diagram som mermaidDiagram-blokke.\n" : run.Mode == "rewrite" ? "Omskriv de relevante eksisterende dele efter instruktionen.\n" : "Generér det ønskede nye indhold.\n")
                    + run.Instruction + (adjustment.Length == 0 ? "" : "\nJustér kun historikkens forslag med adjustmentAllowed=true; bevar deres hunkId og mål præcist.");
                // Reserve the entire call budget before sending. Even an interrupted stream counts against the task budget.
                var reservation = Math.Min(Math.Clamp(settings.EditorResponseTokens, 1024, 131072), tokenBudget - run.AttemptTokens);
                var beforeVersions = run.Hunks.Sum(h => h.Version);
                run.AttemptTokens += reservation; run.OutputTokens += reservation; run.AttemptBatchCount++; run.BatchCount++;
                await store.PublishAsync(run, ct);
                var result = await provider.GenerateAsync(instruction, bundle.ContextText, prior, reservation, async change =>
                {
                    await CheckAccess(services, user, run, lease, ct);
                    foreach (var source in bundle.Sources)
                        if (!await context.CanReadSourceAsync(user, source, ct)) throw new EditorAiAccessChangedException();
                    await db.Entry(run).Collection(r => r.Hunks).LoadAsync(ct);
                    foreach (var h in run.Hunks.ToArray()) await db.Entry(h).ReloadAsync(ct);
                    if (change.Operation is "replace" or "delete" or "replace_selection"
                        && bundle.WritableBlockIds is { } writable && change.TargetBlockIds.Any(id => !writable.Contains(id)))
                        throw new ArgumentException("target_not_fully_read");
                    var hunk = EditorAiChangeValidation.Validate(change, run, blocks, bundle.Sources);
                    if (run.Hunks.Any(h => h.Signature == hunk.Signature && !adjustment.Contains(h.Id))) return;
                    var existing = run.Hunks.FirstOrDefault(h => adjustment.Contains(h.Id) && h.Id == hunk.Id);
                    if (existing is not null)
                    {
                        if (existing.Status is "accepted" or "rejected") throw new InvalidOperationException("hunk_already_reviewed");
                        if (existing.Signature == hunk.Signature) return;
                        if (!revised.Add(existing.Id)) throw new ArgumentException("duplicate_adjustment_hunk");
                        existing.Version++; existing.Operation = hunk.Operation; existing.BeforeJson = hunk.BeforeJson;
                        existing.AfterJson = hunk.AfterJson; existing.BaseFingerprint = hunk.BaseFingerprint;
                        existing.TargetJson = hunk.TargetJson; existing.SourceIdsJson = hunk.SourceIdsJson;
                        existing.Explanation = hunk.Explanation; existing.Signature = hunk.Signature; existing.Status = "ready";
                    }
                    else { run.Hunks.Add(hunk); db.EditorAiRunHunks.Add(hunk); }
                    run.Progress = $"{run.Hunks.Count(h => h.Status == "ready")} forslag klar til gennemgang…";
                    await store.PublishAsync(run, ct);
                }, _ => Task.CompletedTask, ct);
                var charged = Math.Clamp(result.OutputTokens, 0, reservation);
                run.AttemptTokens -= reservation - charged; run.OutputTokens -= reservation - charged;
                await store.PublishAsync(run, ct);
                if (!result.HasMore && !result.Incomplete)
                {
                    if (run.Scope == "page" && adjustment.Length == 0 && bundle.NextSectionBlockId is { } next)
                    {
                        if (next == run.ContextCursorBlockId) throw new InvalidOperationException("context_cursor_did_not_advance");
                        run.ContextCursorBlockId = next; run.Progress = "Læser næste sektion af den aktive side…";
                        await store.PublishAsync(run, ct); bundle = await BuildBundle(); await store.PublishAsync(run, ct);
                        noProgressBatches = 0; continue;
                    }
                    done = true; run.Progress = string.IsNullOrWhiteSpace(result.Note) ? "Forslagene er klar til gennemgang." : result.Note; break;
                }
                noProgressBatches = run.Hunks.Sum(h => h.Version) > beforeVersions ? 0 : noProgressBatches + 1;
                if (noProgressBatches >= 3) break;
            }
            await CheckAccess(services, user, run, lease, ct);
            run.Status = done ? "ready" : "needs_continuation";
            if (!done) run.Progress = pauseMessage ?? "Opgavens budget er nået. Færdige forslag er gemt; vælg Fortsæt.";
            run.LeaseId = null; run.LeaseExpiresAtUtc = null;
            if (done) run.AdjustHunkIdsJson = "[]";
            await store.PublishAsync(run, ct);
        }
        catch (Exception ex)
        {
            // Persist with an independent bounded token: request/host cancellation must not erase completed hunks.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            db.ChangeTracker.Clear();
            var current = await db.EditorAiRuns.Include(r => r.Hunks).SingleOrDefaultAsync(r => r.Id == id, cleanup.Token);
            if (current is not null && current.LeaseId == lease)
            {
                current.Status = ex is EditorAiAccessChangedException ? "failed" : "needs_continuation";
                current.Error = ex switch
                {
                    EditorAiAccessChangedException => "source_access_changed",
                    UnauthorizedAccessException => "page_access_changed",
                    ArgumentException => "invalid_change",
                    OperationCanceledException => hostCt.IsCancellationRequested ? "server_stopped" : "time_budget_reached",
                    HttpRequestException => "provider_unavailable",
                    _ => "generation_interrupted"
                };
                current.Progress = ex is EditorAiAccessChangedException or UnauthorizedAccessException
                    ? "Adgangen er ændret. Opgaven er stoppet."
                    : "Opgaven er sat på pause. Færdige forslag er bevaret; vælg Fortsæt.";
                if (ex is ArgumentException argument && argument.Message == "target_context_too_large_split_selection")
                {
                    current.Error = "target_context_too_large_split_selection";
                    current.Progress = "Markeringen er større end læsebudgettet. Markér et mindre område, og start en ny opgave. Færdige forslag er bevaret.";
                }
                current.LeaseId = null; current.LeaseExpiresAtUtc = null;
                await store.PublishAsync(current, cleanup.Token);
                logger.LogInformation("Editor AI run {RunId} paused: {Reason}", id, current.Error);
            }
        }
        finally
        {
            monitorStop.Cancel();
            try { await pulse; } catch (OperationCanceledException) { }
        }
    }

    private static async Task CheckAccess(IServiceProvider services, ClaimsPrincipal user, EditorAiRun run, Guid lease, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var current = await db.EditorAiRuns.AsNoTracking().Where(r => r.Id == run.Id).Select(r => new { r.LeaseId, r.Status }).SingleOrDefaultAsync(ct);
        if (current is null || current.LeaseId != lease || current.Status == "cancelled") throw new OperationCanceledException(ct);
        if (!await new NotionAccess(db, services.GetRequiredService<CourseAccess>()).CanManageAsync(run.PageId, user, ct))
            throw new UnauthorizedAccessException();
    }

    private async Task Monitor(Guid id, Guid lease, CancellationTokenSource generation, CancellationToken ct)
    {
        try
        {
          while (!ct.IsCancellationRequested && !generation.IsCancellationRequested)
          {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var renewed = await db.EditorAiRuns.Where(r => r.Id == id && r.LeaseId == lease && (r.Status == "gathering" || r.Status == "generating"))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.LeaseExpiresAtUtc, DateTimeOffset.UtcNow.AddSeconds(60)), ct);
            if (renewed == 0) { generation.Cancel(); return; }
          }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        { logger.LogWarning("Editor AI lease renewal stopped: {ExceptionType}", ex.GetType().Name); generation.Cancel(); }
    }

    private static string History(EditorAiRun run)
    {
        var remaining = Math.Max(256, 24000 - EditorAiResponseStream.SystemPrompt.Length - run.Instruction.Length - 300);
        var summaries = new List<object>(); var used = 120;
        var adjusted = EditorAiRunStore.Deserialize<Guid[]>(run.AdjustHunkIdsJson, []).ToHashSet();
        foreach (var hunk in run.Hunks.OrderByDescending(h => adjusted.Contains(h.Id)).ThenByDescending(h => h.Order))
        {
            var ids = EditorAiRunStore.Deserialize<string[]>(hunk.TargetBlockIdsJson, []);
            var after = hunk.AfterJson;
            var summary = new { hunk.Id, hunk.Version, hunk.Order, hunk.Operation, hunk.Status, adjustmentAllowed = adjusted.Contains(hunk.Id),
                targets = ids.Take(12), omittedTargets = Math.Max(0, ids.Length - 12),
                afterSummary = after.Length <= 900 ? after : after[..300] + " … " + after[^600..] };
            var size = EditorAiRunStore.Serialize(summary).Length;
            if (used + size > remaining) continue;
            summaries.Add(summary); used += size;
        }
        return EditorAiRunStore.Serialize(new { completedChanges = run.Hunks.Count, omittedSummaries = run.Hunks.Count - summaries.Count,
            summaryOnly = true, changes = summaries });
    }
}
