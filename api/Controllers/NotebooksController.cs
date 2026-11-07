using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notebooks")]
public sealed class NotebooksController(AppDbContext db, NotebookAccess access) : ControllerBase
{
    [HttpPost("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        await RegisterProfile(sub, ct);
        return NoContent();
    }

    [HttpPost("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var profile = await RegisterProfile(sub, ct);
        var notebook = await db.Notebooks.AsNoTracking().SingleOrDefaultAsync(n => n.OwnerSub == sub, ct);
        if (notebook is null)
        {
            notebook = new() { Id = Guid.NewGuid(), OwnerSub = sub, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.Notebooks.Add(notebook);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(notebook).State = EntityState.Detached;
                notebook = await db.Notebooks.AsNoTracking().SingleOrDefaultAsync(n => n.OwnerSub == sub, ct);
                if (notebook is null) throw;
            }
        }
        NoCache();
        return Ok(new NotebookInfo(notebook.Id, sub, profile.DisplayName, true, true));
    }

    [HttpGet("{notebookId:guid}/pages")]
    public async Task<IActionResult> Pages(Guid notebookId, CancellationToken ct, string? search = null,
        string? tag = null, string? kind = null, int offset = 0, int limit = 100)
    {
        if (!await CanOpenNotebook(notebookId, ct)) return NotFound();
        var query = access.ReadableDocuments(User).AsNoTracking().Where(d => d.NotebookId == notebookId);
        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(d => d.Kind == kind);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var token = JsonSerializer.Serialize(tag.Trim().ToLowerInvariant());
            query = query.Where(d => d.TagsJson.Contains(token));
        }
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            if (term.Length > 200) return BadRequest(new { error = "invalid_search" });
            if (db.Database.IsNpgsql())
                query = query.Where(d => EF.Functions.ToTsVector("simple", d.Title + " " + d.SearchText)
                    .Matches(EF.Functions.WebSearchToTsQuery("simple", term)));
            else
            {
                var lower = term.ToLowerInvariant();
                query = query.Where(d => d.Title.ToLower().Contains(lower) || d.SearchText.ToLower().Contains(lower));
            }
        }
        offset = Math.Max(0, offset); limit = Math.Clamp(limit, 1, 100);
        var total = await query.CountAsync(ct);
        List<PageData> rows;
        if (!db.Database.IsNpgsql())
            rows = (await Project(query).ToListAsync(ct)).OrderByDescending(d => d.UpdatedAtUtc).ThenBy(d => d.Id)
                .Skip(offset).Take(limit).ToList();
        else
            rows = await Project(query.OrderByDescending(d => d.UpdatedAtUtc).ThenBy(d => d.Id).Skip(offset).Take(limit)).ToListAsync(ct);
        var visible = await VisibleIds(notebookId, ct);
        NoCache();
        return Ok(new NotebookPageList(rows.Select(d => View(d, visible, term)).ToArray(), total));
    }

    [HttpGet("shared")]
    public async Task<IActionResult> Shared(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var query = access.ReadableDocuments(User).AsNoTracking().Where(d => d.NotebookId != null
            && d.Notebook!.OwnerSub != sub && db.NotebookShares.Any(s => s.RecipientSub == sub
                && s.NotebookId == d.NotebookId && (s.PageId == null || s.PageId == d.Id)));
        var rows = await Project(query).ToListAsync(ct);
        var visible = rows.Select(d => d.Id).ToHashSet();
        NoCache();
        return Ok(rows.OrderByDescending(d => d.UpdatedAtUtc).Select(d => View(d, visible)).ToArray());
    }

