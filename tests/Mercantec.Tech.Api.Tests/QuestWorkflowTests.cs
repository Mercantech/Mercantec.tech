using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class QuestWorkflowTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), anna = Guid.NewGuid(), bo = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId, annaId, boId, shadowId, foreignId, setId, groupA, groupB, mixedGroup, foreignGroup;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync(); db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher } };
        var other = new Hold { Id = Guid.NewGuid(), Name = "H4", Forloeb = new() { Id = Guid.NewGuid(), Name = "H4", CreatedBySub = outsider } };
        holdId = hold.Id; otherHoldId = other.Id; db.Hold.AddRange(hold, other);
        var a = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = anna, DisplayName = "Anna" };
        var b = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = bo, DisplayName = "Bo" };
        var shadow = new Membership { Id = Guid.NewGuid(), Hold = hold, LectioId = "shadow", DisplayName = "Clara" };
        var foreign = new Membership { Id = Guid.NewGuid(), Hold = other, UserSub = outsider, DisplayName = "Andet hold" };
        annaId = a.Id; boId = b.Id; shadowId = shadow.Id; foreignId = foreign.Id;
        db.Memberships.AddRange(a, b, shadow, foreign);
        var set = new LearningGroupSet { Id = Guid.NewGuid(), HoldId = holdId, Name = "Projektgrupper" }; setId = set.Id; db.LearningGroupSets.Add(set);
        groupA = Group(holdId, "Projekt A", setId, a, shadow); groupB = Group(holdId, "Projekt B", setId, b);
        mixedGroup = Group(holdId, "Blandet", setId, a, b); foreignGroup = Group(otherHoldId, "Andet", null, foreign);
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private Guid Group(Guid hold, string name, Guid? set, params Membership[] members)
    { var group = new LearningGroup { Id = Guid.NewGuid(), HoldId = hold, Name = name, GroupSetId = set }; foreach (var member in members) group.Members.Add(new() { Membership = member, MembershipId = member.Id }); db.LearningGroups.Add(group); return group.Id; }
    private QuestsController Api(Guid sub, bool isTeacher = false)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) }; if (isTeacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db, new(db)) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } } };
    }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private SaveQuest Request(QuestAudienceSettings? audience = null, string mode = "either") => new(holdId, "Undersøg løsningen", "Aflevér en forklaring", mode, true, null, "daily", mode == "individual" ? null : setId, true, audience);
    private async Task<LearningQuest> Entity(Guid id) => await db.LearningQuests.SingleAsync(q => q.Id == id);

    [Fact]
    public async Task SelectedStudentsLimitListingDeepLinksAndExpectedRosterIncludingShadowMembers()
    {
        var quest = Value<QuestView>(await Api(teacher, true).Create(Request(new("students", [annaId, shadowId])), default));
        var helper = new QuestAudienceAccess(db); var entity = await Entity(quest.Id);
        Assert.Equal(new[] { annaId, shadowId }.Order(), (await helper.ExpectedMembersAsync(entity, default)).Select(m => m.Id).Order());
        var own = Value<QuestView>(await Api(anna).Get(quest.Id, default));
        Assert.Empty(own.Audience!.MembershipIds!); Assert.Equal(new[] { groupA }, own.AllowedGroupIds);
        Assert.IsType<NotFoundResult>(await Api(bo).Get(quest.Id, default));
        Assert.Empty(Value<QuestView[]>(await Api(bo).List(holdId, default)));
        Assert.Single(Value<QuestView[]>(await Api(anna).List(holdId, default)));
        Assert.False(await helper.CanParticipateAsync(entity, anna, mixedGroup, default));
        Assert.True(await helper.CanParticipateAsync(entity, anna, groupA, default));
        Assert.False(await helper.CanParticipateAsync(entity, bo, groupB, default));
    }

    [Fact]
    public async Task SelectedGroupsOnlyAllowThoseGroupsAndHideUnrelatedGroupTargets()
    {
        var quest = Value<QuestView>(await Api(teacher, true).Create(Request(new("groups", GroupIds: [groupA]), "group"), default));
        var helper = new QuestAudienceAccess(db); var entity = await Entity(quest.Id);
        Assert.Equal(2, (await helper.ExpectedMembersAsync(entity, default)).Length);
        Assert.True(await helper.CanParticipateAsync(entity, anna, groupA, default));
        Assert.False(await helper.CanParticipateAsync(entity, anna, mixedGroup, default));
        Assert.False(await helper.CanParticipateAsync(entity, anna, null, default));
        Assert.IsType<NotFoundResult>(await Api(bo).Get(quest.Id, default));
        var own = Value<QuestView>(await Api(anna).Get(quest.Id, default));
        Assert.Empty(own.Audience!.GroupIds!); Assert.Equal(new[] { groupA }, own.AllowedGroupIds);
    }

    [Fact]
    public async Task ForeignEmptyDuplicateAndMismatchedAudienceReferencesAreRejectedAtomically()
    {
        var api = Api(teacher, true);
        foreach (var audience in new QuestAudienceSettings[] { new("students", [foreignId]), new("students", []), new("students", [annaId, annaId]), new("groups", GroupIds: [foreignGroup]), new("hold", [annaId]), new("unknown") })
            Assert.IsType<BadRequestObjectResult>(await api.Create(Request(audience), default));
        Assert.Empty(await db.LearningQuests.ToArrayAsync());
        Assert.Empty(await db.QuestAudienceMemberships.ToArrayAsync()); Assert.Empty(await db.QuestAudienceGroups.ToArrayAsync());
    }

    [Fact]
    public async Task AudienceLocksWhenDraftExistsButOldClientsMayStillUpdateMetadata()
    {
        var request = Request(new("students", [annaId])); var api = Api(teacher, true);
        var quest = Value<QuestView>(await api.Create(request, default));
        db.QuestSubmissions.Add(new() { Id = Guid.NewGuid(), QuestId = quest.Id, CreatedBySub = anna, OwnerKey = $"u:{anna:N}", State = "draft" }); await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await api.Update(quest.Id, request with { Audience = new("hold") }, default));
        var updated = Value<QuestView>(await api.Update(quest.Id, request with { Title = "Ny titel", Audience = null }, default));
        Assert.Equal("students", updated.Audience!.Mode); Assert.Equal(new[] { annaId }, updated.Audience.MembershipIds);
        Assert.Equal("Ny titel", updated.Title);
    }

    [Fact]
    public async Task GroupChangesKeepOriginalParticipantReadHistoryWithoutGivingThemNewParticipation()
    {
        var view = Value<QuestView>(await Api(teacher, true).Create(Request(new("groups", GroupIds: [groupA]), "group"), default));
        var submission = new QuestSubmission { Id = Guid.NewGuid(), QuestId = view.Id, GroupId = groupA, CreatedBySub = anna, OwnerKey = $"g:{groupA:N}", State = "submitted" };
        submission.Revisions.Add(new() { Id = Guid.NewGuid(), Number = 1, SubmittedBySub = anna, SubmittedAtUtc = DateTimeOffset.UtcNow,
            Participants = [new() { MembershipId = annaId, UserSub = anna, DisplayName = "Anna" }, new() { MembershipId = shadowId, DisplayName = "Clara" }] });
        db.QuestSubmissions.Add(submission); await db.SaveChangesAsync();
        db.LearningGroupMembers.Remove(await db.LearningGroupMembers.SingleAsync(m => m.GroupId == groupA && m.MembershipId == annaId)); await db.SaveChangesAsync();
        var helper = new QuestAudienceAccess(db); var entity = await Entity(view.Id);
        Assert.True(await helper.CanReadAsync(entity, anna, default));
        Assert.False(await helper.CanParticipateAsync(entity, anna, groupA, default));
        Assert.IsType<OkObjectResult>(await Api(anna).Get(view.Id, default));
    }

    [Fact]
    public async Task WeekdaySeriesCreatesSeparateDatedQuestsWithCorrectDstOffsetsAndRetryIds()
    {
        var body = new CreateQuestSeries(Request(new("students", [annaId]), "individual"), new(new(2027, 3, 26), new(2027, 3, 30), "weekdays", new(15, 0)), Guid.NewGuid());
        var series = Value<QuestSeriesView>(await Api(teacher, true).CreateSeries(body, default));
        Assert.Equal(3, series.Quests.Length); Assert.Equal(3, series.Quests.Select(q => q.Id).Distinct().Count());
        Assert.Equal(new DateOnly(2027, 3, 26), series.Quests[0].OccurrenceDate); Assert.Equal(14, series.Quests[0].DeadlineUtc!.Value.Hour);
        Assert.Equal(new DateOnly(2027, 3, 29), series.Quests[1].OccurrenceDate); Assert.Equal(13, series.Quests[1].DeadlineUtc!.Value.Hour);
        Assert.All(series.Quests, q => { Assert.Equal(body.SeriesId, q.SeriesId); Assert.Equal("Undersøg løsningen", q.Title); });
        Assert.Equal(3, await db.QuestAudienceMemberships.CountAsync());
        var retry = Value<QuestSeriesView>(await Api(teacher, true).CreateSeries(body, default));
        Assert.Equal(series.Quests.Select(q => q.Id), retry.Quests.Select(q => q.Id)); Assert.Equal(3, await db.LearningQuests.CountAsync());
        Assert.Empty(await db.QuestSubmissions.ToArrayAsync());
        Assert.Equal(3, Value<QuestView[]>(await Api(anna).List(holdId, default)).Length); Assert.Empty(Value<QuestView[]>(await Api(bo).List(holdId, default)));
    }

    [Fact]
    public async Task ReusingASeriesRequestIdWithChangedPayloadConflictsWithoutCreatingOrChangingQuests()
    {
        var body = new CreateQuestSeries(Request(new("students", [annaId]), "individual"), new(new(2027, 1, 4), new(2027, 1, 5), "daily", new(15, 0)), Guid.NewGuid());
        var api = Api(teacher, true); var original = Value<QuestSeriesView>(await api.CreateSeries(body, default));
        Assert.IsType<ConflictObjectResult>(await api.CreateSeries(body with { Quest = body.Quest with { Title = "Anden opgave" } }, default));
        Assert.IsType<ConflictObjectResult>(await api.CreateSeries(body with { Repeat = body.Repeat with { DeadlineLocal = new(16, 0) } }, default));
        Assert.IsType<ConflictObjectResult>(await api.CreateSeries(body with { Quest = body.Quest with { Audience = new("students", [boId]) } }, default));
        var retry = Value<QuestSeriesView>(await api.CreateSeries(body, default));
        Assert.Equal(original.Quests.Select(q => q.Id), retry.Quests.Select(q => q.Id));
        Assert.Equal(2, await db.LearningQuests.CountAsync());
    }

    [Fact]
    public async Task InvalidOrDstAmbiguousRepeatPlansCreateNothing()
    {
        var api = Api(teacher, true); var request = Request();
        foreach (var plan in new QuestRepeatPlan[] { new(new(2027, 3, 28), new(2027, 3, 28), "daily", new(2, 30)), new(new(2027, 1, 1), new(2027, 4, 10), "daily", new(15, 0)), new(new(2027, 1, 3), new(2027, 1, 1), "weekly", new(15, 0)), new(new(2027, 1, 1), new(2027, 1, 2), "unknown", new(15, 0)) })
            Assert.IsType<BadRequestObjectResult>(await api.CreateSeries(new(request, plan), default));
        Assert.IsType<ForbidResult>(await Api(anna).CreateSeries(new(request, new(new(2027, 1, 1), new(2027, 1, 2), "daily", new(15, 0))), default));
        Assert.Empty(await db.LearningQuests.ToArrayAsync());
    }

    [Theory]
    [InlineData("daily", 15)]
    [InlineData("weekdays", 11)]
    [InlineData("weekly", 3)]
    public void RecurrenceUsesInclusiveBoundedDates(string frequency, int expected)
    {
        var dates = QuestRecurrence.Dates(new(new(2026, 11, 6), new(2026, 11, 20), frequency, new(15, 0)));
        Assert.Equal(expected, dates.Length); Assert.Equal(new DateOnly(2026, 11, 6), dates[0]);
        Assert.Throws<ArgumentException>(() => QuestRecurrence.Dates(new(new(2026, 11, 6), new DateOnly(2026, 11, 6).AddDays(90), frequency, new(15, 0))));
    }
}
