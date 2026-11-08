using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Mcp;

public sealed class NotionMcpException(string code, int status = 400, string? detail = null) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public string? Detail { get; } = detail;
}

/// <summary>All MCP and browser history requests share user authorization and durable intent storage.</summary>
public sealed class NotionMcpService(AppDbContext db, CourseAccess courses, IHttpClientFactory clients,
    IConfiguration configuration, NotebookServiceKey serviceKey)
{
    private readonly NotionAccess access = new(db, courses);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 512 };
    private static JsonElement Empty => JsonSerializer.SerializeToElement(new { }, Json);

    public static Guid RequireTeacher(ClaimsPrincipal user)
    {
        if (UserClaims.GetSub(user) is not Guid sub) throw new NotionMcpException("unauthorized", 401);
        if (!UserClaims.IsTeacher(user)) throw new NotionMcpException("forbidden", 403);
        return sub;
    }

    public async Task<object> FindAsync(ClaimsPrincipal user, string? query, Guid? holdId, Guid? collectionId,
        int offset, int limit, CancellationToken ct)
    {
        RequireTeacher(user);
        query = query?.Trim();
        if (query?.Length > 200 || offset < 0 || limit is < 1 or > 100) throw new NotionMcpException("invalid_search");
        var holds = courses.AccessibleHolds(user, true).Select(h => h.Id);
        var pages = access.ReadablePages(user).AsNoTracking().Where(p => p.HoldId == null || holds.Contains(p.HoldId.Value));
        if (holdId is Guid hold) pages = pages.Where(p => p.HoldId == hold);
        if (collectionId is Guid collection) pages = pages.Where(p => p.CollectionId == collection || p.Id == collection);
        if (!string.IsNullOrEmpty(query)) { var term = query.ToLowerInvariant(); pages = pages.Where(p => p.Title.ToLower().Contains(term)); }
        var total = await pages.CountAsync(ct);
        var rows = await pages.OrderBy(p => p.Title).ThenBy(p => p.Id).Skip(offset).Take(limit)
            .Select(p => new { pageId = p.Id, p.Title, p.HoldId, p.CollectionId, p.ParentPageId, p.UpdatedAtUtc }).ToArrayAsync(ct);
        return new { items = rows.Select(p => new { p.pageId, p.Title, p.HoldId, p.CollectionId, p.ParentPageId, p.UpdatedAtUtc, href = $"/notion/{p.pageId}" }), total, nextOffset = offset + rows.Length < total ? (int?)(offset + rows.Length) : null };
    }

    public async Task<JsonElement> ReadAsync(ClaimsPrincipal user, string token, string page, string? cursor, int limit, CancellationToken ct)
    {
        var id = ResolvePage(page);
        await AuthorizeAsync(id, user, ct);
        if (limit is < 1 or > 100 || cursor?.Length > 512) throw new NotionMcpException("invalid_read");
        var result = await EngineAsync("read", new { pageId = id, accessToken = token, cursor, limit }, ct);
        return JsonSerializer.SerializeToElement(new { pageId = id, title = await db.NotionPages.Where(p => p.Id == id).Select(p => p.Title).SingleAsync(ct), href = $"/notion/{id}", document = result }, Json);
    }

    public async Task<NotionMcpChangeView> ApplyAsync(ClaimsPrincipal user, string token, ApplyNotionMcpChanges request, CancellationToken ct)
    {
        var owner = await AuthorizeAsync(request.PageId, user, ct);
        if (request.OperationId == Guid.Empty || request.Changes is not { Length: > 0 and <= 500 }
            || request.Changes.Any(c => c.ValueKind != JsonValueKind.Object) || request.Summary?.Length > 300)
            throw new NotionMcpException("invalid_change");
        var intent = JsonSerializer.SerializeToElement(request, Json);
        if (Encoding.UTF8.GetByteCount(intent.GetRawText()) > 8 * 1024 * 1024) throw new NotionMcpException("change_too_large", 413);
        var row = await IntentAsync(request.OperationId, request.PageId, owner, intent, request.Summary, null, ct);
        if (row.Status is "applied" or "undone") return View(row);
        await BeginAttemptAsync(row, ct);
        try
        {
            await EngineAsync("apply", new { pageId = row.PageId, accessToken = token, actorSub = owner,
                operationId = row.Id, requestHash = row.RequestHash, summary = row.Summary, changes = request.Changes }, ct);
            await db.Entry(row).ReloadAsync(ct);
            if (row.Status != "applied") throw new NotionMcpException("change_not_durable", 503);
            return View(row);
        }
        catch (NotionMcpException error)
        {
            await RecordErrorAsync(row, error, ct);
            throw;
        }
    }

    public async Task<NotionMcpChangeSummary[]> ListAsync(Guid pageId, ClaimsPrincipal user, CancellationToken ct)
    {
        var sub = await AuthorizeAsync(pageId, user, ct);
        var metadata = db.NotionMcpChanges.AsNoTracking().Where(c => c.PageId == pageId && c.OwnerSub == sub)
            .Select(c => new NotionMcpChangeSummary(c.Id, c.PageId, c.Summary, c.Status, c.CreatedAtUtc,
                c.AppliedAtUtc, c.UndoneAtUtc, c.UndoOf, c.Status == "applied" && c.UndoOf == null));
        // SQLite test storage cannot order DateTimeOffset; production PostgreSQL limits before materialization.
        return db.Database.IsNpgsql()
            ? await metadata.OrderByDescending(c => c.CreatedAtUtc).Take(100).ToArrayAsync(ct)
            : (await metadata.ToArrayAsync(ct)).OrderByDescending(c => c.CreatedAtUtc).Take(100).ToArray();
    }

    public async Task<NotionMcpChangeView> GetAsync(Guid operationId, ClaimsPrincipal user, CancellationToken ct)
        => View(await OwnedAsync(operationId, user, ct));

    public async Task<NotionMcpChangeView> GetStatusAsync(Guid operationId, ClaimsPrincipal user, CancellationToken ct)
    {
        var sub = RequireTeacher(user);
        var row = await db.NotionMcpChanges.AsNoTracking().Where(c => c.Id == operationId && c.OwnerSub == sub)
            .Select(c => new { c.Id, c.PageId, c.Summary, c.Status, c.CreatedAtUtc, c.AppliedAtUtc, c.UndoneAtUtc, c.UndoOf, c.Error })
            .SingleOrDefaultAsync(ct) ?? throw new NotionMcpException("change_not_found", 404);
        await AuthorizeAsync(row.PageId, user, ct);
        return new(row.Id, row.PageId, row.Summary, row.Status, row.CreatedAtUtc, row.AppliedAtUtc,
            row.UndoneAtUtc, row.UndoOf, row.Status == "applied" && row.UndoOf == null, Empty, Empty, row.Error);
    }

    public async Task<NotionMcpChangeView> UndoAsync(Guid operationId, Guid reversalId, ClaimsPrincipal user, string token, CancellationToken ct)
    {
        var original = await OwnedAsync(operationId, user, ct);
        if (reversalId == Guid.Empty || operationId == reversalId || original.UndoOf != null)
            throw new NotionMcpException("invalid_undo");
        if (original.Status == "undone") return View(original);
        if (original.Status != "applied" || original.BeforeJson is null || original.AfterJson is null)
            throw new NotionMcpException("change_not_applied", 409);
        var existing = await db.NotionMcpChanges.AsNoTracking().SingleOrDefaultAsync(c => c.UndoOf == operationId, ct);
        // A reloaded browser may no longer know its first request ID. Resume the one durable
        // reversal for this operation, including a lost acknowledgement, rather than strand it.
        if (existing != null) reversalId = existing.Id;
        var intent = JsonSerializer.SerializeToElement(new { operationId, reversalId }, Json);
        var row = await IntentAsync(reversalId, original.PageId, original.OwnerSub, intent, "Fortryd: " + original.Summary[..Math.Min(290, original.Summary.Length)], operationId, ct);
        await BeginAttemptAsync(row, ct);
        try
        {
            await EngineAsync("undo", new { pageId = row.PageId, accessToken = token, actorSub = row.OwnerSub,
                operationId = row.Id, requestHash = row.RequestHash, summary = row.Summary, undoOf = operationId,
                originalChange = new { before = Parse(original.BeforeJson), after = Parse(original.AfterJson) } }, ct);
            await db.Entry(original).ReloadAsync(ct);
            if (original.Status != "undone") throw new NotionMcpException("change_not_durable", 503);
            return View(original);
        }
        catch (NotionMcpException error) { await RecordErrorAsync(row, error, ct); throw; }
    }

    public async Task PrepareAsync(Guid pageId, PrepareNotionMcpChange request, CancellationToken ct)
    {
        var row = await db.NotionMcpChanges.SingleOrDefaultAsync(c => c.Id == request.OperationId, ct);
        if (row == null || row.PageId != pageId || row.OwnerSub != request.OwnerSub || row.RequestHash != request.RequestHash
            || row.UndoOf != request.UndoOf) throw new NotionMcpException("operation_mismatch", 409);
        if (request.Before.ValueKind != JsonValueKind.Object || request.After.ValueKind != JsonValueKind.Object)
            throw new NotionMcpException("invalid_audit");
        var before = Canonical(request.Before); var after = Canonical(request.After);
        if (Encoding.UTF8.GetByteCount(before) + Encoding.UTF8.GetByteCount(after) > 24 * 1024 * 1024) throw new NotionMcpException("change_too_large", 413);
        if (row.BeforeJson != null && (row.BeforeJson != before || row.AfterJson != after))
            throw new NotionMcpException("operation_mismatch", 409);
        row.BeforeJson = before; row.AfterJson = after;
        await db.SaveChangesAsync(ct);
    }

    private async Task<NotionMcpChange> IntentAsync(Guid id, Guid page, Guid owner, JsonElement intent, string? summary, Guid? undoOf, CancellationToken ct)
    {
        var canonical = Canonical(intent); var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var row = await db.NotionMcpChanges.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (row == null)
        {
            row = new() { Id = id, PageId = page, OwnerSub = owner, RequestHash = hash, RequestJson = canonical,
                Summary = string.IsNullOrWhiteSpace(summary) ? "Codex-ændring" : summary.Trim(), UndoOf = undoOf, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.NotionMcpChanges.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(row).State = EntityState.Detached;
                row = await db.NotionMcpChanges.SingleOrDefaultAsync(c => c.Id == id, ct);
                if (row == null) throw new NotionMcpException("operation_in_progress", 409);
            }
        }
        if (row.OwnerSub != owner || row.PageId != page || row.RequestHash != hash || row.UndoOf != undoOf)
            throw new NotionMcpException("operation_mismatch", 409);
        return row;
    }

    private async Task<Guid> AuthorizeAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var sub = RequireTeacher(user);
        if (id == Guid.Empty || !await access.CanManageAsync(id, user, ct)) throw new NotionMcpException("page_not_found", 404);
        return sub;
    }

    private async Task<NotionMcpChange> OwnedAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var sub = RequireTeacher(user);
        var row = await db.NotionMcpChanges.SingleOrDefaultAsync(c => c.Id == id && c.OwnerSub == sub, ct)
            ?? throw new NotionMcpException("change_not_found", 404);
        await AuthorizeAsync(row.PageId, user, ct); return row;
    }

    private async Task<JsonElement> EngineAsync(string action, object body, CancellationToken ct)
    {
        var key = serviceKey.Read() ?? throw new NotionMcpException("document_service_unavailable", 503);
        var origin = configuration["Notebook:CollaborationBaseUrl"] ?? "http://mercantec-tech-collaboration:1234";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(origin), "/internal/notion/mcp/" + action));
        request.Headers.Add("X-Notion-Service-Key", key);
        request.Content = JsonContent.Create(body, options: Json);
        HttpResponseMessage response;
        try { response = await clients.CreateClient("notion-mcp").SendAsync(request, ct); }
        catch (HttpRequestException) { throw new NotionMcpException("document_service_unavailable", 503); }
        using (response)
        {
            await response.Content.LoadIntoBufferAsync(32 * 1024 * 1024, ct);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
            if (!response.IsSuccessStatusCode)
            {
                var code = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.String && error.GetString() is { Length: <= 120 } name ? name : "document_service_error";
                var detail = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("detail", out var description)
                    && description.ValueKind == JsonValueKind.String && description.GetString() is { Length: <= 1000 } message ? message : null;
                throw new NotionMcpException(code, response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Conflict or HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.RequestEntityTooLarge ? (int)response.StatusCode : 503, detail);
            }
            return result;
        }
    }

    public Guid ResolvePage(string value)
    {
        if (Guid.TryParse(value, out var id) && id != Guid.Empty) return id;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var publicUri = NotionMcpEndpoints.PublicUri(configuration);
            if (publicUri == null || uri.Scheme != Uri.UriSchemeHttps || uri.Host != publicUri.Host || uri.Port != publicUri.Port
                || !string.IsNullOrEmpty(uri.UserInfo)) throw new NotionMcpException("invalid_page_url");
            value = uri.AbsolutePath;
        }
        var path = value.Split('?', '#')[0].TrimEnd('/').Split('/');
        if (path.Length == 3 && path[0] == "" && path[1] == "notion" && Guid.TryParse(path[2], out id) && id != Guid.Empty) return id;
        throw new NotionMcpException("invalid_page_url");
    }

    private async Task BeginAttemptAsync(NotionMcpChange row, CancellationToken ct)
    {
        if (row.Status == "pending") return;
        row.Status = "pending"; row.Error = null;
        await db.SaveChangesAsync(ct);
    }
    private async Task RecordErrorAsync(NotionMcpChange row, NotionMcpException error, CancellationToken ct)
    {
        // A retry uses the same durable intent and rechecks the live document. Never replace applied audit state.
        await db.Entry(row).ReloadAsync(ct);
        if (row.Status == "pending")
        {
            row.Error = error.Code[..Math.Min(error.Code.Length, 120)];
            row.Status = error.Status == 409 ? "conflict" : error.Status is >= 400 and < 500 ? "failed" : "pending";
            await db.SaveChangesAsync(ct);
        }
    }
    private static JsonElement Parse(string? text) => text == null ? Empty : JsonSerializer.Deserialize<JsonElement>(text, Json);
    public static NotionMcpChangeSummary Summary(NotionMcpChange row) => new(row.Id, row.PageId, row.Summary, row.Status,
        row.CreatedAtUtc, row.AppliedAtUtc, row.UndoneAtUtc, row.UndoOf, row.Status == "applied" && row.UndoOf == null);
    public static NotionMcpChangeView View(NotionMcpChange row) => new(row.Id, row.PageId, row.Summary, row.Status,
        row.CreatedAtUtc, row.AppliedAtUtc, row.UndoneAtUtc, row.UndoOf, row.Status == "applied" && row.UndoOf == null,
        Parse(row.BeforeJson), Parse(row.AfterJson), row.Error);
    public static string Canonical(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", value.EnumerateArray().Select(Canonical)) + "]",
        _ => value.GetRawText()
    };
}
