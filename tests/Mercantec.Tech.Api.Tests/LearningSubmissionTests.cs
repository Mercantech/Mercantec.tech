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

public sealed class LearningSubmissionTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), peer = Guid.NewGuid(), stranger = Guid.NewGuid();
    private Guid holdId, studentMember, peerMember, strangerMember;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Game Design", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = course };
        holdId = hold.Id;
        studentMember = Guid.NewGuid(); peerMember = Guid.NewGuid(); strangerMember = Guid.NewGuid();
        db.Memberships.AddRange(
            new Membership { Id = studentMember, Hold = hold, UserSub = student, DisplayName = "Ada" },
            new Membership { Id = peerMember, Hold = hold, UserSub = peer, DisplayName = "Bo" },
            new Membership { Id = strangerMember, Hold = hold, UserSub = stranger, DisplayName = "Cia" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private static ClaimsPrincipal User(Guid sub, bool isTeacher = false)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) };
        if (isTeacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(new ClaimsIdentity(claims, "test"));
    }

    private T As<T>(T controller, Guid sub, bool isTeacher = false) where T : ControllerBase
    {
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = User(sub, isTeacher) } };
        return controller;
    }

    private SubmissionsController Submissions(Guid sub, bool isTeacher = false) =>
        As(new SubmissionsController(db, new(db)), sub, isTeacher);
    private GroupsController Groups() => As(new GroupsController(db, new(db)), teacher, true);
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private async Task<QuestView> Quest(string mode = "individual")
    {
        var controller = As(new QuestsController(db, new(db)), teacher, true);
        return Value<QuestView>(await controller.Create(new(holdId, "Aflever spil", "Beskriv arbejdet", mode, true, null), default));
    }

    private async Task<LearningGroupView> Group() =>
        Value<LearningGroupView>(await Groups().Create(new(holdId, "Hold 1", [studentMember, peerMember]), default));

    private async Task<SubmissionView> Draft(QuestView quest, Guid? group = null)
    {
        var submission = Value<SubmissionView>(await Submissions(student).Start(new(quest.Id, group), default));
        return Value<SubmissionView>(await Submissions(student).SaveDraft(submission.Id, new(submission.Version, "Vores arbejde", []), default));
    }

    [Fact]
    public async Task GroupSubmissionAndFeedbackAreSharedWithEveryParticipant()
    {
        var group = await Group();
        var draft = await Draft(await Quest("group"), group.Id);
        var submitted = Value<SubmissionView>(await Submissions(peer).Submit(draft.Id, new(draft.Version), default));
        Assert.Equal("submitted", submitted.State);
        Assert.Equal(2, Assert.Single(submitted.Revisions).Participants.Length);
        var revision = submitted.Revisions[0];
        var approved = Value<SubmissionView>(await Submissions(teacher, true).Feedback(submitted.Id,
            new(revision.Id, submitted.Version, "Flot løsning", null, "approved", []), default));
        var byPeer = Value<SubmissionView>(await Submissions(peer).Get(submitted.Id, default));
        Assert.Equal("approved", byPeer.State);
        Assert.Equal("Flot løsning", Assert.Single(Assert.Single(byPeer.Revisions).Feedback).Text);
        Assert.Equal(approved.Version, byPeer.Version);
        Assert.IsType<NotFoundResult>(await Submissions(stranger).Get(submitted.Id, default));
    }

    [Fact]
    public async Task ChangingGroupDoesNotGiveNewMemberOldSubmissionOrRemoveOriginalHistory()
    {
        var group = await Group();
        var draft = await Draft(await Quest("group"), group.Id);
        var submitted = Value<SubmissionView>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        db.ChangeTracker.Clear();
        var current = Assert.Single(Value<LearningGroupView[]>(await Groups().List(holdId, default)));
        await Groups().Update(group.Id, new(holdId, "Hold 1", [studentMember, strangerMember], current.Version), default);
        Assert.IsType<NotFoundResult>(await Submissions(stranger).Get(submitted.Id, default));
        var historical = Value<SubmissionView>(await Submissions(peer).Get(submitted.Id, default));
        Assert.Contains(Assert.Single(historical.Revisions).Participants, p => p.UserSub == peer);
        Assert.DoesNotContain(historical.Revisions[0].Participants, p => p.UserSub == stranger);
    }

    [Fact]
    public async Task ResubmissionPreservesVersionAndFeedbackAndRejectsStaleApproval()
    {
        var draft = await Draft(await Quest());
        var first = Value<SubmissionView>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        var revisionId = Assert.Single(first.Revisions).Id;
        var returned = Value<SubmissionView>(await Submissions(teacher, true).Feedback(first.Id,
            new(revisionId, first.Version, "Ret rapport", null, "changes-requested", []), default));
        var revisedDraft = Value<SubmissionView>(await Submissions(student).SaveDraft(first.Id,
            new(returned.Version, "Rettet arbejde", []), default));
        var second = Value<SubmissionView>(await Submissions(student).Submit(first.Id, new(revisedDraft.Version), default));
        Assert.Equal(2, second.Revisions.Length);
        Assert.Equal("Vores arbejde", second.Revisions[0].Text);
        Assert.Equal("Ret rapport", Assert.Single(second.Revisions[0].Feedback).Text);
        Assert.IsType<ConflictObjectResult>(await Submissions(teacher, true).Feedback(first.Id,
            new(revisionId, second.Version, "Godkend", null, "approved", []), default));
    }

    [Fact]
    public async Task StaleDraftAndDoubleSubmitCannotOverwriteOrDuplicateRevision()
    {
        var draft = await Draft(await Quest());
        Assert.IsType<ConflictObjectResult>(await Submissions(student).SaveDraft(draft.Id,
            new(draft.Version - 1, "Overskriv", []), default));
        var submitted = Value<SubmissionView>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        Assert.IsType<NotFoundResult>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        Assert.Single(await db.SubmissionRevisions.Where(r => r.SubmissionId == draft.Id).ToListAsync());
        Assert.Equal("Vores arbejde", submitted.Revisions[0].Text);
    }

    [Fact]
    public async Task AttachmentsRequireUploaderOwnershipAndMatchingCourse()
    {
        var draft = await Draft(await Quest());
        var privateFile = new FileAsset { Id = Guid.NewGuid(), Key = "peer/code.ino", OwnerSub = peer, HoldId = holdId, OriginalName = "code.ino" };
        db.FileAssets.Add(privateFile);
        await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await Submissions(student).SaveDraft(draft.Id,
            new(draft.Version, "Forsøg", [privateFile.Id]), default));
        Assert.False(await new FileAuthorization(db).CanReadAsync(privateFile, User(student), default));
    }

    [Fact]
    public async Task SharedFilesFollowSnapshotAndRevokingCourseMembershipRevokesFileAccess()
    {
        var group = await Group();
        var draft = await Draft(await Quest("group"), group.Id);
        var asset = new FileAsset { Id = Guid.NewGuid(), Key = "private/report.pdf", OriginalName = "report.pdf",
            OwnerSub = student, HoldId = holdId };
        db.FileAssets.Add(asset);
        await db.SaveChangesAsync();
        draft = Value<SubmissionView>(await Submissions(student).SaveDraft(draft.Id,
            new(draft.Version, "Rapport", [asset.Id]), default));
        var authorization = new FileAuthorization(db);
        Assert.True(await authorization.CanReadAsync(asset, User(peer), default));
        Assert.False(await authorization.CanReadAsync(asset, User(stranger), default));
        var submitted = Value<SubmissionView>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        var current = Assert.Single(Value<LearningGroupView[]>(await Groups().List(holdId, default)));
        await Groups().Update(group.Id, new(holdId, group.Name, [studentMember, strangerMember], current.Version), default);
        Assert.True(await authorization.CanReadAsync(asset, User(peer), default));
        Assert.False(await authorization.CanReadAsync(asset, User(stranger), default));
        db.Memberships.Remove(await db.Memberships.SingleAsync(m => m.Id == peerMember));
        await db.SaveChangesAsync();
        Assert.False(await authorization.CanReadAsync(asset, User(peer), default));
    }

    [Fact]
    public async Task NewGroupMemberCannotReadPrefilledOldDraftOrEarlierRevisionAfterResubmission()
    {
        var group = await Group();
        var draft = await Draft(await Quest("group"), group.Id);
        var oldFile = new FileAsset { Id = Guid.NewGuid(), Key = "old.txt", OriginalName = "old.txt", OwnerSub = student, HoldId = holdId };
        db.FileAssets.Add(oldFile); await db.SaveChangesAsync();
        draft = Value<SubmissionView>(await Submissions(student).SaveDraft(draft.Id, new(draft.Version, "Gammel tekst", [oldFile.Id]), default));
        var first = Value<SubmissionView>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        var returned = Value<SubmissionView>(await Submissions(teacher, true).Feedback(first.Id,
            new(first.Revisions[0].Id, first.Version, "Privat gammel feedback", null, "changes-requested", []), default));
        var current = Assert.Single(Value<LearningGroupView[]>(await Groups().List(holdId, default)));
        await Groups().Update(group.Id, new(holdId, group.Name, [studentMember, strangerMember], current.Version), default);
        Assert.IsType<NotFoundResult>(await Submissions(stranger).Get(first.Id, default));
        Assert.False(await new FileAuthorization(db).CanReadAsync(oldFile, User(stranger), default));
        var next = Value<SubmissionView>(await Submissions(student).SaveDraft(first.Id,
            new(returned.Version, "Nyt fælles arbejde", []), default));
        await Submissions(student).Submit(first.Id, new(next.Version), default);
        var newMember = Value<SubmissionView>(await Submissions(stranger).Get(first.Id, default));
        var visible = Assert.Single(newMember.Revisions);
        Assert.Equal(2, visible.Number);
        Assert.Equal("Nyt fælles arbejde", visible.Text);
        Assert.Empty(visible.Feedback);
        Assert.Empty(visible.Files);
        Assert.False(await new FileAuthorization(db).CanReadAsync(oldFile, User(stranger), default));
    }

    [Fact]
    public async Task FeedbackFilesArePrivateAndScopedToSubmittedVersion()
    {
        var draft = await Draft(await Quest());
        var submission = Value<SubmissionView>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        var attachment = new FileAsset { Id = Guid.NewGuid(), Key = "feedback.txt", OriginalName = "feedback.txt",
            OwnerSub = teacher, HoldId = holdId };
        db.FileAssets.Add(attachment);
        await db.SaveChangesAsync();
        await Submissions(teacher, true).Feedback(submission.Id,
            new(submission.Revisions[0].Id, submission.Version, "Se vedhæftning", null, null, [attachment.Id]), default);
        var authorization = new FileAuthorization(db);
        Assert.True(await authorization.CanReadAsync(attachment, User(student), default));
        Assert.False(await authorization.CanReadAsync(attachment, User(peer), default));
    }

    [Fact]
    public async Task TeacherOutsideCourseCannotReviewAndTeacherActorCannotSubmit()
    {
        var quest = await Quest();
        Assert.IsType<ForbidResult>(await Submissions(teacher, true).Start(new(quest.Id, null), default));
        var draft = await Draft(quest);
        var submitted = Value<SubmissionView>(await Submissions(student).Submit(draft.Id, new(draft.Version), default));
        Assert.IsType<NotFoundResult>(await Submissions(Guid.NewGuid(), true).Feedback(submitted.Id,
            new(submitted.Revisions[0].Id, submitted.Version, "Uvedkommende", null, "approved", []), default));
    }

    [Fact]
    public async Task CourseManagerCanReadLegacyUnscopedMaterialWithoutPublishingToStudents()
    {
        var file = new FileAsset { Id = Guid.NewGuid(), Key = "legacy.txt", OriginalName = "legacy.txt",
            OwnerSub = Guid.NewGuid() };
        db.FileAssets.Add(file);
        db.CourseMaterials.Add(new CourseMaterial { Id = Guid.NewGuid(), HoldId = holdId, FileAsset = file,
            Title = "Kladde", Published = false });
        await db.SaveChangesAsync();
        var authorization = new FileAuthorization(db);
        Assert.True(await authorization.CanReadAsync(file, User(teacher, true), default));
        Assert.False(await authorization.CanReadAsync(file, User(student), default));
        Assert.False(await authorization.CanReadAsync(file, User(Guid.NewGuid(), true), default));
    }

    [Fact]
    public async Task GroupCannotContainMembersFromAnotherCourse()
    {
        var otherCourse = new Forloeb { Id = Guid.NewGuid(), Name = "Andet", CreatedBySub = teacher };
        var otherHold = new Hold { Id = Guid.NewGuid(), Name = "Andet", Forloeb = otherCourse };
        var otherMember = new Membership { Id = Guid.NewGuid(), Hold = otherHold, UserSub = Guid.NewGuid() };
        db.Memberships.Add(otherMember);
        await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await Groups().Create(new(holdId, "Forkert", [studentMember, otherMember.Id]), default));
    }
}
