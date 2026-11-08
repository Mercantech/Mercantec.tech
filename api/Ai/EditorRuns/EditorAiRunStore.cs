using System.Security.Claims;
using System.Text.Json;
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

public sealed class EditorAiRunStore(AppDbContext db, CourseAccess courses,
    IEditorAiContextService context, IOptions<OpenAiOptions> options)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 128 };
    public static readonly string[] ActiveStates = ["queued", "gathering", "generating"];

    public async Task<EditorAiRun?> OwnedAsync(Guid id, ClaimsPrincipal user, CancellationToken ct, bool sources = true)
    {
        if (!UserClaims.IsTeacher(user) || UserClaims.GetSub(user) is not Guid sub) return null;
        var run = await db.EditorAiRuns.Include(r => r.Hunks).SingleOrDefaultAsync(r => r.Id == id && r.OwnerSub == sub, ct);
        if (run is null || run.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || !await new NotionAccess(db, courses).CanManageAsync(run.PageId, user, ct)) return null;
        if (sources)
            foreach (var source in Deserialize<EditorAiSource[]>(run.SourcesJson, []))
                if (!await context.CanReadSourceAsync(user, source, ct)) throw new EditorAiAccessChangedException();
        return run;
    }

    public async Task<EditorAiRun> CreateAsync(EditorAiRunRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        ValidateRequest(request);
        if (!UserClaims.IsTeacher(user) || UserClaims.GetSub(user) is not Guid sub
            || !await new NotionAccess(db, courses).CanManageAsync(request.PageId, user, ct))
            throw new UnauthorizedAccessException();
        if (request.ClientRequestId is Guid key)
        {
            var prior = await db.EditorAiRuns.Include(r => r.Hunks)
                .SingleOrDefaultAsync(r => r.OwnerSub == sub && r.ClientRequestId == key, ct);
            if (prior is not null)
            {
                if (prior.PageId != request.PageId) throw new ArgumentException("request_id_changed");
                return await OwnedAsync(prior.Id, user, ct) ?? throw new UnauthorizedAccessException();
            }
        }
        if (await db.EditorAiRuns.AnyAsync(r => r.OwnerSub == sub && r.PageId == request.PageId && ActiveStates.Contains(r.Status), ct))
            throw new InvalidOperationException("run_in_progress");
        var now = DateTimeOffset.UtcNow;
        var run = new EditorAiRun
        {
            Id = Guid.NewGuid(), ClientRequestId = request.ClientRequestId ?? Guid.NewGuid(), OwnerSub = sub,
            OwnerRole = user.Claims.Any(c => (c.Type is "role" or "roles" || c.Type == ClaimTypes.Role) && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)) ? "Admin" : "Teacher",
            PageId = request.PageId, Instruction = request.Instruction.Trim(), Mode = request.Mode, Scope = request.Scope,
            DocumentJson = request.Document.GetRawText(), TargetJson = request.Target?.GetRawText() ?? "null",
            ExcludedSourceIdsJson = Serialize(request.ExcludedSourceIds ?? []),
            Model = options.Value.EditorModel, CreatedAtUtc = now, UpdatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(Math.Clamp(options.Value.EditorRetentionDays, 1, 90))
        };
        db.EditorAiRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await PublishAsync(run, ct);
        return run;
    }

    public async Task<EditorAiRunView> ViewAsync(EditorAiRun run, CancellationToken ct)
    {
        var cursor = await db.EditorAiRunEvents.Where(e => e.RunId == run.Id).Select(e => (long?)e.Id).MaxAsync(ct) ?? 0;
        return View(run, cursor);
    }

    public static EditorAiRunView View(EditorAiRun run, long cursor) => new(run.Id, run.PageId, run.Instruction,
        run.Mode, run.Scope, run.Status,
        run.Hunks.OrderBy(h => h.Order).Select(h => new EditorAiHunkView(h.Id, h.Version, h.Order, h.Operation,
            Deserialize<string[]>(h.TargetBlockIdsJson, []), h.BaseFingerprint, Deserialize<JsonElement[]>(h.BeforeJson, []),
            Deserialize<JsonElement[]>(h.AfterJson, []), h.Status, h.Explanation, Deserialize<string[]>(h.SourceIdsJson, []),
            Deserialize<JsonElement?>(h.TargetJson, null))).ToArray(),
        Deserialize<EditorAiSource[]>(run.SourcesJson, []),
        Deserialize(run.CoverageJson, new EditorAiCoverage(0, 0, 0, 0, false)), run.Progress, run.Error,
        run.Model, cursor, run.OutputTokens, run.CreatedAtUtc, run.ExpiresAtUtc);

    public async Task PublishAsync(EditorAiRun run, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        run.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        var entry = new EditorAiRunEvent { RunId = run.Id, Type = "run", DataJson = Serialize(View(run, 0)), CreatedAtUtc = DateTimeOffset.UtcNow };
        // Event identity is assigned by the database, never by concurrent worker/controller counters.
        db.EditorAiRunEvents.Add(entry);
        await db.SaveChangesAsync(ct);
        entry.DataJson = Serialize(View(run, entry.Id));
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    public static void ValidateRequest(EditorAiRunRequest request)
    {
        if (request.PageId == Guid.Empty || string.IsNullOrWhiteSpace(request.Instruction)) throw new ArgumentException("instruction_required");
        if (request.Instruction.Length > 18000) throw new ArgumentException("instruction_too_large");
        if (request.Mode is not ("generate" or "rewrite" or "mermaid") || request.Scope is not ("selection" or "section" or "page"))
            throw new ArgumentException("invalid_scope");
        if (request.Document.ValueKind != JsonValueKind.Array || request.Document.GetRawText().Length > 8 * 1024 * 1024)
            throw new ArgumentException("document_too_large");
        if (request.Target is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Object) }) throw new ArgumentException("invalid_target");
        if (request.ExcludedSourceIds is { Length: > 256 }) throw new ArgumentException("too_many_excluded_sources");
        _ = EditorAiChangeValidation.Blocks(request.Document);
    }

    public static EditorAiRunRequest Request(EditorAiRun run) => new(run.PageId, run.Instruction, run.Mode, run.Scope,
        Parse(run.DocumentJson), Deserialize<JsonElement?>(run.TargetJson, null), Deserialize<string[]>(run.ExcludedSourceIdsJson, []));
    public static JsonElement Parse(string value) { using var doc = JsonDocument.Parse(value, new() { MaxDepth = 128 }); return doc.RootElement.Clone(); }
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Deserialize<T>(string value, T fallback) => JsonSerializer.Deserialize<T>(value, Json) ?? fallback;
}

public sealed class EditorAiAccessChangedException : Exception { }
