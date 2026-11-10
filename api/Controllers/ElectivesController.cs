using System.Security.Claims;
using System.Text;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/electives")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ElectivesController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] bool publishedOnly = false)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var teacher = UserClaims.IsTeacher(User);
        var canManage = teacher && !publishedOnly;
        var items = await db.Electives.AsNoTracking()
            .Where(e => canManage || e.Status != "draft")
            .OrderBy(e => e.StartsOn == null).ThenBy(e => e.StartsOn).ThenBy(e => e.Title)
            .Select(e => new
            {
                Elective = e,
                Enrolled = e.Enrollments.Any(r => r.UserSub == sub),
                Count = e.Enrollments.Count
            }).ToArrayAsync(ct);
        var today = LocalDate(clock.GetUtcNow());
        return Ok(new ElectiveWorkspace(canManage,
            items.Select(row => View(row.Elective, row.Count, row.Enrolled, !teacher, today)).ToArray()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveElective request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!Valid(request)) return BadRequest(new { error = "invalid_elective" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (request.TemplateId is Guid templateId)
        {
            await LockAsync(templateId, ct);
            if (!await IsTemplateAsync(templateId, ct))
                return BadRequest(new { error = "invalid_elective_template" });
        }

        var elective = new Elective { Id = Guid.NewGuid() };
        Apply(elective, request);
        db.Electives.Add(elective);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(View(elective, 0, false, false, LocalDate(clock.GetUtcNow())));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveElective request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!Valid(request) || request.TemplateId == id) return BadRequest(new { error = "invalid_elective" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Always take both locks in the same order when an offering references a template.
        var ids = request.TemplateId is Guid templateId ? new[] { id, templateId } : [id];
        foreach (var lockId in ids.Order())
            await LockAsync(lockId, ct);

        var elective = await db.Electives.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (elective is null) return NotFound();
        if (request.Version != elective.Version) return Conflict(new { error = "elective_changed" });
        if (request.TemplateId is Guid template && !await IsTemplateAsync(template, ct))
            return BadRequest(new { error = "invalid_elective_template" });
        if (elective.Status == "template" && request.Status != "template"
            && await db.Electives.AnyAsync(e => e.TemplateId == id, ct))
            return Conflict(new { error = "elective_template_in_use" });

        var count = await db.ElectiveEnrollments.CountAsync(e => e.ElectiveId == id, ct);
        if (count > 0 && request.Status is ("draft" or "template"))
            return Conflict(new { error = "elective_has_enrollments" });
        if (request.Capacity is int capacity && count > capacity - request.ReservedSeats)
            return Conflict(new { error = "elective_capacity_below_enrollment" });

        Apply(elective, request);
        elective.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "elective_changed" }); }
        await transaction.CommitAsync(ct);
        return Ok(View(elective, count, false, false, LocalDate(clock.GetUtcNow())));
    }

    [HttpGet("{id:guid}/participants")]
    public async Task<IActionResult> Participants(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!await db.Electives.AnyAsync(e => e.Id == id, ct)) return NotFound();
        var participants = await db.ElectiveEnrollments.AsNoTracking().Where(e => e.ElectiveId == id)
            .OrderBy(e => e.DisplayName).ThenBy(e => e.UserSub)
            .Select(e => new ElectiveParticipant(e.UserSub, e.DisplayName, e.EnrolledAtUtc)).ToArrayAsync(ct);
        return Ok(participants);
    }

    [HttpPut("{id:guid}/enrollment")]
    public async Task<IActionResult> Enroll(Guid id, EnrollInElective request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (UserClaims.IsTeacher(User)) return Forbid();
        if (!request.HasEmployerAgreement) return BadRequest(new { error = "elective_employer_agreement_required" });
        return await ChangeEnrollmentAsync(id, sub, true, ct);
    }

    [HttpDelete("{id:guid}/enrollment")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (UserClaims.IsTeacher(User)) return Forbid();
        return await ChangeEnrollmentAsync(id, sub, false, ct);
    }

    private async Task<IActionResult> ChangeEnrollmentAsync(Guid id, Guid sub, bool enroll, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id, ct);
        var elective = await db.Electives.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.Status != "draft", ct);
        if (elective is null) return NotFound();

        var now = clock.GetUtcNow();
        var today = LocalDate(now);
        var registrations = db.ElectiveEnrollments.Where(e => e.ElectiveId == id);
        var existing = await registrations.SingleOrDefaultAsync(e => e.UserSub == sub, ct);
        var count = await registrations.CountAsync(ct);
        if (enroll == (existing is not null))
            return Ok(View(elective, count, enroll, true, today));

        if (!RegistrationOpen(elective, today))
            return Conflict(new { error = enroll ? "elective_enrollment_closed" : "elective_withdrawal_closed" });
        if (enroll && elective.Capacity is int capacity && count >= capacity - elective.ReservedSeats)
            return Conflict(new { error = "elective_full" });

        if (existing is not null)
        {
            db.ElectiveEnrollments.Remove(existing);
            count--;
        }
        else
        {
            var name = User.FindFirstValue("name")?.Trim();
            db.ElectiveEnrollments.Add(new ElectiveEnrollment
            {
                ElectiveId = id,
                UserSub = sub,
                DisplayName = string.IsNullOrWhiteSpace(name) ? "Elev"
                    : string.Concat(name.EnumerateRunes().Where(r => !Rune.IsControl(r)).Take(200)),
                EnrolledAtUtc = now
            });
            count++;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(View(elective, count, enroll, true, today));
    }

    private Task<int> LockAsync(Guid id, CancellationToken ct) =>
        // A database write lock serializes enrollment and administrative changes on every app instance.
        db.Electives.Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Version, e => e.Version), ct);

    private Task<bool> IsTemplateAsync(Guid id, CancellationToken ct) =>
        db.Electives.AnyAsync(e => e.Id == id && e.Status == "template", ct);

    private static DateOnly LocalDate(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Copenhagen).DateTime);

    private static bool RegistrationOpen(Elective e, DateOnly today) =>
        e.Status == "open"
        && (e.RegistrationOpensOn is not DateOnly opens || today >= opens)
        && (e.RegistrationClosesOn is not DateOnly closes || today <= closes)
        && ((e.EndsOn ?? e.StartsOn) is not DateOnly lastDay || today <= lastDay);

    private static ElectiveView View(Elective e, int count, bool enrolled, bool canParticipate, DateOnly today)
    {
        var open = canParticipate && RegistrationOpen(e, today);
        var occupied = count + e.ReservedSeats;
        return new(e.Id, e.Title, e.Description, e.Schedule, e.StartsOn, e.EndsOn, e.RegistrationOpensOn,
            e.RegistrationClosesOn, e.Capacity, e.ReservedSeats, e.Status, e.SourceUrl, e.TemplateId, e.Version,
            occupied, enrolled, open && !enrolled && (e.Capacity is null || occupied < e.Capacity), open && enrolled);
    }

    private static bool Valid(SaveElective r) =>
        !string.IsNullOrWhiteSpace(r.Title) && r.Title.Length <= 200 && !r.Title.Any(char.IsControl)
        && r.Description is not null && r.Description.Length <= 20000 && !r.Description.Contains('\0')
        && r.Schedule is not null && r.Schedule.Length <= 8000 && !r.Schedule.Contains('\0')
        && r.Status is "template" or "draft" or "open" or "closed" or "archived"
        && (r.Status != "template" || r.TemplateId is null && r.ReservedSeats == 0)
        && r.ReservedSeats is >= 0 and <= 10000
        && (r.Capacity is null || r.Capacity is > 0 and <= 10000 && r.Capacity >= r.ReservedSeats)
        && !(r.StartsOn is DateOnly start && r.EndsOn is DateOnly end && end < start)
        && !(r.RegistrationOpensOn is DateOnly opens && r.RegistrationClosesOn is DateOnly closes && closes < opens)
        && ValidSourceUrl(r.SourceUrl);

    private static bool ValidSourceUrl(string? sourceUrl) =>
        string.IsNullOrWhiteSpace(sourceUrl) || sourceUrl.Length <= 2048
            && Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && uri.UserInfo.Length == 0;

    private static void Apply(Elective e, SaveElective r)
    {
        e.Title = r.Title.Trim();
        e.Description = r.Description.Trim();
        e.Schedule = r.Schedule.Trim();
        e.StartsOn = r.StartsOn;
        e.EndsOn = r.EndsOn;
        e.RegistrationOpensOn = r.RegistrationOpensOn;
        e.RegistrationClosesOn = r.RegistrationClosesOn;
        e.Capacity = r.Capacity;
        e.ReservedSeats = r.ReservedSeats;
        e.Status = r.Status;
        e.SourceUrl = string.IsNullOrWhiteSpace(r.SourceUrl) ? null : r.SourceUrl.Trim();
        e.TemplateId = r.TemplateId;
    }
}
