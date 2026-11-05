using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/polls")]
public sealed class PollsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] Moods = ["Glad", "Klar", "Midt imellem", "Træt", "Presset"];

    private IQueryable<Hold> AccessibleHolds(Guid sub, bool manageOnly = false)
    {
        var teacher = UserClaims.IsTeacher(User);
        var admin = User.IsInRole("Admin");
        return db.Hold.Where(h => (teacher && (admin || h.Forloeb.CreatedBySub == sub
            || h.Memberships.Any(m => m.UserSub == sub)))
            || (!manageOnly && h.Memberships.Any(m => m.UserSub == sub)));
    }

    [HttpGet("holds")]
    public async Task<IActionResult> Holds(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var teacher = UserClaims.IsTeacher(User);
        var holds = await AccessibleHolds(sub).OrderBy(h => h.Name)
            .Select(h => new PollHold(h.Id, h.Name, teacher)).ToListAsync(ct);
        return Ok(holds);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var holds = AccessibleHolds(sub).Select(h => h.Id);
        var manageable = await AccessibleHolds(sub, true).Select(h => h.Id).ToListAsync(ct);
        var rounds = await Query().Where(p => holds.Contains(p.HoldId)
                && (p.State != "draft" || manageable.Contains(p.HoldId)))
            .OrderByDescending(p => p.CreatedAtUtc).Take(100).ToListAsync(ct);
        return Ok(rounds.Select(p => View(p, sub, manageable.Contains(p.HoldId))).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var holds = AccessibleHolds(sub).Select(h => h.Id);
        var round = await Query().FirstOrDefaultAsync(p => p.Id == id && holds.Contains(p.HoldId), ct);
        if (round is null) return NotFound();
        var manage = await AccessibleHolds(sub, true).AnyAsync(h => h.Id == round.HoldId, ct);
        return round.State == "draft" && !manage ? NotFound() : Ok(View(round, sub, manage));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePoll request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!await AccessibleHolds(sub, true).AnyAsync(h => h.Id == request.HoldId, ct)) return NotFound();
        var question = request.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question) || question.Length > 200
            || request.Kind is not ("mood" or "choice" or "scale" or "text")
            || request.State is not ("draft" or "open")
            || request.ResultsMode is not ("after-vote" or "closed" or "teacher"))
            return BadRequest(new { error = "invalid_poll" });
        var options = request.Kind switch
        {
            "mood" => Moods, "scale" => ["1", "2", "3", "4", "5"], "text" => [],
            _ => (request.Options ?? []).Select(s => s?.Trim() ?? "").ToArray()
        };
        if (request.Kind == "choice" && (options.Length is < 2 or > 6
            || options.Any(s => s.Length is < 1 or > 100)
            || options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length))
            return BadRequest(new { error = "invalid_options" });
        var round = new PollRound
        {
            Id = Guid.NewGuid(), HoldId = request.HoldId, CreatedBySub = sub,
            Question = question, Kind = request.Kind, State = request.State,
            ResultsMode = request.Kind == "text" ? "teacher" : request.ResultsMode,
            OptionsJson = JsonSerializer.Serialize(options), CreatedAtUtc = DateTime.UtcNow
        };
        db.PollRounds.Add(round);
        await db.SaveChangesAsync(ct);
        return await Get(round.Id, ct);
    }

    [HttpPost("{id:guid}/state")]
    public async Task<IActionResult> ChangeState(Guid id, PollState request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var holds = AccessibleHolds(sub, true).Select(h => h.Id);
        if (!await db.PollRounds.AnyAsync(p => p.Id == id && holds.Contains(p.HoldId), ct)) return NotFound();
        if (request.State is not ("open" or "closed")) return BadRequest(new { error = "invalid_state" });
        await db.PollRounds.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.State, request.State), ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/answer")]
    public async Task<IActionResult> Answer(Guid id, PollAnswer request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var holds = AccessibleHolds(sub).Select(h => h.Id);
        var round = await db.PollRounds.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && holds.Contains(p.HoldId), ct);
        if (round is null || round.State == "draft") return NotFound();
        if (!await db.Memberships.AnyAsync(m => m.HoldId == round.HoldId && m.UserSub == sub, ct)) return Forbid();
        var options = JsonSerializer.Deserialize<string[]>(round.OptionsJson)!;
        var text = request.Text?.Trim();
        if (round.Kind == "text" ? string.IsNullOrWhiteSpace(text) || text.Length > 280 || request.Choice is not null
            : request.Choice is not int index || index < 0 || index >= options.Length || !string.IsNullOrEmpty(text))
            return BadRequest(new { error = "invalid_answer" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Taking a write lock on this round serializes answers and closure, including across servers.
        var open = await db.PollRounds.Where(p => p.Id == id && p.State == "open")
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ResponseRevision, p => p.ResponseRevision + 1), ct);
        if (open == 0) return Conflict(new { error = "poll_closed" });
        var response = await db.PollResponses.FindAsync([id, sub], ct);
        if (response is null) { response = new() { PollId = id, UserSub = sub }; db.PollResponses.Add(response); }
        response.Choice = request.Choice; response.Text = round.Kind == "text" ? text : null;
        response.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Get(id, ct);
    }

    private IQueryable<PollRound> Query() => db.PollRounds.AsNoTracking()
        .Include(p => p.Hold).ThenInclude(h => h.Memberships).Include(p => p.Responses).AsSplitQuery();

    private static PollView View(PollRound round, Guid sub, bool manage)
    {
        var own = round.Responses.FirstOrDefault(r => r.UserSub == sub);
        var options = JsonSerializer.Deserialize<string[]>(round.OptionsJson)!;
        var visible = manage || round.ResultsMode == "closed" && round.State == "closed"
            || round.ResultsMode == "after-vote" && own is not null;
        return new(round.Id, round.HoldId, round.Hold.Name, round.Question, round.Kind, options, round.State,
            round.ResultsMode, manage, round.Hold.Memberships.Count, own is not null, own?.Choice, own?.Text,
            visible ? round.Responses.Count : null,
            visible ? options.Select((_, i) => round.Responses.Count(r => r.Choice == i)).ToArray() : null,
            manage && round.Kind == "text" ? round.Responses.OrderBy(r => r.UpdatedAtUtc).Take(100).Select(r => r.Text ?? "").ToArray() : null,
            round.CreatedAtUtc);
    }
}
