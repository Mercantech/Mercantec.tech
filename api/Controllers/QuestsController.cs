using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/quests")]
public sealed class QuestsController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet("holds")]
    public async Task<IActionResult> Holds(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        var manageable = await access.AccessibleHolds(User, true).Select(h => h.Id).ToListAsync(ct);
        var holds = await access.AccessibleHolds(User).OrderBy(h => h.Name)
            .Select(h => new LearningHold(h.Id, h.Name, manageable.Contains(h.Id))).ToListAsync(ct);
        return Ok(holds);
    }

    [HttpGet("members")]
    public async Task<IActionResult> Members(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        return Ok(await db.Memberships.Where(m => m.HoldId == holdId)
            .Select(m => new LearningMember(m.Id, m.UserSub, m.DisplayName ?? "Elev")).ToListAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(holdId, User, ct);
        var quests = await db.LearningQuests.AsNoTracking().Where(q => q.HoldId == holdId && (q.Published || manage)).ToListAsync(ct);
        return Ok(quests.OrderBy(q => q.CreatedAtUtc).Select(q => View(q, manage)).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var quest = await db.LearningQuests.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id, ct);
        if (quest is null || !await access.CanAccessAsync(quest.HoldId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(quest.HoldId, User, ct);
        if (!quest.Published && !manage) return NotFound();
        return Ok(View(quest, manage));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveQuest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!await access.CanManageAsync(request.HoldId, User, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200
            || request.Description is null || request.Description.Length > 20000
            || request.SubmissionMode is not ("individual" or "group" or "either")
            || request.Kind is not ("main" or "daily" or "side"))
            return BadRequest(new { error = "invalid_quest" });
        var quest = new LearningQuest
        {
            Id = Guid.NewGuid(), HoldId = request.HoldId, Title = request.Title.Trim(),
            Description = request.Description, SubmissionMode = request.SubmissionMode,
            Published = request.Published, DeadlineUtc = request.DeadlineUtc?.ToUniversalTime(), Kind = request.Kind,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.LearningQuests.Add(quest);
        await db.SaveChangesAsync(ct);
        return Ok(View(quest, true));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveQuest request, CancellationToken ct)
    {
        var quest = await db.LearningQuests.SingleOrDefaultAsync(q => q.Id == id, ct);
        if (quest is null || !await access.CanManageAsync(quest.HoldId, User, ct)) return NotFound();
        if (request.HoldId != quest.HoldId || string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200
            || request.Description is null || request.Description.Length > 20000
            || request.SubmissionMode is not ("individual" or "group" or "either")
            || request.Kind is not ("main" or "daily" or "side"))
            return BadRequest(new { error = "invalid_quest" });
        if (request.SubmissionMode != quest.SubmissionMode
            && await db.QuestSubmissions.AnyAsync(s => s.QuestId == id, ct))
            return Conflict(new { error = "submission_mode_in_use" });
        quest.Title = request.Title.Trim();
        quest.Description = request.Description;
        quest.Published = request.Published;
        quest.SubmissionMode = request.SubmissionMode;
        quest.DeadlineUtc = request.DeadlineUtc?.ToUniversalTime();
        quest.Kind = request.Kind;
        await db.SaveChangesAsync(ct);
        return Ok(View(quest, true));
    }

    private static QuestView View(LearningQuest q, bool manage) =>
        new(q.Id, q.HoldId, q.Title, q.Description, q.SubmissionMode, q.Published, q.DeadlineUtc, manage, q.Kind);
}
