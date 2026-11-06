using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class GroupSetsTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId, memberId, peerId;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "Hold A", CreatedBySub = teacher } };
        var other = new Hold { Id = Guid.NewGuid(), Name = "H4", Forloeb = new() { Id = Guid.NewGuid(), Name = "Hold B", CreatedBySub = outsider } };
        holdId = hold.Id; otherHoldId = other.Id; memberId = Guid.NewGuid(); peerId = Guid.NewGuid();
        db.Hold.AddRange(hold, other);
        db.Memberships.AddRange(new() { Id = memberId, Hold = hold, UserSub = student, DisplayName = "Ada" },
            new() { Id = peerId, Hold = hold, LectioId = "peer", DisplayName = "Bo" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private T As<T>(T controller, Guid subject, bool isTeacher = false) where T : ControllerBase
    {
        var claims = new List<Claim> { new("sub", subject.ToString()) };
        if (isTeacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }
    private GroupSetsController Sets(Guid subject, bool isTeacher = false) => As(new GroupSetsController(db, new(db)), subject, isTeacher);
    private NotionGroupsController Groups(Guid subject, bool isTeacher = false) => As(new NotionGroupsController(db, new(db)), subject, isTeacher);
    private QuestsController Quests(Guid subject, bool isTeacher = false) => As(new QuestsController(db, new(db)), subject, isTeacher);
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<GroupSetView> CreateSet(string name = "Projektgrupper") =>
        Value<GroupSetView>(await Sets(teacher, true).Create(holdId, new(name), default));
    private async Task<NotionGroupView> CreateGroup(Guid? setId, string name = "Gruppe 1", params Guid[] members) =>
        Value<NotionGroupView>(await Groups(teacher, true).Create(holdId, new(name, members.Length == 0 ? [memberId] : members, GroupSetId: setId), default));

    [Fact]
    public async Task SetsRequireTeachingAccessAndValidName()
    {
        Assert.IsType<NotFoundResult>(await Sets(student).Create(holdId, new("Elevsæt"), default));
        Assert.IsType<NotFoundResult>(await Sets(outsider, true).List(holdId, true, default));
        Assert.IsType<BadRequestObjectResult>(await Sets(teacher, true).Create(holdId, new(" "), default));
        Assert.IsType<BadRequestObjectResult>(await Sets(teacher, true).Create(holdId, new("Ugyldig\nlinje"), default));
        Assert.Empty(await db.LearningGroupSets.ToListAsync());
    }

    [Fact]
    public async Task StudentOnlySeesOwnSetsAndCountsOwnGroups()
    {
        var own = await CreateSet(); var hidden = await CreateSet("Andre elever");
        await CreateGroup(own.Id, "Fælles", memberId, peerId);
        await CreateGroup(own.Id, "Sparring", memberId);
        await CreateGroup(hidden.Id, "Anden gruppe", peerId);
        var visible = Assert.Single(Value<GroupSetView[]>(await Sets(student).List(holdId, false, default)));
        Assert.Equal(own.Id, visible.Id); Assert.Equal(2, visible.GroupCount); Assert.Equal(2, visible.MemberCount);
        Assert.Equal(2, Value<GroupSetView[]>(await Sets(teacher, true).List(holdId, false, default)).Length);
    }

    [Fact]
    public async Task SetRenameArchiveAndRestoreUseOptimisticVersionAndKeepGroups()
    {
        var set = await CreateSet(); var group = await CreateGroup(set.Id);
        var archived = Value<GroupSetView>(await Sets(teacher, true).Update(holdId, set.Id, new("Projekt 1", true, set.Version), default));
        Assert.True(archived.Archived); Assert.Equal(1, archived.GroupCount); Assert.Equal(1, archived.Version);
        Assert.Empty(Value<GroupSetView[]>(await Sets(student).List(holdId, false, default)));
        Assert.Single(Value<GroupSetView[]>(await Sets(student).List(holdId, true, default)));
        Assert.Equal(group.Id, Assert.Single(Value<NotionGroupView[]>(await Groups(student).Groups(holdId, default))).Id);
        Assert.IsType<ConflictObjectResult>(await Sets(teacher, true).Update(holdId, set.Id, new("Gammel", false, 0), default));
        var restored = Value<GroupSetView>(await Sets(teacher, true).Update(holdId, set.Id, new("Projekt 1", false, archived.Version), default));
        Assert.False(restored.Archived); Assert.Equal(2, restored.Version); Assert.Single(Value<GroupSetView[]>(await Sets(student).List(holdId, false, default)));
    }

    [Fact]
    public async Task BatchStoresItsSetAndPreservesOverlappingMembershipInOtherSets()
    {
        var first = await CreateSet("Projekt"); var second = await CreateSet("Makkerpar");
        await CreateGroup(first.Id, "Projekt A", memberId, peerId);
        var created = Value<NotionGroupView[]>(await Groups(teacher, true).CreateBatch(holdId,
            new([new("Makker 1", [memberId]), new("Makker 2", [peerId])], second.Id), default));
        Assert.All(created, g => Assert.Equal(second.Id, g.GroupSetId));
        var roster = Value<NotionHoldMember[]>(await Groups(teacher, true).Members(holdId, default));
        Assert.Equal(2, roster.Single(m => m.Id == memberId).GroupIds.Length);
        var viaAssignments = Value<LearningGroupView[]>(await As(new GroupsController(db, new(db)), student).List(holdId, default));
        Assert.Equal(2, viaAssignments.Length); Assert.Contains(viaAssignments, g => g.GroupSetId == second.Id);
    }

    [Fact]
    public async Task ForeignOrArchivedSetsRejectNewGroupsAndBatchCreatesNothing()
    {
        var foreign = Value<GroupSetView>(await Sets(outsider, true).Create(otherHoldId, new("Fremmed"), default));
        Assert.IsType<BadRequestObjectResult>(await Groups(teacher, true).Create(holdId, new("Forkert", [memberId], GroupSetId: foreign.Id), default));
        var set = await CreateSet();
        await Sets(teacher, true).Update(holdId, set.Id, new(set.Name, true, set.Version), default);
        Assert.IsType<BadRequestObjectResult>(await Groups(teacher, true).CreateBatch(holdId, new([new("Arkiv", [memberId])], set.Id), default));
        Assert.Empty(await db.LearningGroups.ToListAsync());
    }

    [Fact]
    public async Task QuestSetMustBeActiveSameHoldAndCannotChangeAfterSubmission()
    {
        var set = await CreateSet(); var other = await CreateSet("Nyt projekt");
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Projekt", "", "group", true, null, GroupSetId: set.Id), default));
        Assert.Equal(set.Id, quest.GroupSetId);
        Assert.IsType<BadRequestObjectResult>(await Quests(teacher, true).Create(new(holdId, "Individuel", "", "individual", true, null, GroupSetId: set.Id), default));
        db.QuestSubmissions.Add(new() { Id = Guid.NewGuid(), QuestId = quest.Id, OwnerKey = "u:test", CreatedBySub = student, State = "draft" });
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await Quests(teacher, true).Update(quest.Id, new(holdId, "Projekt", "", "group", true, null, GroupSetId: other.Id), default));
        await Sets(teacher, true).Update(holdId, set.Id, new(set.Name, true, set.Version), default);
        Assert.IsType<BadRequestObjectResult>(await Quests(teacher, true).Create(new(holdId, "Ny", "", "group", true, null, GroupSetId: set.Id), default));
        Assert.IsType<OkObjectResult>(await Quests(teacher, true).Update(quest.Id, new(holdId, "Bevaret historie", "", "group", true, null, GroupSetId: set.Id), default));
    }

    [Fact]
    public async Task OptionalQuestRemainsOptionalAfterDatabaseReloadAndUpdate()
    {
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Ekstra", "", "individual", true, null, "side", Required: false), default));
        db.ChangeTracker.Clear();
        Assert.False(Value<QuestView>(await Quests(student).Get(quest.Id, default)).Required);
        var updated = Value<QuestView>(await Quests(teacher, true).Update(quest.Id, new(holdId, "Ekstra 2", "", "individual", true, null, "side", Required: false), default));
        Assert.False(updated.Required);
    }

    [Fact]
    public async Task BatchRequestRetryReturnsOriginalGroupIdsAndChecksAccessBeforeReplay()
    {
        var set = await CreateSet(); var requestId = Guid.NewGuid();
        var request = new SaveNotionGroupBatch([new("Makker 1", [memberId]), new("Makker 2", [peerId])], set.Id, requestId);
        var created = Value<NotionGroupView[]>(await Groups(teacher, true).CreateBatch(holdId, request, default));
        db.ChangeTracker.Clear();
        var replay = Value<NotionGroupView[]>(await Groups(teacher, true).CreateBatch(holdId,
            request with { Groups = [new(" Makker 1 ", [memberId]), new("Makker 2", [peerId])] }, default));
        Assert.Equal(created.Select(g => g.Id), replay.Select(g => g.Id));
        Assert.Equal(2, await db.LearningGroups.CountAsync());
        Assert.IsType<NotFoundResult>(await Groups(student).CreateBatch(holdId, request, default));
        Assert.IsType<NotFoundResult>(await Groups(outsider, true).CreateBatch(holdId, request, default));
        await Sets(teacher, true).Update(holdId, set.Id, new(set.Name, true, set.Version), default);
        var afterArchive = Value<NotionGroupView[]>(await Groups(teacher, true).CreateBatch(holdId, request, default));
        Assert.Equal(created.Select(g => g.Id), afterArchive.Select(g => g.Id));
    }

    [Fact]
    public async Task BatchRequestReuseWithChangedPayloadConflictsWithoutDuplicatingGroups()
    {
        var set = await CreateSet();
        var request = new SaveNotionGroupBatch([new("Gruppe", [memberId, peerId])], set.Id, Guid.NewGuid());
        var created = Value<NotionGroupView[]>(await Groups(teacher, true).CreateBatch(holdId, request, default));
        db.ChangeTracker.Clear();
        // Ordering inside a group is not part of its identity.
        var replay = Value<NotionGroupView[]>(await Groups(teacher, true).CreateBatch(holdId,
            request with { Groups = [new("Gruppe", [peerId, memberId])] }, default));
        Assert.Equal(created.Single().Id, replay.Single().Id);
        Assert.IsType<ConflictObjectResult>(await Groups(teacher, true).CreateBatch(holdId,
            request with { Groups = [new("Andet navn", [memberId, peerId])] }, default));
        Assert.IsType<BadRequestObjectResult>(await Groups(teacher, true).CreateBatch(holdId,
            request with { RequestId = Guid.Empty }, default));
        Assert.Equal(1, await db.LearningGroups.CountAsync());
    }

    [Fact]
    public async Task LegacyGroupUpdateDoesNotDetachItsSetAndSubmittedGroupsCannotMove()
    {
        var set = await CreateSet(); var next = await CreateSet("Nyt sæt"); var group = await CreateGroup(set.Id);
        var renamed = Value<NotionGroupView>(await Groups(teacher, true).Update(holdId, group.Id, new("Omdøbt", [memberId], group.Version), default));
        Assert.Equal(set.Id, renamed.GroupSetId);
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Projekt", "", "group", true, null), default));
        db.QuestSubmissions.Add(new() { Id = Guid.NewGuid(), QuestId = quest.Id, GroupId = group.Id, OwnerKey = $"g:{group.Id:N}", CreatedBySub = student });
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await Groups(teacher, true).Update(holdId, group.Id, new("Flyttet", [memberId], renamed.Version, next.Id), default));
    }

    [Fact]
    public async Task StartingFirstGroupDraftInvalidatesEarlierTeacherEditsAndPreventsSetMove()
    {
        var set = await CreateSet(); var next = await CreateSet("Nyt sæt"); var group = await CreateGroup(set.Id);
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Projekt", "", "group", true, null, GroupSetId: set.Id), default));
        var submission = Value<SubmissionView>(await As(new SubmissionsController(db, new(db)), student)
            .Start(new(quest.Id, group.Id), default));
        Assert.Equal(group.Id, submission.GroupId);
        db.ChangeTracker.Clear();
        var currentVersion = await db.LearningGroups.Where(g => g.Id == group.Id).Select(g => g.Version).SingleAsync();
        Assert.Equal(group.Version + 1, currentVersion);
        Assert.IsType<ConflictObjectResult>(await Groups(teacher, true).Update(holdId, group.Id,
            new("Gammel redigering", [memberId], group.Version, set.Id), default));
        Assert.IsType<ConflictObjectResult>(await Groups(teacher, true).Update(holdId, group.Id,
            new("Flyttet", [memberId], currentVersion, next.Id), default));
        Assert.Equal(set.Id, await db.LearningGroups.Where(g => g.Id == group.Id).Select(g => g.GroupSetId).SingleAsync());
        Assert.Single(await db.QuestSubmissions.Where(s => s.QuestId == quest.Id).ToListAsync());
    }
}