    [HttpGet("pages/{id:guid}/context")]
    public async Task<IActionResult> Context(Guid id, CancellationToken ct, long? knownRevision = null)
    {
        // SQL CASE avoids fetching/toasting the large binary for an unchanged reader, while
        // access, page labels and link targets are still refreshed on every poll.
        var document = await access.ReadableDocuments(User).AsNoTracking().Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id, d.NotebookId, d.Title, d.Kind, d.UpdatedAtUtc, d.Revision, d.Fingerprint,
                d.TagsJson, d.PeriodStart, d.HoldId,
                YjsState = knownRevision != null && knownRevision == d.Revision ? null : d.YjsState
            }).SingleOrDefaultAsync(ct);
        if (document is null) return NotFound();
        var pages = document.NotebookId is Guid notebookId
            ? await access.ReadableDocuments(User).AsNoTracking().Where(d => d.NotebookId == notebookId)
                .Select(d => new NotionPageLink(d.Id, d.Title, d.ParentPageId)).ToArrayAsync(ct)
            : [];
        var visible = pages.Select(p => p.Id).ToHashSet();
        pages = pages.Select(p => p.ParentPageId is Guid parent && !visible.Contains(parent)
            ? p with { ParentPageId = null } : p).OrderBy(p => p.Title).ToArray();
        NoCache();
        return Ok(new NotebookDocumentContext(document.Id, document.NotebookId, document.Title, document.Kind,
            true, await access.CanEditAsync(id, User, ct), await access.CanCommentAsync(id, User, ct),
            document.NotebookId is Guid nb && await access.CanShareNotebookAsync(nb, User, ct),
            document.YjsState is { Length: > 0 } bytes ? Convert.ToBase64String(bytes) : null,
            document.UpdatedAtUtc, document.Revision, document.Fingerprint, pages,
            ReadTags(document.TagsJson), document.PeriodStart, document.HoldId));
    }

    [HttpPost("{notebookId:guid}/pages")]
    public async Task<IActionResult> Create(Guid notebookId, CreateNotebookPage input, CancellationToken ct)
    {
        if (!await access.CanShareNotebookAsync(notebookId, User, ct)) return NotFound();
        if (!ValidTitle(input.Title)) return BadRequest(new { error = "invalid_title" });
        if (input.ParentPageId is Guid parent && !await db.NotebookDocuments.AnyAsync(d => d.Id == parent
            && d.NotebookId == notebookId, ct)) return BadRequest(new { error = "invalid_parent" });
        var now = DateTimeOffset.UtcNow;
        var document = new NotebookDocument { Id = Guid.NewGuid(), NotebookId = notebookId, Kind = "note",
            Title = input.Title.Trim(), ParentPageId = input.ParentPageId, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.NotebookDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        NoCache();
        return Ok(PageView(document));
    }

    [HttpPatch("pages/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNotebookPage input, CancellationToken ct)
    {
        if (!await access.CanEditAsync(id, User, ct)) return NotFound();
        var document = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == id, ct);
        if (document.NotebookId is not Guid notebookId) return NotFound();
        if (!ValidTitle(input.Title) || input.Tags is null || input.Tags.Length > 20
            || input.Tags.Any(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length > 50 || t.Any(char.IsControl)))
            return BadRequest(new { error = "invalid_metadata" });
        if (input.HoldId is Guid hold && !await new CourseAccess(db).CanAccessAsync(hold, User, ct))
            return BadRequest(new { error = "invalid_hold" });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql())
            // Serialize tree moves within a notebook so two tabs cannot create A -> B -> A.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM notebooks WHERE \"Id\" = {notebookId} FOR UPDATE", ct);
        if (input.ParentPageId is Guid parent)
        {
            var tree = await db.NotebookDocuments.AsNoTracking().Where(d => d.NotebookId == notebookId)
                .Select(d => new { d.Id, d.ParentPageId }).ToDictionaryAsync(d => d.Id, d => d.ParentPageId, ct);
            if (!tree.ContainsKey(parent)) return BadRequest(new { error = "invalid_parent" });
            var seen = new HashSet<Guid> { id };
            Guid? cursor = parent;
            while (cursor is Guid item)
            {
                if (!seen.Add(item)) return BadRequest(new { error = "parent_cycle" });
                cursor = tree.GetValueOrDefault(item);
            }
        }
        var tags = JsonSerializer.Serialize(input.Tags.Select(t => t.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal).ToArray());
        var title = input.Title.Trim();
        // Only metadata columns change: a simultaneous collaboration save keeps its bytes and fingerprint.
        await db.NotebookDocuments.Where(d => d.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Title, title).SetProperty(d => d.ParentPageId, input.ParentPageId)
            .SetProperty(d => d.TagsJson, tags).SetProperty(d => d.HoldId, input.HoldId)
            .SetProperty(d => d.UpdatedAtUtc, DateTimeOffset.UtcNow).SetProperty(d => d.Revision, d => d.Revision + 1), ct);
        await transaction.CommitAsync(ct);
        NoCache();
        return Ok(PageView(await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == id, ct)));
    }

    [HttpGet("pages/{id:guid}/backlinks")]
    public async Task<IActionResult> Backlinks(Guid id, CancellationToken ct)
    {
        var notebookId = await access.ReadableDocuments(User).Where(d => d.Id == id).Select(d => d.NotebookId).SingleOrDefaultAsync(ct);
        if (notebookId is not Guid nb) return NotFound();
        var rows = await Project(access.ReadableDocuments(User).AsNoTracking().Where(d => d.NotebookId == nb
            && db.NotebookLinks.Any(link => link.FromId == d.Id && link.ToId == id))).ToListAsync(ct);
        var visible = await VisibleIds(nb, ct);
        NoCache();
        return Ok(rows.OrderBy(d => d.Title).Select(d => View(d, visible)).ToArray());
    }

    [HttpGet("{notebookId:guid}/graph")]
    public async Task<IActionResult> Graph(Guid notebookId, CancellationToken ct)
    {
        if (!await CanOpenNotebook(notebookId, ct)) return NotFound();
        var query = access.ReadableDocuments(User).AsNoTracking().Where(d => d.NotebookId == notebookId);
        var total = await query.CountAsync(ct);
        var nodes = await query.OrderBy(d => d.Title).ThenBy(d => d.Id).Take(400)
            .Select(d => new NotebookGraphNode(d.Id, d.Title)).ToArrayAsync(ct);
        var ids = nodes.Select(n => n.Id).ToArray();
        var edges = await db.NotebookLinks.AsNoTracking().Where(l => ids.Contains(l.FromId) && ids.Contains(l.ToId))
            .Select(l => new NotionGraphEdge(l.FromId, l.ToId)).ToArrayAsync(ct);
        NoCache();
        return Ok(new NotebookGraph(nodes, edges, total));
    }

    [HttpGet("pages/{id:guid}/comments")]
    public async Task<IActionResult> Comments(Guid id, CancellationToken ct)
    {
        if (!await access.CanCommentAsync(id, User, ct)) return NotFound();
        var comments = await (from c in db.NotebookComments.AsNoTracking()
            join p in db.NotebookProfiles on c.AuthorSub equals p.Sub into profiles
            from p in profiles.DefaultIfEmpty()
            where c.PageId == id
            select new NotebookCommentView(c.Id, c.AuthorSub, p == null ? "LUP-bruger" : p.DisplayName, c.Text, c.CreatedAtUtc))
            .ToListAsync(ct);
        NoCache();
        return Ok(comments.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id).ToArray());
    }

    [HttpPost("pages/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, AddNotebookComment input, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanCommentAsync(id, User, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 4000
            || input.Text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            return BadRequest(new { error = "invalid_comment" });
        var profile = await RegisterProfile(sub, ct);
        var comment = new NotebookComment { Id = Guid.NewGuid(), PageId = id, AuthorSub = sub,
            Text = input.Text.Trim(), CreatedAtUtc = DateTimeOffset.UtcNow };
        db.NotebookComments.Add(comment);
        await db.SaveChangesAsync(ct);
        NoCache();
        return Ok(new NotebookCommentView(comment.Id, sub, profile.DisplayName, comment.Text, comment.CreatedAtUtc));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct, string? q = null)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (q?.Length > 200) return BadRequest(new { error = "invalid_search" });
        var query = db.NotebookProfiles.AsNoTracking().Where(p => p.Sub != sub);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLowerInvariant();
            query = query.Where(p => p.DisplayName.ToLower().Contains(lower));
        }
        NoCache();
        return Ok(await query.OrderBy(p => p.DisplayName).ThenBy(p => p.Sub).Take(50)
            .Select(p => new NotebookUser(p.Sub, p.DisplayName, p.IsTeacher)).ToArrayAsync(ct));
    }

    [HttpGet("{notebookId:guid}/shares")]
    public async Task<IActionResult> Shares(Guid notebookId, CancellationToken ct)
    {
        if (!await access.CanShareNotebookAsync(notebookId, User, ct)) return NotFound();
        var rows = await (from s in db.NotebookShares.AsNoTracking()
            join p in db.NotebookProfiles on s.RecipientSub equals p.Sub into profiles
            from p in profiles.DefaultIfEmpty()
            where s.NotebookId == notebookId
            select new NotebookShareView(s.Id, s.RecipientSub, p == null ? "LUP-bruger" : p.DisplayName,
                s.PageId, s.CreatedAtUtc)).ToArrayAsync(ct);
        NoCache();
        return Ok(rows.OrderBy(s => s.RecipientName).ToArray());
    }

    [HttpPost("{notebookId:guid}/shares")]
    public async Task<IActionResult> GrantShare(Guid notebookId, GrantNotebookShare input, CancellationToken ct)
    {
        if (!await access.CanShareNotebookAsync(notebookId, User, ct)) return NotFound();
        if (input.RecipientSub == UserClaims.GetSub(User) || input.RecipientSub == Guid.Empty)
            return BadRequest(new { error = "invalid_recipient" });
        var recipient = await db.NotebookProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Sub == input.RecipientSub, ct);
        if (recipient is null) return BadRequest(new { error = "recipient_unavailable" });
        if (input.PageId is Guid page && !await db.NotebookDocuments.AnyAsync(d => d.Id == page
            && d.NotebookId == notebookId, ct)) return BadRequest(new { error = "invalid_page" });
        var share = await db.NotebookShares.AsNoTracking().SingleOrDefaultAsync(s => s.NotebookId == notebookId
            && s.RecipientSub == input.RecipientSub && s.PageId == input.PageId, ct);
        if (share is null)
        {
            share = new() { Id = Guid.NewGuid(), NotebookId = notebookId, RecipientSub = input.RecipientSub,
                PageId = input.PageId, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.NotebookShares.Add(share);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(share).State = EntityState.Detached;
                share = await db.NotebookShares.AsNoTracking().SingleOrDefaultAsync(s => s.NotebookId == notebookId
                    && s.RecipientSub == input.RecipientSub && s.PageId == input.PageId, ct);
                if (share is null) throw;
            }
        }
        NoCache();
        return Ok(new NotebookShareView(share.Id, share.RecipientSub, recipient.DisplayName, share.PageId, share.CreatedAtUtc));
    }

    [HttpDelete("{notebookId:guid}/shares/{id:guid}")]
    public async Task<IActionResult> RevokeShare(Guid notebookId, Guid id, CancellationToken ct)
    {
        if (!await access.CanShareNotebookAsync(notebookId, User, ct)) return NotFound();
        await db.NotebookShares.Where(s => s.Id == id && s.NotebookId == notebookId).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    [HttpGet("teacher")]
    public async Task<IActionResult> Teacher(CancellationToken ct, string? search = null, Guid? holdId = null)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var query = db.Notebooks.AsNoTracking();
        if (holdId is Guid hold) query = query.Where(n => db.Memberships.Any(m => m.HoldId == hold && m.UserSub == n.OwnerSub));
        var notebooks = await query.ToListAsync(ct);
        var owners = notebooks.Select(n => n.OwnerSub).ToArray();
        var profiles = await db.NotebookProfiles.AsNoTracking().Where(p => owners.Contains(p.Sub)).ToDictionaryAsync(p => p.Sub, ct);
        var memberships = await (from m in db.Memberships.AsNoTracking() join h in db.Hold on m.HoldId equals h.Id
            where m.UserSub != null && owners.Contains(m.UserSub.Value)
            select new { Sub = m.UserSub!.Value, h.Name }).ToListAsync(ct);
        var ids = notebooks.Select(n => n.Id).ToArray();
        var documents = await db.NotebookDocuments.AsNoTracking().Where(d => d.NotebookId != null && ids.Contains(d.NotebookId.Value))
            .Select(d => new { d.NotebookId, d.Kind, d.PeriodStart, d.UpdatedAtUtc }).ToListAsync(ct);
        var result = notebooks.Select(n =>
        {
            var pages = documents.Where(d => d.NotebookId == n.Id).ToArray();
            return new TeacherNotebookView(n.Id, n.OwnerSub, profiles.GetValueOrDefault(n.OwnerSub)?.DisplayName ?? "LUP-bruger",
                memberships.Where(m => m.Sub == n.OwnerSub).Select(m => m.Name).Distinct().Order().ToArray(),
                pages.Where(d => d.Kind == "journal").Select(d => d.PeriodStart).Max(),
                pages.Length > 0 ? pages.Max(d => d.UpdatedAtUtc) : n.CreatedAtUtc);
        });
        if (!string.IsNullOrWhiteSpace(search)) result = result.Where(n => n.OwnerName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        NoCache();
        return Ok(result.OrderBy(n => n.OwnerName).ToArray());
    }

    [HttpPost("collaboration/authorize")]
    public async Task<IActionResult> AuthorizeDocument(AuthorizeNotionCollaboration input, CancellationToken ct)
    {
        if (!TryDocumentId(input.DocumentName, out var id)) return BadRequest(new { error = "invalid_document" });
        if (!await access.CanEditAsync(id, User, ct)) return NotFound();
        return Ok(new NotionCollaborationAccess(id, true));
    }

    public static bool TryDocumentId(string? name, out Guid id)
    {
        id = Guid.Empty;
        return name is { Length: 45 } && name.StartsWith("notebook:", StringComparison.Ordinal)
            && Guid.TryParseExact(name.AsSpan(9), "D", out id) && id != Guid.Empty;
    }

    public static NotebookPageSummary PageView(NotebookDocument d) => new(d.Id, d.NotebookId ?? Guid.Empty,
        d.Title, d.ParentPageId, d.Kind, ReadTags(d.TagsJson), d.PeriodStart, d.HoldId, d.TemplateId, d.UpdatedAtUtc, d.Revision);

    public static string[] ReadTags(string? json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? []; }
        catch (JsonException) { return []; }
    }

    private async Task<bool> CanOpenNotebook(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return false;
        return await db.Notebooks.AnyAsync(n => n.Id == id && (n.OwnerSub == sub || UserClaims.IsTeacher(User)
            || db.NotebookShares.Any(s => s.NotebookId == id && s.RecipientSub == sub)), ct);
    }

    private async Task<NotebookProfile> RegisterProfile(Guid sub, CancellationToken ct)
    {
        var name = User.FindFirstValue("name")?.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "LUP-bruger";
        name = name[..Math.Min(name.Length, 200)];
        if (char.IsHighSurrogate(name[^1])) name = name[..^1];
        var teacher = UserClaims.IsTeacher(User);
        var now = DateTimeOffset.UtcNow;
        var updated = await db.NotebookProfiles.Where(p => p.Sub == sub).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.DisplayName, name).SetProperty(p => p.IsTeacher, teacher).SetProperty(p => p.UpdatedAtUtc, now), ct);
        if (updated == 0)
        {
            var profile = new NotebookProfile { Sub = sub, DisplayName = name, IsTeacher = teacher, UpdatedAtUtc = now };
            db.NotebookProfiles.Add(profile);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(profile).State = EntityState.Detached;
                if (!await db.NotebookProfiles.AsNoTracking().AnyAsync(p => p.Sub == sub, ct)) throw;
            }
        }
        return new() { Sub = sub, DisplayName = name, IsTeacher = teacher, UpdatedAtUtc = now };
    }

    private async Task<HashSet<Guid>> VisibleIds(Guid notebookId, CancellationToken ct) =>
        (await access.ReadableDocuments(User).Where(d => d.NotebookId == notebookId).Select(d => d.Id).ToListAsync(ct)).ToHashSet();

    private static bool ValidTitle(string? title) => !string.IsNullOrWhiteSpace(title) && title.Length <= 200 && !title.Any(char.IsControl);
    private void NoCache() => Response.Headers.CacheControl = "private, no-store";

    private sealed record PageData(Guid Id, Guid NotebookId, string Title, Guid? ParentPageId, string Kind,
        string TagsJson, DateOnly? PeriodStart, Guid? HoldId, Guid? TemplateId, DateTimeOffset UpdatedAtUtc,
        long Revision, string SearchText);

    private static IQueryable<PageData> Project(IQueryable<NotebookDocument> query) => query.Select(d => new PageData(
        d.Id, d.NotebookId!.Value, d.Title, d.ParentPageId, d.Kind, d.TagsJson, d.PeriodStart, d.HoldId,
        d.TemplateId, d.UpdatedAtUtc, d.Revision, d.SearchText));

    private static NotebookPageSummary View(PageData d, HashSet<Guid> visible, string? search = null) => new(d.Id,
        d.NotebookId, d.Title, d.ParentPageId is Guid parent && visible.Contains(parent) ? parent : null,
        d.Kind, ReadTags(d.TagsJson), d.PeriodStart, d.HoldId, d.TemplateId, d.UpdatedAtUtc, d.Revision,
        string.IsNullOrWhiteSpace(search) ? null : Snippet(d.SearchText, search));

    private static string Snippet(string text, string search)
    {
        var match = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, match - 50);
        var length = Math.Min(220, text.Length - start);
        return (start > 0 ? "…" : "") + text.Substring(start, length) + (start + length < text.Length ? "…" : "");
    }
}
