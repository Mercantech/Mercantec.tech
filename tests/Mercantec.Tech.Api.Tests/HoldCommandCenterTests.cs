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

public sealed class HoldCommandCenterTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Programmering", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = course };
        var other = new Hold { Id = Guid.NewGuid(), Name = "H4", Forloeb = new() { Id = Guid.NewGuid(), Name = "Andet", CreatedBySub = outsider } };
        holdId = hold.Id; otherHoldId = other.Id;
        db.Hold.Add(other);
        db.Memberships.Add(new() { Id = Guid.NewGuid(), Hold = hold, UserSub = student, DisplayName = "Ada" });
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
    private HoldCalendarController Calendar(Guid subject, bool isTeacher = false) => As(new HoldCalendarController(db, new(db)), subject, isTeacher);
    private QuestsController Quests(Guid subject, bool isTeacher = false) => As(new QuestsController(db, new(db)), subject, isTeacher);
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private SaveHoldCalendarEvent Event() => new(holdId, "Workshop", "Byg spil", new(2026, 11, 5, 8, 0, 0, TimeSpan.FromHours(1)), new(2026, 11, 5, 10, 0, 0, TimeSpan.FromHours(1)), "204", "workshop");

    [Theory]
    [InlineData("main")]
    [InlineData("daily")]
    [InlineData("side")]
    public async Task QuestKindsPersistAndAreReadableThroughDeepLinks(string kind)
    {
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Byg en løsning", "Aflevér kode", "individual", true, null, kind), default));
        db.ChangeTracker.Clear();
        var visible = Value<QuestView>(await Quests(student).Get(quest.Id, default));
        Assert.Equal(kind, visible.Kind);
        Assert.IsType<NotFoundResult>(await Quests(outsider).Get(quest.Id, default));
    }

    [Fact]
    public async Task InvalidQuestKindAndStudentCreationAreRejected()
    {
        var invalid = new SaveQuest(holdId, "Quest", "Opgave", "individual", true, null, "unknown");
        Assert.IsType<BadRequestObjectResult>(await Quests(teacher, true).Create(invalid, default));
        Assert.IsType<ForbidResult>(await Quests(student).Create(invalid with { Kind = "main" }, default));
        Assert.Empty(await db.LearningQuests.ToListAsync());
    }

    [Fact]
    public async Task DraftQuestsAreOnlyVisibleToManagingTeachers()
    {
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Kladde", "", "group", false, null, "side"), default));
        Assert.IsType<NotFoundResult>(await Quests(student).Get(quest.Id, default));
        Assert.Equal("side", Value<QuestView>(await Quests(teacher, true).Get(quest.Id, default)).Kind);
    }

    [Fact]
    public async Task CalendarIsHoldScopedAndStudentReadOnly()
    {
        var item = Value<HoldCalendarEventView>(await Calendar(teacher, true).Create(holdId, Event(), default));
        Assert.Equal(TimeSpan.Zero, item.StartUtc.Offset);
        Assert.Equal(7, item.StartUtc.Hour);
        Assert.Single(Value<HoldCalendarEventView[]>(await Calendar(student).List(holdId, default)));
        Assert.IsType<NotFoundResult>(await Calendar(outsider).List(holdId, default));
        Assert.IsType<NotFoundResult>(await Calendar(student).Create(holdId, Event(), default));
        Assert.IsType<NotFoundResult>(await Calendar(student).Update(holdId, item.Id, Event(), default));
        Assert.IsType<NotFoundResult>(await Calendar(student).Delete(holdId, item.Id, 0, default));
        Assert.IsType<NotFoundResult>(await Calendar(teacher, true).Create(otherHoldId, Event() with { HoldId = otherHoldId }, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, Event() with { HoldId = otherHoldId }, default));
    }

    [Fact]
    public async Task CalendarRejectsInvalidTimesAndUnknownKinds()
    {
        var request = Event();
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, request with { EndUtc = request.StartUtc }, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, request with { EndUtc = request.StartUtc.AddDays(32) }, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, request with { Kind = "unknown" }, default));
        Assert.Empty(await db.HoldCalendarEvents.ToListAsync());
    }

    [Fact]
    public async Task CalendarUpdateAndDeleteRejectStaleVersions()
    {
        var item = Value<HoldCalendarEventView>(await Calendar(teacher, true).Create(holdId, Event(), default));
        var updated = Value<HoldCalendarEventView>(await Calendar(teacher, true).Update(holdId, item.Id, Event() with { Title = "Nyt navn", Version = item.Version }, default));
        Assert.Equal(item.Version + 1, updated.Version);
        Assert.IsType<ConflictObjectResult>(await Calendar(teacher, true).Update(holdId, item.Id, Event() with { Version = item.Version }, default));
        Assert.IsType<ConflictObjectResult>(await Calendar(teacher, true).Delete(holdId, item.Id, item.Version, default));
        Assert.IsType<OkObjectResult>(await Calendar(teacher, true).Delete(holdId, item.Id, updated.Version, default));
        Assert.Empty(Value<HoldCalendarEventView[]>(await Calendar(student).List(holdId, default)));
    }
}
