using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/holds/{holdId:guid}/calendar")]
public sealed class HoldCalendarController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var events = await db.HoldCalendarEvents.AsNoTracking().Where(e => e.HoldId == holdId).ToListAsync(ct);
        return Ok(events.OrderBy(e => e.StartUtc).Select(View).ToArray());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid holdId, SaveHoldCalendarEvent request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (!Valid(holdId, request)) return BadRequest(new { error = "invalid_calendar_event" });
        var item = new HoldCalendarEvent { Id = Guid.NewGuid(), HoldId = holdId };
        Apply(item, request);
        db.HoldCalendarEvents.Add(item);
        await db.SaveChangesAsync(ct);
        return Ok(View(item));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid holdId, Guid id, SaveHoldCalendarEvent request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var item = await db.HoldCalendarEvents.SingleOrDefaultAsync(e => e.Id == id && e.HoldId == holdId, ct);
        if (item is null) return NotFound();
        if (!Valid(holdId, request)) return BadRequest(new { error = "invalid_calendar_event" });
        if (request.Version != item.Version) return Conflict(new { error = "calendar_event_changed" });
        Apply(item, request);
        item.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "calendar_event_changed" }); }
        return Ok(View(item));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid holdId, Guid id, [FromQuery] int version, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var item = await db.HoldCalendarEvents.SingleOrDefaultAsync(e => e.Id == id && e.HoldId == holdId, ct);
        if (item is null) return NotFound();
        if (version != item.Version) return Conflict(new { error = "calendar_event_changed" });
        db.HoldCalendarEvents.Remove(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "calendar_event_changed" }); }
        return Ok(new { deleted = true });
    }

    private static bool Valid(Guid holdId, SaveHoldCalendarEvent r) => r.HoldId == holdId
        && !string.IsNullOrWhiteSpace(r.Title) && r.Title.Length <= 200
        && r.Description is not null && r.Description.Length <= 4000
        && r.Location is not null && r.Location.Length <= 200
        && r.Kind is "teaching" or "workshop" or "exam" or "other"
        && r.StartUtc != default && r.EndUtc > r.StartUtc && r.EndUtc - r.StartUtc <= TimeSpan.FromDays(31);

    private static void Apply(HoldCalendarEvent item, SaveHoldCalendarEvent r)
    {
        item.Title = r.Title.Trim(); item.Description = r.Description.Trim();
        item.StartUtc = r.StartUtc.ToUniversalTime(); item.EndUtc = r.EndUtc.ToUniversalTime();
        item.Location = r.Location.Trim(); item.Kind = r.Kind;
    }

    private static HoldCalendarEventView View(HoldCalendarEvent e) =>
        new(e.Id, e.HoldId, e.Title, e.Description, e.StartUtc, e.EndUtc, e.Location, e.Kind, e.Version);
}
