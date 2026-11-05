using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Pilot authoring API. Every document and roster requires management of its specific hold.</summary>
[ApiController, Authorize, Route("api/notion")]
public sealed class NotionController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet("holds")]
    public async Task<IActionResult> Holds(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        return Ok(await access.AccessibleHolds(User, true).AsNoTracking().OrderBy(h => h.Name)
            .Select(h => new NotionHold(h.Id, h.Name, true)).ToListAsync(ct));
    }

    [HttpGet("pages")]
    public async Task<IActionResult> List(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        // Metadata queries deliberately never materialize the potentially large Yjs binary.
        return Ok(await db.NotionPages.AsNoTracking().Where(p => p.HoldId == holdId)
            .OrderBy(p => p.Title)
            .Select(p => new NotionPageSummary(p.Id, p.HoldId, p.Title, p.ParentPageId, p.UpdatedAtUtc))
            .ToListAsync(ct));
    }

    [HttpPost("pages")]
    public async Task<IActionResult> Create(CreateNotionPage request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanManageAsync(request.HoldId, User, ct)) return NotFound();
        if (!ValidTitle(request.Title)) return BadRequest(new { error = "invalid_title" });
        if (request.ParentPageId is Guid parent && !await db.NotionPages
            .AnyAsync(p => p.Id == parent && p.HoldId == request.HoldId, ct))
            return BadRequest(new { error = "invalid_parent" });
        var page = new NotionPage
        {
            Id = Guid.NewGuid(), HoldId = request.HoldId, ParentPageId = request.ParentPageId,
            Title = request.Title.Trim(), CreatedBySub = sub,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.NotionPages.Add(page);
        await db.SaveChangesAsync(ct);
        return Ok(View(page));
    }

    [HttpPatch("pages/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameNotionPage request, CancellationToken ct)
    {
        var page = await FindEditable(id, ct);
        if (page is null) return NotFound();
        if (!ValidTitle(request.Title)) return BadRequest(new { error = "invalid_title" });
        page.Title = request.Title.Trim();
        page.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(View(page));
    }

    [HttpGet("pages/{id:guid}/context")]
    public async Task<IActionResult> Context(Guid id, CancellationToken ct)
    {
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.Id, p.HoldId, p.Title, p.UpdatedAtUtc }).SingleOrDefaultAsync(ct);
        if (page is null || !await access.CanManageAsync(page.HoldId, User, ct)) return NotFound();
        var members = await db.Memberships.AsNoTracking().Where(m => m.HoldId == page.HoldId)
            .OrderBy(m => m.DisplayName).Select(m => new NotionMember(m.Id, m.DisplayName ?? "Elev"))
            .ToArrayAsync(ct);
        var polls = await db.PollRounds.AsNoTracking().Where(p => p.HoldId == page.HoldId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new NotionPollReference(p.Id, p.Question, p.State)).Take(100).ToArrayAsync(ct);
        return Ok(new NotionPageContext(page.Id, page.HoldId, page.Title, true, members, polls, page.UpdatedAtUtc));
    }

    [HttpPost("collaboration/authorize")]
    public async Task<IActionResult> AuthorizeDocument(AuthorizeNotionCollaboration request, CancellationToken ct)
    {
        if (!TryDocumentId(request.DocumentName, out var id))
            return BadRequest(new { error = "invalid_document" });
        var hold = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.HoldId }).SingleOrDefaultAsync(ct);
        if (hold is null || !await access.CanManageAsync(hold.HoldId, User, ct)) return NotFound();
        return Ok(new NotionCollaborationAccess(id, true));
    }

    public static bool TryDocumentId(string? name, out Guid id)
    {
        id = Guid.Empty;
        // Only canonical document names become WebSocket rooms or storage keys.
        return name is not null && name.Length == 43 && name.StartsWith("notion:", StringComparison.Ordinal)
            && Guid.TryParseExact(name.AsSpan(7), "D", out id) && id != Guid.Empty
            && name.AsSpan(7).SequenceEqual(id.ToString("D").AsSpan());
    }

    private async Task<NotionPage?> FindEditable(Guid id, CancellationToken ct)
    {
        var page = await db.NotionPages.SingleOrDefaultAsync(p => p.Id == id, ct);
        return page is not null && await access.CanManageAsync(page.HoldId, User, ct) ? page : null;
    }

    private static bool ValidTitle(string? title) => !string.IsNullOrWhiteSpace(title)
        && title.Length <= 200 && !title.Any(char.IsControl);
    private static NotionPageSummary View(NotionPage p) => new(p.Id, p.HoldId, p.Title, p.ParentPageId, p.UpdatedAtUtc);
}
