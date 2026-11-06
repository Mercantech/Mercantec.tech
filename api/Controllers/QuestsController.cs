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
        return Ok(await access.AccessibleHolds(User).OrderBy(h => h.Name)
            .Select(h => new LearningHold(h.Id, h.Name, manageable.Contains(h.Id))).ToListAsync(ct));
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
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(holdId, User, ct);
        var query = db.LearningQuests.AsNoTracking().Where(q => q.HoldId == holdId);
        if (!manage) query = new QuestAudienceAccess(db).Readable(query, sub);
        var quests = await query.ToListAsync(ct);
        return Ok(await Views(quests.OrderBy(q => q.OccurrenceDate).ThenBy(q => q.CreatedAtUtc).ToArray(), manage, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var quest = await db.LearningQuests.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id, ct);
        if (quest is null || !await access.CanAccessAsync(quest.HoldId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(quest.HoldId, User, ct);
        if (!manage && !await new QuestAudienceAccess(db).CanReadAsync(quest, sub, ct)) return NotFound();
        return Ok((await Views([quest], manage, ct))[0]);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveQuest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!await access.CanManageAsync(request.HoldId, User, ct)) return NotFound();
        if (!Valid(request)) return BadRequest(new { error = "invalid_quest" });
        if (!await ValidGroupSet(request, null, ct)) return BadRequest(new { error = "invalid_group_set" });
        var audience = Normalize(request.Audience);
        if (!await ValidAudience(request.HoldId, request.GroupSetId, audience, [], ct)) return BadRequest(new { error = "invalid_quest_audience" });
        var quest = NewQuest(request, audience);
        db.LearningQuests.Add(quest);
        AddAudience(quest, audience);
        await db.SaveChangesAsync(ct);
        return Ok((await Views([quest], true, ct))[0]);
    }

    [HttpPost("series")]
    public async Task<IActionResult> CreateSeries(CreateQuestSeries request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (request.Quest is null || request.Repeat is null) return BadRequest(new { error = "invalid_quest_series" });
        var body = request.Quest;
        if (!await access.CanManageAsync(body.HoldId, User, ct)) return NotFound();
        if (!Valid(body)) return BadRequest(new { error = "invalid_quest" });
        if (!await ValidGroupSet(body, null, ct)) return BadRequest(new { error = "invalid_group_set" });
        var audience = Normalize(body.Audience);
        if (!await ValidAudience(body.HoldId, body.GroupSetId, audience, [], ct)) return BadRequest(new { error = "invalid_quest_audience" });
        if (request.SeriesId == Guid.Empty) return BadRequest(new { error = "invalid_quest_series" });
        var seriesId = request.SeriesId ?? Guid.NewGuid();
        DateOnly[] dates;
        try { dates = QuestRecurrence.Dates(request.Repeat); }
        catch (ArgumentException) { return BadRequest(new { error = "invalid_quest_series" }); }
        if (dates.Length == 0) return BadRequest(new { error = "invalid_quest_series" });
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        var deadlines = dates.Select(date => date.ToDateTime(request.Repeat.DeadlineLocal, DateTimeKind.Unspecified)).ToArray();
        if (deadlines.Any(local => zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)))
            return BadRequest(new { error = "invalid_repeat_time" });
        var utcDeadlines = deadlines.Select(local => new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone))).ToArray();
        var existing = await db.LearningQuests.AsNoTracking().Where(q => q.SeriesId == seriesId).ToArrayAsync(ct);
        if (existing.Length > 0)
        {
            if (!await SameSeries(existing, body, audience, dates, utcDeadlines, ct)) return Conflict(new { error = "quest_series_exists" });
            return Ok(new QuestSeriesView(seriesId, await Views(existing.OrderBy(q => q.OccurrenceDate).ToArray(), true, ct)));
        }
        var quests = dates.Select((date, i) =>
        {
            var quest = NewQuest(body, audience); quest.SeriesId = seriesId; quest.OccurrenceDate = date;
            quest.DeadlineUtc = utcDeadlines[i]; return quest;
        }).ToArray();
        foreach (var quest in quests) { db.LearningQuests.Add(quest); AddAudience(quest, audience); }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var winner = await db.LearningQuests.AsNoTracking().Where(q => q.SeriesId == seriesId && q.HoldId == body.HoldId).ToArrayAsync(ct);
            if (winner.Length == 0) throw;
            if (!await SameSeries(winner, body, audience, dates, utcDeadlines, ct)) return Conflict(new { error = "quest_series_exists" });
            return Ok(new QuestSeriesView(seriesId, await Views(winner.OrderBy(q => q.OccurrenceDate).ToArray(), true, ct)));
        }
        return Ok(new QuestSeriesView(seriesId, await Views(quests, true, ct)));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveQuest request, CancellationToken ct)
    {
        var snapshot = await db.LearningQuests.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id, ct);
        if (snapshot is null || !await access.CanManageAsync(snapshot.HoldId, User, ct)) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await QuestWriteLock.AcquireAsync(db, id, ct) == 0) return NotFound();
        var quest = await db.LearningQuests.SingleAsync(q => q.Id == id, ct);
        // Existing contexts may already track this entity; the authorization snapshot predates the lock.
        await db.Entry(quest).ReloadAsync(ct);
        if (request.HoldId != quest.HoldId || !Valid(request)) return BadRequest(new { error = "invalid_quest" });
        if (!await ValidGroupSet(request, quest.GroupSetId, ct)) return BadRequest(new { error = "invalid_group_set" });
        var oldAudience = await Settings(quest, ct);
        var audience = request.Audience is null ? oldAudience : Normalize(request.Audience);
        if (!await ValidAudience(request.HoldId, request.GroupSetId, audience, oldAudience.GroupIds ?? [], ct))
            return BadRequest(new { error = "invalid_quest_audience" });
        if ((request.SubmissionMode != quest.SubmissionMode || request.GroupSetId != quest.GroupSetId || !SameAudience(oldAudience, audience))
            && await db.QuestSubmissions.AnyAsync(s => s.QuestId == id, ct))
            return Conflict(new { error = "submission_mode_in_use" });
        quest.Title = request.Title.Trim(); quest.Description = request.Description; quest.Published = request.Published;
        quest.SubmissionMode = request.SubmissionMode; quest.DeadlineUtc = request.DeadlineUtc?.ToUniversalTime();
        quest.Kind = request.Kind; quest.GroupSetId = request.GroupSetId; quest.Required = request.Required;
        await ApplyAudience(quest, audience, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok((await Views([quest], true, ct))[0]);
    }

    private static bool Valid(SaveQuest request) => !string.IsNullOrWhiteSpace(request.Title) && request.Title.Length <= 200
        && request.Description is not null && request.Description.Length <= 20000
        && request.SubmissionMode is "individual" or "group" or "either" && request.Kind is "main" or "daily" or "side";
    private async Task<bool> ValidGroupSet(SaveQuest request, Guid? existingSet, CancellationToken ct) =>
        request.GroupSetId is not Guid id || request.SubmissionMode != "individual"
            && await db.LearningGroupSets.AnyAsync(s => s.Id == id && s.HoldId == request.HoldId && (!s.Archived || s.Id == existingSet), ct);
    private static QuestAudienceSettings Normalize(QuestAudienceSettings? settings) => new(settings?.Mode ?? "hold", settings?.MembershipIds ?? [], settings?.GroupIds ?? []);
    private static bool SameAudience(QuestAudienceSettings a, QuestAudienceSettings b) => a.Mode == b.Mode
        && (a.MembershipIds ?? []).Order().SequenceEqual((b.MembershipIds ?? []).Order()) && (a.GroupIds ?? []).Order().SequenceEqual((b.GroupIds ?? []).Order());
    private async Task<bool> SameSeries(LearningQuest[] existing, SaveQuest body, QuestAudienceSettings audience, DateOnly[] dates, DateTimeOffset[] deadlines, CancellationToken ct)
    {
        if (existing.Length != dates.Length) return false;
        var ordered = existing.OrderBy(q => q.OccurrenceDate).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            var q = ordered[i];
            if (q.HoldId != body.HoldId || q.Title != body.Title.Trim() || q.Description != body.Description || q.SubmissionMode != body.SubmissionMode
                || q.Kind != body.Kind || q.Required != body.Required || q.GroupSetId != body.GroupSetId || q.Published != body.Published
                || q.OccurrenceDate != dates[i] || q.DeadlineUtc != deadlines[i] || !SameAudience(await Settings(q, ct), audience)) return false;
        }
        return true;
    }
    private async Task<bool> ValidAudience(Guid holdId, Guid? setId, QuestAudienceSettings a, Guid[] oldGroups, CancellationToken ct)
    {
        var members = a.MembershipIds ?? []; var groups = a.GroupIds ?? [];
        if (a.Mode is not ("hold" or "students" or "groups") || members.Length > 250 || groups.Length > 100
            || members.Distinct().Count() != members.Length || groups.Distinct().Count() != groups.Length) return false;
        if (a.Mode == "hold") return members.Length == 0 && groups.Length == 0;
        if (a.Mode == "students") return groups.Length == 0 && members.Length > 0
            && await db.Memberships.CountAsync(m => m.HoldId == holdId && members.Contains(m.Id), ct) == members.Length;
        return members.Length == 0 && groups.Length > 0 && await db.LearningGroups.CountAsync(g =>
            g.HoldId == holdId && groups.Contains(g.Id) && (setId == null || g.GroupSetId == setId)
            && (g.GroupSetId == null || db.LearningGroupSets.Any(s => s.Id == g.GroupSetId && !s.Archived) || oldGroups.Contains(g.Id)), ct) == groups.Length;
    }
    private static LearningQuest NewQuest(SaveQuest r, QuestAudienceSettings audience) => new()
    { Id = Guid.NewGuid(), HoldId = r.HoldId, Title = r.Title.Trim(), Description = r.Description, SubmissionMode = r.SubmissionMode, Published = r.Published,
        DeadlineUtc = r.DeadlineUtc?.ToUniversalTime(), Kind = r.Kind, GroupSetId = r.GroupSetId, Required = r.Required,
        AudienceMode = audience.Mode, CreatedAtUtc = DateTimeOffset.UtcNow };
    private async Task ApplyAudience(LearningQuest quest, QuestAudienceSettings audience, CancellationToken ct)
    {
        var members = await db.QuestAudienceMemberships.Where(a => a.QuestId == quest.Id).ToListAsync(ct);
        var groups = await db.QuestAudienceGroups.Where(a => a.QuestId == quest.Id).ToListAsync(ct);
        var wantedMembers = (audience.MembershipIds ?? []).ToHashSet(); var wantedGroups = (audience.GroupIds ?? []).ToHashSet();
        db.QuestAudienceMemberships.RemoveRange(members.Where(a => !wantedMembers.Contains(a.MembershipId)));
        db.QuestAudienceGroups.RemoveRange(groups.Where(a => !wantedGroups.Contains(a.GroupId)));
        foreach (var id in wantedMembers.Where(id => !members.Any(a => a.MembershipId == id))) db.QuestAudienceMemberships.Add(new() { Quest = quest, MembershipId = id });
        foreach (var id in wantedGroups.Where(id => !groups.Any(a => a.GroupId == id))) db.QuestAudienceGroups.Add(new() { Quest = quest, GroupId = id });
        quest.AudienceMode = audience.Mode;
    }
    private void AddAudience(LearningQuest quest, QuestAudienceSettings audience)
    {
        foreach (var id in audience.MembershipIds ?? []) db.QuestAudienceMemberships.Add(new() { Quest = quest, MembershipId = id });
        foreach (var id in audience.GroupIds ?? []) db.QuestAudienceGroups.Add(new() { Quest = quest, GroupId = id });
        quest.AudienceMode = audience.Mode;
    }
    private async Task<QuestAudienceSettings> Settings(LearningQuest quest, CancellationToken ct) => new(quest.AudienceMode,
        await db.QuestAudienceMemberships.Where(a => a.QuestId == quest.Id).Select(a => a.MembershipId).ToArrayAsync(ct),
        await db.QuestAudienceGroups.Where(a => a.QuestId == quest.Id).Select(a => a.GroupId).ToArrayAsync(ct));
    private async Task<QuestView[]> Views(LearningQuest[] quests, bool manage, CancellationToken ct)
    {
        var ids = quests.Select(q => q.Id).ToArray();
        var members = await db.QuestAudienceMemberships.AsNoTracking().Where(a => ids.Contains(a.QuestId)).ToArrayAsync(ct);
        var groups = await db.QuestAudienceGroups.AsNoTracking().Where(a => ids.Contains(a.QuestId)).ToArrayAsync(ct);
        var sub = UserClaims.GetSub(User);
        var holdIds = quests.Select(q => q.HoldId).Distinct().ToArray();
        var ownGroups = !manage && sub is Guid subject ? await db.LearningGroups.AsNoTracking().Include(g => g.Members)
            .Where(g => holdIds.Contains(g.HoldId) && g.Members.Any(m => m.Membership.UserSub == subject)
                && (g.GroupSetId == null || db.LearningGroupSets.Any(s => s.Id == g.GroupSetId && !s.Archived))).ToArrayAsync(ct) : [];
        return quests.Select(q => new QuestView(q.Id, q.HoldId, q.Title, q.Description, q.SubmissionMode, q.Published, q.DeadlineUtc, manage,
            q.Kind, q.GroupSetId, q.Required, new(q.AudienceMode, manage ? members.Where(a => a.QuestId == q.Id).Select(a => a.MembershipId).ToArray() : [],
                manage ? groups.Where(a => a.QuestId == q.Id).Select(a => a.GroupId).ToArray() : []), q.SeriesId, q.OccurrenceDate,
                manage ? null : ownGroups.Where(g => q.SubmissionMode != "individual" && g.HoldId == q.HoldId
                    && (q.GroupSetId == null || g.GroupSetId == q.GroupSetId)
                    && (q.AudienceMode == "hold" || q.AudienceMode == "groups" && groups.Any(a => a.QuestId == q.Id && a.GroupId == g.Id)
                        || q.AudienceMode == "students" && g.Members.All(m => members.Any(a => a.QuestId == q.Id && a.MembershipId == m.MembershipId))))
                    .Select(g => g.Id).ToArray())).ToArray();
    }
}
