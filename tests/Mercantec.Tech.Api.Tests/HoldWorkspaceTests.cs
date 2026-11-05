using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class HoldWorkspaceTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), peer = Guid.NewGuid(), outsider = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private Guid courseId, holdId, publishedPageId, draftPageId, questId, pastQuestId, ongoingEventId, nextEventId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Programmering", Description = "Byg en løsning", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = course, LectioHoldKey = "primary" };
        var otherHold = new Hold
        {
            Id = Guid.NewGuid(), Name = "Andet hold",
            Forloeb = new() { Id = Guid.NewGuid(), Name = "Andet forløb", CreatedBySub = outsider }
        };
        courseId = course.Id;
        holdId = hold.Id;
        db.Hold.AddRange(hold, otherHold);
        hold.LectioSources.Add(new() { Id = Guid.NewGuid(), LectioHoldKey = "primary" });
        hold.LectioSources.Add(new() { Id = Guid.NewGuid(), LectioHoldKey = "secondary" });

        var studentMember = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = student, DisplayName = "Ada" };
        var peerMember = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = peer, DisplayName = "Bo" };
        db.Memberships.AddRange(studentMember, peerMember,
            new() { Id = Guid.NewGuid(), Hold = hold, LectioId = "shadow", DisplayName = "Clara" });
        AddGroup(hold.Id, "Projektgruppe", studentMember, peerMember);
        AddGroup(hold.Id, "Sparringsgruppe", studentMember);
        AddGroup(hold.Id, "Anden gruppe", peerMember);
        AddGroup(otherHold.Id, "Andet holds gruppe");

        var publishedPage = new NotionPage
        {
            Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Privat titelkladde", PublishedTitle = "Udgivet introduktion",
            CreatedBySub = teacher, UpdatedAtUtc = now.AddHours(-1), PublishedAtUtc = now.AddDays(-1),
            DraftRevision = 2, PublishedRevision = 1
        };
        var draftPage = new NotionPage
        {
            Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Hemmelig forløbskladde",
            CreatedBySub = teacher, UpdatedAtUtc = now, DraftRevision = 1
        };
        publishedPageId = publishedPage.Id;
        draftPageId = draftPage.Id;
        db.NotionPages.AddRange(publishedPage, draftPage,
            new() { Id = Guid.NewGuid(), HoldId = otherHold.Id, Title = "Andet holds side", PublishedTitle = "Andet holds side", PublishedAtUtc = now });

        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Aflevér projekt", Published = true, DeadlineUtc = now.AddDays(2) };
        var pastQuest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Tidligere aflevering", Published = true, DeadlineUtc = now.AddDays(-2) };
        var otherQuest = new LearningQuest { Id = Guid.NewGuid(), HoldId = otherHold.Id, Title = "Andet holds quest", Published = true, DeadlineUtc = now.AddHours(1) };
        questId = quest.Id;
        pastQuestId = pastQuest.Id;
        db.LearningQuests.AddRange(quest, pastQuest, otherQuest,
            new() { Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Hemmelig questkladde", Published = false, DeadlineUtc = now.AddHours(2) });
        AddSubmission(quest, student, "submitted");
        AddSubmission(quest, peer, "submitted");
        AddSubmission(quest, teacher, "draft");
        AddSubmission(pastQuest, student, "submitted");
        AddSubmission(pastQuest, peer, "approved");
        AddSubmission(otherQuest, outsider, "submitted");

        AddMaterial(hold.Id, "Udgivet materiale", true);
        AddMaterial(hold.Id, "Privat materiale", false);
        AddMaterial(otherHold.Id, "Andet holds materiale", true);

        var ongoing = AddEvent(hold.Id, "Workshop i gang", now.AddHours(-1), now.AddHours(1));
        var next = AddEvent(hold.Id, "Næste undervisning", now.AddDays(1), now.AddDays(1).AddHours(1));
        ongoingEventId = ongoing.Id;
        nextEventId = next.Id;
        AddEvent(hold.Id, "Afsluttet undervisning", now.AddDays(-2), now.AddDays(-1));
        AddEvent(otherHold.Id, "Andet holds begivenhed", now.AddMinutes(30), now.AddHours(1));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task TeacherOverviewIncludesDraftsAndOnlySubmittedWorkFromThisHold()
    {
        var view = Value(await As(teacher, true).Get(courseId, false, default));

        Assert.True(view.CanManage);
        Assert.Equal(courseId, view.CourseId);
        Assert.Equal(holdId, view.HoldId);
        Assert.Equal("H3", view.Name);
        Assert.Equal("Byg en løsning", view.Description);
        Assert.Equal(3, view.MemberCount);
        Assert.Equal(3, view.GroupCount);
        Assert.Equal(2, view.MaterialCount);
        Assert.Equal(3, view.QuestCount);
        Assert.Equal(1, view.DraftQuestCount);
        Assert.Equal(3, view.PendingReviews);
        Assert.Equal(new[] { draftPageId, publishedPageId }, view.Pages.Select(p => p.Id).ToArray());
        Assert.Equal("Privat titelkladde", view.Pages.Single(p => p.Id == publishedPageId).Title);
        Assert.True(view.Pages.Single(p => p.Id == publishedPageId).HasUnpublishedChanges);
        Assert.False(view.Pages.Single(p => p.Id == draftPageId).HasUnpublishedChanges);
        Assert.Collection(view.Reviews,
            review => { Assert.Equal(questId, review.QuestId); Assert.Equal("Aflevér projekt", review.Title); Assert.Equal(2, review.Count); },
            review => { Assert.Equal(pastQuestId, review.QuestId); Assert.Equal(1, review.Count); });
        Assert.Equal(new[] { "primary", "secondary" }, view.LectioKeys.Order().ToArray());
    }

    [Fact]
    public async Task StudentOverviewUsesPublishedTitlesAndCountsEveryGroupTheyBelongTo()
    {
        var view = Value(await As(student).Get(courseId, false, default));

        Assert.False(view.CanManage);
        Assert.Equal(3, view.MemberCount);
        Assert.Equal(2, view.GroupCount);
        Assert.Equal(1, view.MaterialCount);
        Assert.Equal(2, view.QuestCount);
        Assert.Equal(0, view.DraftQuestCount);
        Assert.Equal(0, view.PendingReviews);
        Assert.Empty(view.Reviews);
        var page = Assert.Single(view.Pages);
        Assert.Equal(publishedPageId, page.Id);
        Assert.Equal("Udgivet introduktion", page.Title);
        Assert.False(page.HasUnpublishedChanges);
        AssertPublishedOnly(view);
    }

    [Fact]
    public async Task PublishedOnlyPreviewHidesTeacherDraftsAndReviewQueue()
    {
        var view = Value(await As(teacher, true).Get(courseId, true, default));

        Assert.False(view.CanManage);
        Assert.Equal(1, view.MaterialCount);
        Assert.Equal(2, view.QuestCount);
        Assert.Equal(0, view.DraftQuestCount);
        Assert.Equal(0, view.PendingReviews);
        Assert.Empty(view.Reviews);
        Assert.Equal("Udgivet introduktion", Assert.Single(view.Pages).Title);
        Assert.False(view.Pages[0].HasUnpublishedChanges);
        Assert.Equal(0, view.GroupCount);
        AssertPublishedOnly(view);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnrelatedAccountsCannotReadAnExistingHold(bool teacherRole)
    {
        Assert.IsType<NotFoundResult>(await As(outsider, teacherRole).Get(courseId, false, default));
        Assert.IsType<NotFoundResult>(await As(outsider, teacherRole).Get(courseId, true, default));
    }

    [Fact]
    public async Task EndpointRequiresAnAuthenticatedSubjectAndUsesCourseIdInsteadOfHoldId()
    {
        var controller = As(teacher, true);
        Assert.IsType<NotFoundResult>(await controller.Get(holdId, false, default));
        Assert.IsType<NotFoundResult>(await controller.Get(Guid.NewGuid(), false, default));
        controller.HttpContext.User = new();
        Assert.IsType<UnauthorizedResult>(await controller.Get(courseId, false, default));
        Assert.NotEmpty(typeof(HoldWorkspaceController).GetCustomAttributes(typeof(AuthorizeAttribute), false));
    }

    [Fact]
    public async Task AgendaCombinesOngoingEventsAndPublishedDeadlinesInDateOrder()
    {
        var view = Value(await As(student).Get(courseId, false, default));

        Assert.Collection(view.Agenda,
            item => { Assert.Equal(ongoingEventId, item.Id); Assert.Equal("event", item.Kind); Assert.Equal("204", item.Location); },
            item => { Assert.Equal(nextEventId, item.Id); Assert.Equal("event", item.Kind); },
            item => { Assert.Equal(questId, item.Id); Assert.Equal("deadline", item.Kind); Assert.Null(item.Location); });
        Assert.Equal(view.Agenda.Select(a => a.StartUtc).Order(), view.Agenda.Select(a => a.StartUtc));
    }

    [Fact]
    public async Task AgendaReturnsTheSixNearestItemsRatherThanInsertionOrder()
    {
        for (var day = 10; day >= 3; day--)
            AddEvent(holdId, $"Undervisning dag {day}", now.AddDays(day), now.AddDays(day).AddHours(1));
        await db.SaveChangesAsync();

        var view = Value(await As(teacher, true).Get(courseId, false, default));

        Assert.Equal(6, view.Agenda.Length);
        Assert.Equal(new[] { "Workshop i gang", "Næste undervisning", "Aflevér projekt", "Undervisning dag 3", "Undervisning dag 4", "Undervisning dag 5" },
            view.Agenda.Select(a => a.Title).ToArray());
    }

    private static void AssertPublishedOnly(HoldWorkspaceView view)
    {
        var json = JsonSerializer.Serialize(view);
        Assert.DoesNotContain("Privat titelkladde", json);
        Assert.DoesNotContain("Hemmelig forløbskladde", json);
        Assert.DoesNotContain("Hemmelig questkladde", json);
        Assert.DoesNotContain("Andet holds", json);
    }

    private HoldWorkspaceController As(Guid subject, bool teacherRole = false)
    {
        var claims = new List<Claim> { new("sub", subject.ToString()) };
        if (teacherRole) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db, new(db))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } }
        };
    }

    private static HoldWorkspaceView Value(IActionResult result) =>
        Assert.IsType<HoldWorkspaceView>(Assert.IsType<OkObjectResult>(result).Value);

    private void AddGroup(Guid groupHoldId, string name, params Membership[] members)
    {
        var group = new LearningGroup { Id = Guid.NewGuid(), HoldId = groupHoldId, Name = name };
        foreach (var member in members) group.Members.Add(new() { Membership = member });
        db.LearningGroups.Add(group);
    }

    private void AddSubmission(LearningQuest quest, Guid subject, string state) => db.QuestSubmissions.Add(new()
    {
        Id = Guid.NewGuid(), Quest = quest, CreatedBySub = subject, OwnerKey = subject.ToString(), State = state, UpdatedAtUtc = now
    });

    private void AddMaterial(Guid materialHoldId, string title, bool published) => db.CourseMaterials.Add(new()
    {
        Id = Guid.NewGuid(), HoldId = materialHoldId, Title = title, Published = published,
        FileAsset = new() { Id = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), OriginalName = "opgave.pdf", OwnerSub = teacher, HoldId = materialHoldId }
    });

    private HoldCalendarEvent AddEvent(Guid eventHoldId, string title, DateTimeOffset start, DateTimeOffset end)
    {
        var item = new HoldCalendarEvent { Id = Guid.NewGuid(), HoldId = eventHoldId, Title = title, StartUtc = start, EndUtc = end, Location = "204" };
        db.HoldCalendarEvents.Add(item);
        return item;
    }
}
