using System.Security.Claims;
using System.Text;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class StudentLearningJourneyTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly MemoryStorage storage = new();
    private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
    private readonly Guid teacher = Guid.NewGuid(), anna = Guid.NewGuid(), bo = Guid.NewGuid(), clara = Guid.NewGuid();
    private AppDbContext db = null!;
    private Guid holdId, foreignHoldId, annaId, boId, claraId, setId, groupId, pairId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "Spil", CreatedBySub = teacher } };
        var foreign = new Hold { Id = Guid.NewGuid(), Name = "Andet hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "Andet", CreatedBySub = teacher } };
        holdId = hold.Id; foreignHoldId = foreign.Id; db.Hold.AddRange(hold, foreign);
        annaId = Guid.NewGuid(); boId = Guid.NewGuid(); claraId = Guid.NewGuid();
        db.Memberships.AddRange(new() { Id = annaId, Hold = hold, UserSub = anna, DisplayName = "Anna" },
            new() { Id = boId, Hold = hold, UserSub = bo, DisplayName = "Bo" },
            new() { Id = claraId, Hold = hold, UserSub = clara, DisplayName = "Clara" });
        setId = AddSet("Projektgrupper");
        groupId = AddGroup("Anna og Bo", setId, annaId, boId);
        pairId = AddGroup("Annas makkerpar", AddSet("Makkerpar"), annaId);
        AddGroup("Claras gruppe", AddSet("Andre elever"), claraId);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private Guid AddSet(string name)
    { var id = Guid.NewGuid(); db.LearningGroupSets.Add(new() { Id = id, HoldId = holdId, Name = name }); return id; }
    private Guid AddGroup(string name, Guid set, params Guid[] members)
    {
        var group = new LearningGroup { Id = Guid.NewGuid(), HoldId = holdId, Name = name, GroupSetId = set };
        foreach (var id in members) group.Members.Add(new() { MembershipId = id });
        db.LearningGroups.Add(group); return group.Id;
    }
    private T As<T>(T controller, Guid sub, bool teaching = false) where T : ControllerBase
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) };
        if (teaching) claims.Add(new(ClaimTypes.Role, "Underviser"));
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }
    private QuestsController Quests(Guid sub, bool teaching = false) => As(new QuestsController(db, new(db)), sub, teaching);
    private SubmissionsController Submissions(Guid sub, bool teaching = false) => As(new SubmissionsController(db, new(db)), sub, teaching);
    private TeachingCoursesController Courses(Guid sub, bool teaching = false) => As(new TeachingCoursesController(db, new(db)), sub, teaching);
    private FilesController Files(Guid sub, bool teaching = false) => As(new FilesController(storage, db, protection), sub, teaching);
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<QuestView> Quest(string title, string mode = "individual", QuestAudienceSettings? audience = null, bool published = true) =>
        Value<QuestView>(await Quests(teacher, true).Create(new(holdId, title, "Aflevér dit arbejde", mode, published, null,
            GroupSetId: mode == "group" ? setId : null, Audience: audience), default));
    private async Task<UploadedFile> Upload(Guid sub, string name, string text, bool teaching = false)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary() };
        return Value<UploadedFile>(await Files(sub, teaching).Upload(file, default, holdId));
    }

    [Fact]
    public async Task StudentDiscoversPublishedCoursesOwnQuestsAndOverlappingGroupsButCannotOpenOtherTargets()
    {
        var shared = await Quest("Alle elever");
        var own = await Quest("Kun Anna", audience: new("students", [annaId]));
        var team = await Quest("Projektgruppen", "group", new("groups", GroupIds: [groupId]));
        var hidden = await Quest("Kun Clara", audience: new("students", [claraId]));
        var draft = await Quest("Underviserens kladde", published: false);
        var course = Value<TeachingCourseView>(await Courses(teacher, true).Create(holdId,
            new("Fra idé til spil", "Byg et spil", "Test din løsning", null, null, true), default));
        await Courses(teacher, true).SaveResources(holdId, course.Id, new(course.Version,
            [new("quest", shared.Id), new("quest", own.Id), new("quest", team.Id), new("quest", hidden.Id), new("quest", draft.Id)]), default);
        await Courses(teacher, true).Create(holdId, new("Kladdeforløb", "", "", null, null), default);
        var archived = Value<TeachingCourseView>(await Courses(teacher, true).Create(holdId,
            new("Arkiv", "", "", null, null, true, true), default));

        var learningHold = Assert.Single(Value<List<LearningHold>>(await Quests(anna).Holds(default)));
        Assert.Equal(holdId, learningHold.Id); Assert.False(learningHold.CanManage);
        var workspace = Value<TeachingCourseWorkspace>(await Courses(anna).List(holdId, false, default));
        Assert.False(workspace.CanManage);
        Assert.Equal(course.Id, Assert.Single(workspace.Courses).Id);
        Assert.Equal(new[] { shared.Id, own.Id, team.Id }.Order(), workspace.Courses[0].Resources.Select(r => r.ResourceId).Order());
        var available = Value<QuestView[]>(await Quests(anna).List(holdId, default));
        Assert.Equal(3, available.Length); Assert.All(available, q => Assert.False(q.CanManage));
        Assert.Equal(new[] { groupId }, available.Single(q => q.Id == team.Id).AllowedGroupIds);
        var groups = Value<LearningGroupView[]>(await As(new GroupsController(db, new(db)), anna).List(holdId, default));
        Assert.Equal(new[] { groupId, pairId }.Order(), groups.Select(g => g.Id).Order());
        Assert.Equal(2, Value<GroupSetView[]>(await As(new GroupSetsController(db, new(db)), anna).List(holdId, false, default)).Length);
        Assert.IsType<NotFoundResult>(await Courses(anna).List(foreignHoldId, false, default));
        Assert.IsType<NotFoundResult>(await Quests(anna).Get(hidden.Id, default));
        Assert.IsType<NotFoundResult>(await Submissions(anna).Start(new(hidden.Id, null), default));
        Assert.IsType<NotFoundResult>(await Quests(anna).Get(draft.Id, default));
        Assert.IsType<ForbidResult>(await Submissions(teacher, true).Start(new(shared.Id, null), default));
        Assert.DoesNotContain(workspace.Courses, c => c.Id == archived.Id);
    }

    [Fact]
    public async Task IndividualJourneyUploadsSubmitsAndReadsTeacherFeedbackWithPrivateAttachments()
    {
        var quest = await Quest("Min løsning", audience: new("students", [annaId]));
        var draft = Value<SubmissionView>(await Submissions(anna).Start(new(quest.Id, null), default));
        var code = await Upload(anna, "solution.txt", "Mit program");
        draft = Value<SubmissionView>(await Submissions(anna).SaveDraft(draft.Id, new(draft.Version, "Min forklaring", [code.Id]), default));
        var submitted = Value<SubmissionView>(await Submissions(anna).Submit(draft.Id, new(draft.Version), default));
        Assert.False(submitted.CanEdit); Assert.Equal("submitted", submitted.State);
        var feedbackFile = await Upload(teacher, "feedback.txt", "Kommentarer til programmet", true);
        await Submissions(teacher, true).Feedback(submitted.Id,
            new(submitted.Revisions[0].Id, submitted.Version, "God løsning", null, "approved", [feedbackFile.Id]), default);
        var read = Value<SubmissionView>(await Submissions(anna).Get(submitted.Id, default));
        var revision = Assert.Single(read.Revisions);
        Assert.Equal("approved", read.State); Assert.False(read.CanReview);
        Assert.Equal(code.Id, Assert.Single(revision.Files).Id);
        Assert.Equal(feedbackFile.Id, Assert.Single(Assert.Single(revision.Feedback).Attachments).Id);
        Assert.Equal("Kommentarer til programmet", Value<FileTextPreview>(await Files(anna).Text(feedbackFile.Id, default)).Text);
        Assert.IsType<NotFoundResult>(await Submissions(bo).Get(submitted.Id, default));
        Assert.IsType<NotFoundResult>(await Files(bo).Text(code.Id, default));
        Assert.IsType<NotFoundResult>(await Files(bo).Text(feedbackFile.Id, default));
    }

    [Fact]
    public async Task GroupJourneySharesDraftFilesAndFeedbackOnlyWithItsParticipants()
    {
        var quest = await Quest("Vores løsning", "group", new("groups", GroupIds: [groupId]));
        var draft = Value<SubmissionView>(await Submissions(anna).Start(new(quest.Id, groupId), default));
        var file = await Upload(anna, "group.txt", "Fælles program");
        draft = Value<SubmissionView>(await Submissions(anna).SaveDraft(draft.Id, new(draft.Version, "Vores arbejde", [file.Id]), default));
        Assert.Equal(draft.Id, Value<SubmissionView>(await Submissions(bo).Start(new(quest.Id, groupId), default)).Id);
        Assert.Equal("Fælles program", Value<FileTextPreview>(await Files(bo).Text(file.Id, default)).Text);
        draft = Value<SubmissionView>(await Submissions(bo).SaveDraft(draft.Id, new(draft.Version, "Vores færdige arbejde", [file.Id]), default));
        var submitted = Value<SubmissionView>(await Submissions(bo).Submit(draft.Id, new(draft.Version), default));
        var revision = Assert.Single(submitted.Revisions);
        Assert.Equal(new[] { annaId, boId }.Order(), revision.Participants.Select(p => p.MembershipId).Order());
        await Submissions(teacher, true).Feedback(submitted.Id, new(revision.Id, submitted.Version, "Godt samarbejde", null, "approved", []), default);
        foreach (var pupil in new[] { anna, bo })
        {
            var own = Value<SubmissionView>(await Submissions(pupil).Get(submitted.Id, default));
            Assert.Equal("approved", own.State);
            Assert.Equal("Godt samarbejde", Assert.Single(Assert.Single(own.Revisions).Feedback).Text);
        }
        Assert.IsType<NotFoundResult>(await Submissions(clara).Get(submitted.Id, default));
        Assert.IsType<NotFoundResult>(await Files(clara).Text(file.Id, default));
        Assert.IsType<NotFoundResult>(await Submissions(anna).Start(new(quest.Id, pairId), default));
    }

    [Fact]
    public async Task AddedNonTargetGroupMemberCannotReadTargetedDraftOrItsUploadedFile()
    {
        var quest = await Quest("Annas gruppeopgave", "group", new("students", [annaId]));
        var single = AddGroup("Kun Anna", setId, annaId); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var draft = Value<SubmissionView>(await Submissions(anna).Start(new(quest.Id, single), default));
        var file = await Upload(anna, "private.txt", "Målrettet kladde");
        await Submissions(anna).SaveDraft(draft.Id, new(draft.Version, "Annas arbejde", [file.Id]), default);
        db.ChangeTracker.Clear();
        var current = await db.LearningGroups.SingleAsync(g => g.Id == single);
        await As(new GroupsController(db, new(db)), teacher, true).Update(single,
            new(holdId, current.Name, [annaId, boId], current.Version, setId), default);
        Assert.IsType<NotFoundResult>(await Quests(bo).Get(quest.Id, default));
        Assert.IsType<NotFoundResult>(await Submissions(bo).Get(draft.Id, default));
        Assert.IsType<NotFoundResult>(await Files(bo).Text(file.Id, default));
        Assert.Equal("Målrettet kladde", Value<FileTextPreview>(await Files(anna).Text(file.Id, default)).Text);
    }

    private sealed class MemoryStorage : IObjectStorage
    {
        private readonly Dictionary<string, (byte[] Bytes, string Type)> objects = new();
        public bool IsConfigured => true;
        public string Bucket => "student-tests";
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken);
            objects[key] = (buffer.ToArray(), contentType); return new(key, Bucket, contentType, buffer.Length);
        }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<ObjectGetResult?>(objects.TryGetValue(key, out var value)
                ? new(new MemoryStream(value.Bytes), value.Type, value.Bytes.Length) : null);
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Student files must use the authorized API.");
    }
}
