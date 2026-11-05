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

public sealed class MaterialsControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId;
    private FileAsset file = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Forloeb = course, Name = "H3" };
        holdId = hold.Id;
        db.Memberships.Add(new() { Id = Guid.NewGuid(), Hold = hold, UserSub = student });
        file = new() { Id = Guid.NewGuid(), Key = "material.pdf", OriginalName = "material.pdf", OwnerSub = teacher, HoldId = holdId };
        db.FileAssets.Add(file);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private static ClaimsPrincipal User(Guid sub, bool teacherRole = false)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) };
        if (teacherRole) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(new ClaimsIdentity(claims, "test"));
    }

    private MaterialsController As(Guid sub, bool teacherRole = false) => new(db, new(db))
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = User(sub, teacherRole) } }
    };

    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    [Fact]
    public async Task PublishingControlsListingAndAccessToOriginalAndPreview()
    {
        var material = Value<CourseMaterialView>(await As(teacher, true).Create(new(holdId, file.Id, "Noter", null, false), default));
        Assert.Empty(Value<List<CourseMaterialView>>(await As(student).List(holdId, default)));
        Assert.False(await new FileAuthorization(db).CanReadAsync(file, User(student), default));
        await As(teacher, true).Update(material.Id, new("Noter", "Læs før timen", true), default);
        Assert.Single(Value<List<CourseMaterialView>>(await As(student).List(holdId, default)));
        Assert.True(await new FileAuthorization(db).CanReadAsync(file, User(student), default));
        await As(teacher, true).Update(material.Id, new("Noter", null, false), default);
        Assert.False(await new FileAuthorization(db).CanReadAsync(file, User(student), default));
    }

    [Fact]
    public async Task OutsideTeacherCannotListPublishOrEditCourseMaterials()
    {
        var material = Value<CourseMaterialView>(await As(teacher, true).Create(new(holdId, file.Id, "Noter", null, true), default));
        Assert.IsType<NotFoundResult>(await As(outsider, true).List(holdId, default));
        Assert.IsType<NotFoundResult>(await As(outsider, true).Create(new(holdId, file.Id, "Noter", null, true), default));
        Assert.IsType<NotFoundResult>(await As(outsider, true).Update(material.Id, new("Ændret", null, false), default));
        Assert.False(await new FileAuthorization(db).CanReadAsync(file, User(outsider, true), default));
    }

    [Fact]
    public async Task TeacherCannotPublishAnotherUsersPrivateUpload()
    {
        file.OwnerSub = student;
        await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await As(teacher, true).Create(new(holdId, file.Id, "Elevfil", null, true), default));
        Assert.Empty(await db.CourseMaterials.ToListAsync());
    }
}
