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

public sealed class TeachingCoursesTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId, pageId, childId, draftPageId, questId, draftQuestId, materialId, draftMaterialId, eventId, foreignPageId;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher } };
        var other = new Hold { Id = Guid.NewGuid(), Name = "H4", Forloeb = new() { Id = Guid.NewGuid(), Name = "H4", CreatedBySub = outsider } };
        holdId = hold.Id; otherHoldId = other.Id; db.Hold.AddRange(hold, other);
        db.Memberships.Add(new() { Id = Guid.NewGuid(), Hold = hold, UserSub = student, DisplayName = "Ada" });
        pageId = Page(holdId, "Ny privat titel", true, "Udgivet introduktion");
        childId = Page(holdId, "Underside", true, "Udgivet underside", pageId);
        draftPageId = Page(holdId, "Privat side", false);
        foreignPageId = Page(otherHoldId, "Andet holds side", true, "Andet hold");
        questId = Quest(holdId, "Main quest", true); draftQuestId = Quest(holdId, "Privat quest", false);
        materialId = Material(holdId, "Oplæg", true); draftMaterialId = Material(holdId, "Privat oplæg", false);
        eventId = Guid.NewGuid();
        db.HoldCalendarEvents.Add(new() { Id = eventId, HoldId = holdId, Title = "Workshop", StartUtc = DateTimeOffset.UtcNow.AddDays(1), EndUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(2) });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private Guid Page(Guid hold, string title, bool published, string? publishedTitle = null, Guid? parent = null)
    { var id = Guid.NewGuid(); db.NotionPages.Add(new() { Id = id, HoldId = hold, Title = title, ParentPageId = parent, PublishedTitle = publishedTitle, PublishedAtUtc = published ? DateTimeOffset.UtcNow : null }); return id; }
    private Guid Quest(Guid hold, string title, bool published)
    { var id = Guid.NewGuid(); db.LearningQuests.Add(new() { Id = id, HoldId = hold, Title = title, Published = published }); return id; }
    private Guid Material(Guid hold, string title, bool published)
    {
        var id = Guid.NewGuid(); var file = new FileAsset { Id = Guid.NewGuid(), HoldId = hold, OwnerSub = teacher, Key = Guid.NewGuid().ToString(), OriginalName = "oplaeg.pdf" };
        db.CourseMaterials.Add(new() { Id = id, HoldId = hold, Title = title, Published = published, FileAsset = file }); return id;
    }
    private TeachingCoursesController Api(Guid sub, bool isTeacher = false)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) }; if (isTeacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db, new(db)) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } } };
    }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private static SaveTeachingCourse Draft(string title = "Fra idé til spil") => new(title, "Byg et spil", "Eleven kan teste sin løsning.", new(2026, 11, 6), new(2026, 11, 20));
    private Task<IActionResult> Create(string title = "Fra idé til spil", bool published = false) => Api(teacher, true).Create(holdId, Draft(title) with { Published = published }, default);
    private async Task<TeachingCourseView> Link(TeachingCourseView course, params TeachingCourseResourceReference[] resources) =>
        Value<TeachingCourseView>(await Api(teacher, true).SaveResources(holdId, course.Id, new(course.Version, resources), default));

    [Fact]
    public async Task HoldCanHaveSeveralSequencesWithIndependentPeriodAndObjectives()
    {
        var first = Value<TeachingCourseView>(await Create());
        var second = Value<TeachingCourseView>(await Create("Databaser"));
        Assert.NotEqual(first.Id, second.Id);
        var workspace = Value<TeachingCourseWorkspace>(await Api(teacher, true).List(holdId, false, default));
        Assert.True(workspace.CanManage); Assert.Equal(2, workspace.Courses.Length);
        Assert.Equal(new DateOnly(2026, 11, 6), first.StartsOn);
        Assert.Equal("Eleven kan teste sin løsning.", first.LearningObjectives);
        Assert.IsType<BadRequestObjectResult>(await Api(teacher, true).Create(holdId, Draft() with { EndsOn = new(2026, 11, 1) }, default));
    }

    [Fact]
    public async Task OnlyManagingTeachersCanCreateEditOrReadCatalog()
    {
        var course = Value<TeachingCourseView>(await Create());
        Assert.IsType<NotFoundResult>(await Api(student).Create(holdId, Draft(), default));
        Assert.IsType<NotFoundResult>(await Api(student).Update(holdId, course.Id, Draft(), default));
        Assert.IsType<NotFoundResult>(await Api(student).Catalog(holdId, default));
        Assert.IsType<NotFoundResult>(await Api(outsider, true).List(holdId, false, default));
        Assert.IsType<NotFoundResult>(await Api(teacher, true).Update(otherHoldId, course.Id, Draft(), default));
        Assert.Single(await db.TeachingCourses.ToListAsync());
    }

    [Fact]
    public async Task ResourcesAreHoldScopedAndAResourceCanOnlyBelongToOneSequence()
    {
        var course = Value<TeachingCourseView>(await Create());
        var other = Value<TeachingCourseView>(await Create("Andet emne"));
        Assert.IsType<BadRequestObjectResult>(await Api(teacher, true).SaveResources(holdId, course.Id,
            new(course.Version, [new TeachingCourseResourceReference("page", foreignPageId)]), default));
        Assert.Empty(await db.TeachingCourseResources.ToListAsync());
        course = await Link(course, new TeachingCourseResourceReference("page", pageId), new TeachingCourseResourceReference("page", childId), new TeachingCourseResourceReference("quest", questId), new TeachingCourseResourceReference("material", materialId), new TeachingCourseResourceReference("event", eventId));
        Assert.Equal(5, course.Resources.Length);
        Assert.Equal(pageId, course.Resources.Single(r => r.ResourceId == childId).ParentPageId);
        Assert.IsType<ConflictObjectResult>(await Api(teacher, true).SaveResources(holdId, other.Id,
            new(other.Version, [new TeachingCourseResourceReference("quest", questId)]), default));
        Assert.Equal(5, await db.TeachingCourseResources.CountAsync());
        Assert.Equal(course.Id, Value<TeachingCourseResourceView[]>(await Api(teacher, true).Catalog(holdId, default)).Single(r => r.ResourceId == questId).LinkedCourseId);
    }

    [Fact]
    public async Task StudentReadsOnlyPublishedActiveSequencesAndPublishedSnapshotTitles()
    {
        var visible = Value<TeachingCourseView>(await Create(published: true));
        await Create("Hemmelig kladde");
        var archived = Value<TeachingCourseView>(await Create("Gammelt forløb", true));
        await Api(teacher, true).Update(holdId, archived.Id, Draft(archived.Title) with { Published = true, Archived = true }, default);
        visible = await Link(visible, new TeachingCourseResourceReference("page", pageId), new TeachingCourseResourceReference("page", childId), new TeachingCourseResourceReference("page", draftPageId), new TeachingCourseResourceReference("quest", questId), new TeachingCourseResourceReference("quest", draftQuestId), new TeachingCourseResourceReference("material", materialId), new TeachingCourseResourceReference("material", draftMaterialId), new TeachingCourseResourceReference("event", eventId));
        var workspace = Value<TeachingCourseWorkspace>(await Api(student).List(holdId, false, default));
        Assert.False(workspace.CanManage);
        var studentCourse = Assert.Single(workspace.Courses); Assert.Equal(visible.Id, studentCourse.Id);
        Assert.Equal(5, studentCourse.Resources.Length);
        Assert.All(studentCourse.Resources, r => Assert.True(r.Published));
        Assert.Contains(studentCourse.Resources, r => r.Title == "Udgivet introduktion");
        Assert.DoesNotContain(studentCourse.Resources, r => r.Title.Contains("Privat", StringComparison.OrdinalIgnoreCase) || r.Title == "Ny privat titel");
        var preview = Value<TeachingCourseWorkspace>(await Api(teacher, true).List(holdId, true, default));
        Assert.False(preview.CanManage); Assert.Equal(studentCourse.Resources, Assert.Single(preview.Courses).Resources);
    }

    [Fact]
    public async Task EditingUsesVersionsAndArchiveKeepsResourcesAndExistingContent()
    {
        var course = Value<TeachingCourseView>(await Create(published: true));
        course = await Link(course, new TeachingCourseResourceReference("quest", questId));
        var archived = Value<TeachingCourseView>(await Api(teacher, true).Update(holdId, course.Id,
            Draft(course.Title) with { Published = true, Archived = true, Version = course.Version }, default));
        Assert.True(archived.Archived); Assert.Single(archived.Resources);
        Assert.True(await db.LearningQuests.AnyAsync(q => q.Id == questId));
        Assert.Empty(Value<TeachingCourseWorkspace>(await Api(student).List(holdId, false, default)).Courses);
        Assert.IsType<ConflictObjectResult>(await Api(teacher, true).Update(holdId, course.Id, Draft(), default));
        Assert.IsType<ConflictObjectResult>(await Api(teacher, true).CreatePage(holdId, archived.Id, new("Ny", Version: archived.Version), default));
        var restored = Value<TeachingCourseView>(await Api(teacher, true).Update(holdId, archived.Id,
            Draft(archived.Title) with { Published = true, Version = archived.Version }, default));
        Assert.False(restored.Archived); Assert.Single(restored.Resources);
        Assert.Single(Value<TeachingCourseWorkspace>(await Api(student).List(holdId, false, default)).Courses);
    }

    [Fact]
    public async Task PageCreationLinksItsHierarchyAtomicallyAndRejectsForeignOrStaleParentRequests()
    {
        var course = Value<TeachingCourseView>(await Create());
        var root = Value<TeachingCoursePageCreated>(await Api(teacher, true).CreatePage(holdId, course.Id, new("Introduktion", Version: course.Version), default));
        var child = Value<TeachingCoursePageCreated>(await Api(teacher, true).CreatePage(holdId, course.Id, new("Øvelse", root.PageId, root.Course.Version), default));
        Assert.Equal(root.PageId, (await db.NotionPages.SingleAsync(p => p.Id == child.PageId)).ParentPageId);
        Assert.Equal(2, child.Course.Resources.Length);
        var count = await db.NotionPages.CountAsync();
        Assert.IsType<ConflictObjectResult>(await Api(teacher, true).CreatePage(holdId, course.Id, new("Stale", Version: 0), default));
        Assert.IsType<BadRequestObjectResult>(await Api(teacher, true).CreatePage(holdId, course.Id, new("Forkert", foreignPageId, child.Course.Version), default));
        Assert.IsType<NotFoundResult>(await Api(student).CreatePage(holdId, course.Id, new("Elev", Version: child.Course.Version), default));
        Assert.Equal(count, await db.NotionPages.CountAsync());
    }

    [Fact]
    public async Task ResourceReplacementRejectsStaleVersionsAndDoesNotDeleteResourceDocuments()
    {
        var course = Value<TeachingCourseView>(await Create());
        var linked = await Link(course, new TeachingCourseResourceReference("page", pageId));
        Assert.IsType<ConflictObjectResult>(await Api(teacher, true).SaveResources(holdId, course.Id,
            new(course.Version, [new TeachingCourseResourceReference("quest", questId)]), default));
        Assert.Equal(pageId, Assert.Single(await db.TeachingCourseResources.ToListAsync()).ResourceId);
        var cleared = await Link(linked);
        Assert.Empty(cleared.Resources);
        Assert.True(await db.NotionPages.AnyAsync(p => p.Id == pageId));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("page")]
    public async Task InvalidResourceReferencesDoNotPartiallyChangeASequence(string kind)
    {
        var course = Value<TeachingCourseView>(await Create());
        Assert.IsType<BadRequestObjectResult>(await Api(teacher, true).SaveResources(holdId, course.Id,
            new(course.Version, [new TeachingCourseResourceReference("quest", questId), new(kind, Guid.NewGuid())]), default));
        Assert.Empty(await db.TeachingCourseResources.ToListAsync());
    }
}
