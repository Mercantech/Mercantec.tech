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

public sealed class MyHoldsTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid teacher = Guid.NewGuid();
    private readonly Guid otherTeacher = Guid.NewGuid();
    private readonly Guid student = Guid.NewGuid();
    private readonly Guid holdId = Guid.NewGuid();
    private readonly Guid courseId = Guid.NewGuid();
    private readonly Guid otherHoldId = Guid.NewGuid();
    private AppDbContext db = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var hold = new Hold
        {
            Id = holdId,
            Name = "H3 PRO",
            Forloeb = new() { Id = courseId, Name = "Programmering", Description = "Byg en app", CreatedBySub = teacher },
            LectioHoldKey = "h3-pro",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        hold.LectioSources.Add(new()
        {
            Id = Guid.NewGuid(),
            LectioHoldKey = "h3-pro",
            LectioHoldName = "H3 / Programmering"
        });
        hold.Memberships.Add(new() { Id = Guid.NewGuid(), UserSub = student, DisplayName = "Ada" });
        hold.Memberships.Add(new() { Id = Guid.NewGuid(), LectioId = "123", DisplayName = "Bo", Source = "lectio" });
        var otherHold = new Hold
        {
            Id = otherHoldId,
            Name = "H4",
            Forloeb = new() { Id = Guid.NewGuid(), Name = "Sikkerhed", CreatedBySub = otherTeacher },
            LectioHoldKey = "legacy-h4"
        };
        otherHold.Memberships.Add(new() { Id = Guid.NewGuid(), UserSub = student });
        db.Hold.AddRange(hold, otherHold);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Mine_scopes_cards_and_roster_counts_to_current_access()
    {
        var teacherApi = As(teacher, "Underviser");
        Assert.IsType<NoContentResult>(await teacherApi.AddFavorite(holdId, default));

        var own = Assert.Single(Cards(await teacherApi.Mine(null, default)));
        Assert.Equal(holdId, own.HoldId);
        Assert.Equal(courseId, own.ForloebId);
        Assert.Equal("H3 PRO", own.HoldName);
        Assert.Equal("Programmering", own.ForloebName);
        Assert.Equal("Byg en app", own.Description);
        Assert.True(own.CanManage);
        Assert.True(own.IsFavorite);
        Assert.Equal(2, own.MemberCount);
        Assert.Equal(1, own.UnlinkedMemberCount);
        Assert.Equal(new HoldLectioBadge("h3-pro", "H3 / Programmering"), Assert.Single(own.LectioSources));

        var studentApi = As(student, "Elev");
        var ownMemberships = Cards(await studentApi.Mine(null, default));
        Assert.Equal(2, ownMemberships.Length);
        Assert.All(ownMemberships, AssertReadOnly);
        Assert.Equal(new HoldLectioBadge("legacy-h4", null),
            Assert.Single(ownMemberships.Single(h => h.HoldId == otherHoldId).LectioSources));

        var preview = Assert.Single(Cards(await teacherApi.Mine(student, default)));
        Assert.Equal(holdId, preview.HoldId);
        AssertReadOnly(preview);
        Assert.IsType<ForbidResult>(await studentApi.Mine(teacher, default));
    }

    [Fact]
    public async Task Favorites_are_idempotent_and_private_to_the_current_account()
    {
        var teacherApi = As(teacher, "Underviser");
        var studentApi = As(student, "Elev");
        Assert.IsType<NotFoundResult>(await teacherApi.AddFavorite(otherHoldId, default));
        Assert.IsType<NoContentResult>(await teacherApi.AddFavorite(holdId, default));
        Assert.IsType<NoContentResult>(await teacherApi.AddFavorite(holdId, default));
        Assert.Equal(1, await db.HoldFavorites.CountAsync());
        Assert.False(Cards(await studentApi.Mine(null, default)).Single(h => h.HoldId == holdId).IsFavorite);

        Assert.IsType<NoContentResult>(await studentApi.AddFavorite(holdId, default));
        Assert.IsType<NoContentResult>(await teacherApi.RemoveFavorite(holdId, default));
        Assert.IsType<NoContentResult>(await teacherApi.RemoveFavorite(holdId, default));
        Assert.False(Assert.Single(Cards(await teacherApi.Mine(null, default))).IsFavorite);
        Assert.True(Cards(await studentApi.Mine(null, default)).Single(h => h.HoldId == holdId).IsFavorite);
    }

    [Fact]
    public async Task Renaming_requires_hold_management_and_preserves_course_and_lectio_identities()
    {
        var teacherApi = As(teacher, "Underviser");
        Assert.IsType<NotFoundResult>(await As(student, "Elev").Rename(holdId, new("Elevens navn"), default));
        Assert.IsType<NotFoundResult>(await As(otherTeacher, "Underviser").Rename(holdId, new("Anden lærer"), default));
        Assert.IsType<BadRequestObjectResult>(await teacherApi.Rename(holdId, new("Linje\nnavn"), default));
        Assert.IsType<NoContentResult>(await teacherApi.Rename(holdId, new("  Mit programmeringshold  "), default));

        var card = Assert.Single(Cards(await teacherApi.Mine(null, default)));
        Assert.Equal(holdId, card.HoldId);
        Assert.Equal(courseId, card.ForloebId);
        Assert.Equal("Mit programmeringshold", card.HoldName);
        Assert.Equal("Programmering", card.ForloebName);
        Assert.Equal("h3-pro", Assert.Single(card.LectioSources).Key);
        Assert.Equal(2, await db.Memberships.CountAsync(m => m.HoldId == holdId));
    }

    [Fact]
    public async Task Legacy_course_edit_keeps_a_manual_holds_custom_name_and_checks_management()
    {
        var manualHold = new Hold
        {
            Id = Guid.NewGuid(),
            Name = "Workshop",
            Forloeb = new() { Id = Guid.NewGuid(), Name = "Workshop", CreatedBySub = teacher }
        };
        db.Hold.Add(manualHold);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var otherApi = new ForloebController(db, null!)
        {
            ControllerContext = As(otherTeacher, "Underviser").ControllerContext
        };
        Assert.IsType<NotFoundResult>((await otherApi.Update(manualHold.ForloebId, new("Andet navn", null), default)).Result);

        var teacherApi = As(teacher, "Underviser");
        var legacyApi = new ForloebController(db, null!) { ControllerContext = teacherApi.ControllerContext };
        Assert.IsType<OkObjectResult>((await legacyApi.Update(manualHold.ForloebId, new("Workshop 2", null), default)).Result);
        Assert.Equal("Workshop 2", await db.Hold.Where(h => h.Id == manualHold.Id).Select(h => h.Name).SingleAsync());

        await teacherApi.Rename(manualHold.Id, new("Mit fredagshold"), default);
        Assert.IsType<OkObjectResult>((await legacyApi.Update(manualHold.ForloebId, new("Workshop 3", null), default)).Result);
        Assert.Equal("Mit fredagshold", await db.Hold.Where(h => h.Id == manualHold.Id).Select(h => h.Name).SingleAsync());
    }

    private HoldsController As(Guid subject, string role) => new(db)
    {
        ControllerContext = new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", subject.ToString()), new("role", role)], "test"))
            }
        }
    };

    private static MyHoldCard[] Cards(IActionResult result) =>
        Assert.IsType<MyHoldCard[]>(Assert.IsType<OkObjectResult>(result).Value);

    private static void AssertReadOnly(MyHoldCard card)
    {
        Assert.False(card.CanManage);
        Assert.False(card.IsFavorite);
        Assert.Equal(0, card.MemberCount);
        Assert.Equal(0, card.UnlinkedMemberCount);
    }
}
