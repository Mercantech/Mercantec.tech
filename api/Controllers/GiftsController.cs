using System.Text;
using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Gifts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/gifts")]
public sealed class GiftsController(AppDbContext db) : ControllerBase
{
    [HttpPost("profile")]
    public async Task<IActionResult> Register(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var displayName = User.FindFirstValue("name")?.Trim();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = "En fra LUP";
        displayName = string.Concat(displayName.EnumerateRunes().Take(120).Select(r => r.ToString()));
        var contact = await db.GiftContacts.FindAsync([sub], ct);
        if (contact is null)
        {
            contact = new GiftContact { UserSub = sub };
            db.GiftContacts.Add(contact);
        }
        contact.DisplayName = displayName;
        contact.IsTeacher = UserClaims.IsTeacher(User);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            if (!await db.GiftContacts.AsNoTracking().AnyAsync(p => p.UserSub == sub, ct)) throw;
            // A simultaneous tab registered the same account first.
            db.Entry(contact).State = EntityState.Detached;
        }
        return NoContent();
    }

    [HttpGet("recipients")]
    public async Task<IActionResult> Recipients(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var items = await EligibleRecipients(sub)
            .OrderBy(p => p.DisplayName)
            .Select(p => new RecipientDto(p.UserSub, p.DisplayName, p.IsTeacher))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet]
    public async Task<IActionResult> Mailbox(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var received = await db.ChristmasGifts.AsNoTracking().Where(g => g.RecipientSub == sub)
            .OrderByDescending(g => g.CreatedAtUtc).Take(50).Select(g => ToDto(g)).ToListAsync(ct);
        var sent = await db.ChristmasGifts.AsNoTracking().Where(g => g.SenderSub == sub)
            .OrderByDescending(g => g.CreatedAtUtc).Take(50).Select(g => ToDto(g)).ToListAsync(ct);
        var unread = await db.ChristmasGifts.CountAsync(g => g.RecipientSub == sub && g.OpenedAtUtc == null, ct);
        return Ok(new MailboxDto(received, sent, unread));
    }

    [HttpGet("{id:guid}"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var gift = await db.ChristmasGifts.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && g.RecipientSub == sub, ct);
        return gift is null ? NotFound() : Ok(ToDto(gift));
    }

    [HttpPost]
    public async Task<IActionResult> Send(SendGiftRequest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        // Keep a request that started on the previous account from being sent
        // on a newly refreshed token. Identity still comes only from the JWT.
        if (Request.Headers.TryGetValue("X-LUP-Gift-Subject", out var expectedSubject)
            && (!Guid.TryParse(expectedSubject.ToString(), out var expected) || expected != sub))
            return Conflict(new { error = "account_changed" });
        if (request.Id == Guid.Empty || request.RecipientSub == Guid.Empty || request.RecipientSub == sub
            || request.Kind is not ("cheer" or "thanks" or "warmth")
            || request.Wrapping is not ("berry" or "pine" or "gold")
            || request.Theme is not null && !GiftThemes.IsKnown(request.Theme)
            || request.Message is null || request.Message.Length > 240)
            return BadRequest(new { error = "invalid_gift" });
        var existing = await db.ChristmasGifts.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.Id && g.SenderSub == sub, ct);
        if (existing is not null) return Repeat(existing, request);
        var recipient = await EligibleRecipients(sub).FirstOrDefaultAsync(p => p.UserSub == request.RecipientSub, ct);
        if (recipient is null) return BadRequest(new { error = "recipient_unavailable" });
        var sender = await db.GiftContacts.AsNoTracking().FirstOrDefaultAsync(p => p.UserSub == sub, ct);
        if (sender is null) return BadRequest(new { error = "profile_required" });
        var gift = new ChristmasGift
        {
            Id = request.Id, SenderSub = sub, RecipientSub = recipient.UserSub,
            SenderName = sender.DisplayName, RecipientName = recipient.DisplayName,
            Kind = request.Kind, Wrapping = request.Wrapping, Theme = request.Theme ?? GiftThemes.Unknown,
            Message = request.Message.Trim(), CreatedAtUtc = DateTime.UtcNow
        };
        db.ChristmasGifts.Add(gift);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(gift).State = EntityState.Detached;
            existing = await db.ChristmasGifts.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.Id && g.SenderSub == sub, ct);
            if (existing is not null) return Repeat(existing, request);
            // A caller cannot inspect another sender's request ID.
            if (await db.ChristmasGifts.AsNoTracking().AnyAsync(g => g.Id == request.Id, ct)) return Conflict(new { error = "request_conflict" });
            throw;
        }
        return Created($"/api/gifts/{gift.Id}", ToDto(gift));
    }

    [HttpPost("{id:guid}/open")]
    public async Task<IActionResult> Open(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        await db.ChristmasGifts.Where(g => g.Id == id && g.RecipientSub == sub && g.OpenedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(g => g.OpenedAtUtc, DateTime.UtcNow), ct);
        var gift = await db.ChristmasGifts.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id && g.RecipientSub == sub, ct);
        return gift is null ? NotFound() : Ok(ToDto(gift));
    }

    private IQueryable<GiftContact> EligibleRecipients(Guid sub)
    {
        var ownHolds = db.Memberships.Where(m => m.UserSub == sub).Select(m => m.HoldId);
        var peers = db.Memberships.Where(m => ownHolds.Contains(m.HoldId) && m.UserSub != null).Select(m => m.UserSub!.Value);
        var teachers = db.Hold.Where(h => ownHolds.Contains(h.Id)).Select(h => h.Forloeb.CreatedBySub);
        var isTeacher = UserClaims.IsTeacher(User);
        // A student without a linked hold can still reach a registered teacher.
        // Other students remain limited to their actual hold memberships.
        return db.GiftContacts.AsNoTracking().Where(p => p.UserSub != sub && (isTeacher || p.IsTeacher || peers.Contains(p.UserSub) || teachers.Contains(p.UserSub)));
    }

    private IActionResult Repeat(ChristmasGift gift, SendGiftRequest request) =>
        gift.RecipientSub == request.RecipientSub && gift.Kind == request.Kind && gift.Wrapping == request.Wrapping
            && gift.Theme == (request.Theme ?? GiftThemes.Unknown) && gift.Message == request.Message.Trim()
            ? Ok(ToDto(gift)) : Conflict(new { error = "request_conflict" });
    private static GiftDto ToDto(ChristmasGift g) => new(g.Id, g.SenderName, g.RecipientName, g.Kind, g.Wrapping, g.Message, g.CreatedAtUtc, g.OpenedAtUtc, g.Theme);
    public sealed record RecipientDto(Guid UserSub, string DisplayName, bool IsTeacher);
    public sealed record GiftDto(Guid Id, string SenderName, string RecipientName, string Kind, string Wrapping, string Message, DateTime CreatedAtUtc, DateTime? OpenedAtUtc, string? Theme = null);
    public sealed record MailboxDto(List<GiftDto> Received, List<GiftDto> Sent, int Unread);
    public sealed record SendGiftRequest(Guid Id, Guid RecipientSub, string Kind, string Wrapping, string Message, string? Theme = null);
}
