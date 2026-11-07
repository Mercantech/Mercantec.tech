using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebooksControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private AppDbContext db = null!;
    private readonly Guid owner = Guid.NewGuid(), reader = Guid.NewGuid(), teacher = Guid.NewGuid(), stranger = Guid.NewGuid();
    private Notebook notebook = null!;
    private NotebookDocument parent = null!, child = null!, descendant = null!, hidden = null!, template = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        notebook = new() { Id = Guid.NewGuid(), OwnerSub = owner, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Notebooks.Add(notebook);
        db.NotebookProfiles.AddRange(
            new() { Sub = owner, DisplayName = "Anna", UpdatedAtUtc = DateTimeOffset.UtcNow },
            new() { Sub = reader, DisplayName = "Bo", UpdatedAtUtc = DateTimeOffset.UtcNow },
            new() { Sub = teacher, DisplayName = "Lærer", IsTeacher = true, UpdatedAtUtc = DateTimeOffset.UtcNow });
        parent = Page("Skjult mappe");
        child = Page("SQL-refleksion", parent.Id); child.SearchText = "SELECT og JOIN giver mig forståelse";
        child.TagsJson = "[\"sql\",\"refleksion\"]";
        descendant = Page("Skjult underside", child.Id);
        hidden = Page("Hemmelig C#", parent.Id); hidden.SearchText = "hemmeligt indhold";
        template = new() { Id = Guid.NewGuid(), Kind = "template", Title = "Daglig logbog",
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, YjsState = [9] };
        db.NotebookDocuments.Add(template);
        db.NotebookLinks.AddRange(new() { FromId = hidden.Id, ToId = child.Id }, new() { FromId = child.Id, ToId = parent.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private NotebookDocument Page(string title, Guid? parentId = null)
    {
        var page = new NotebookDocument { Id = Guid.NewGuid(), NotebookId = notebook.Id, Title = title,
            ParentPageId = parentId, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            YjsState = [1, 2], Fingerprint = new('a', 64), Revision = 1 };
        db.NotebookDocuments.Add(page);
        return page;
    }

    private static ClaimsPrincipal Principal(Guid sub, string? role = null)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()), new("name", sub.ToString() == Guid.Empty.ToString() ? "" : "Testnavn") };
        if (role is not null) claims.Add(new("role", role));
        return new(new ClaimsIdentity(claims, "test"));
    }

    private NotebooksController As(Guid sub, string? role = null) => new(db, new(db))
    { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = Principal(sub, role) } } };
    private static T Value<T>(IActionResult action) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(action).Value);

    [Fact]
    public async Task MineIsOnePermanentNotebookAndProfileComesFromAuthenticatedClaims()
    {
        var first = Value<NotebookInfo>(await As(owner).Mine(default));
        var again = Value<NotebookInfo>(await As(owner).Mine(default));
        Assert.Equal(notebook.Id, first.Id); Assert.Equal(first, again);
        Assert.True(first.CanEdit); Assert.True(first.CanShare);
        Assert.Single(await db.Notebooks.Where(n => n.OwnerSub == owner).ToArrayAsync());
        Assert.Equal("Testnavn", (await db.NotebookProfiles.FindAsync(owner))!.DisplayName);
        var other = Value<NotebookInfo>(await As(reader).Mine(default));
        Assert.NotEqual(first.Id, other.Id);
        var anonymous = As(owner); anonymous.HttpContext.User = new();
        Assert.IsType<UnauthorizedResult>(await anonymous.Mine(default));
        Assert.NotEmpty(typeof(NotebooksController).GetCustomAttributes(typeof(AuthorizeAttribute), false));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Underviser")]
    [InlineData("Teacher")]
    [InlineData("instructor")]
    public async Task EveryTeacherCanReadAndCommentWithoutBeingAbleToEditStudentText(string role)
    {
        var controller = As(teacher, role);
        var context = Value<NotebookDocumentContext>(await controller.Context(child.Id, default));
        Assert.True(context.CanRead); Assert.True(context.CanComment);
        Assert.False(context.CanEdit); Assert.False(context.CanShare);
        Assert.Equal(Convert.ToBase64String(child.YjsState!), context.StateBase64);
        Assert.IsType<NotFoundResult>(await controller.Update(child.Id, new("Overtag", null, []), default));
        Assert.IsType<NotFoundResult>(await controller.Create(notebook.Id, new("Lærerændring"), default));
        Assert.IsType<NotFoundResult>(await controller.AuthorizeDocument(new("notebook:" + child.Id), default));
        Assert.IsType<NotFoundResult>(await controller.GrantShare(notebook.Id, new(reader), default));
        Assert.IsType<OkObjectResult>(await controller.AddComment(child.Id, new("God refleksion"), default));
        Assert.Single(Value<NotebookCommentView[]>(await As(owner).Comments(child.Id, default)));
        Assert.IsType<OkObjectResult>(await controller.AuthorizeDocument(new("notebook:" + template.Id), default));
    }

    [Fact]
    public async Task ExactPageShareDoesNotExposeAncestorsDescendantsSearchOrHiddenBacklinks()
    {
        var share = Value<NotebookShareView>(await As(owner).GrantShare(notebook.Id, new(reader, child.Id), default));
        var controller = As(reader);
        var context = Value<NotebookDocumentContext>(await controller.Context(child.Id, default));
        Assert.False(context.CanEdit); Assert.False(context.CanComment); Assert.False(context.CanShare);
        Assert.Equal(child.Id, Assert.Single(context.Pages).Id);
        Assert.Null(context.Pages[0].ParentPageId);
        Assert.IsType<NotFoundResult>(await controller.Context(parent.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Context(descendant.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Context(hidden.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Context(template.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Comments(child.Id, default));
        Assert.IsType<NotFoundResult>(await controller.AddComment(child.Id, new("Uønsket"), default));
        Assert.IsType<NotFoundResult>(await controller.AuthorizeDocument(new("notebook:" + child.Id), default));
        var pages = Value<NotebookPageList>(await controller.Pages(notebook.Id, default));
        Assert.Equal(1, pages.Total); Assert.Null(Assert.Single(pages.Items).ParentPageId);
        Assert.Empty(Value<NotebookPageList>(await controller.Pages(notebook.Id, default, search: "hemmeligt")).Items);
        Assert.Equal(child.Id, Assert.Single(Value<NotebookPageList>(await controller.Pages(notebook.Id, default, search: "JOIN")).Items).Id);
        Assert.Empty(Value<NotebookPageSummary[]>(await controller.Backlinks(child.Id, default)));
        var graph = Value<NotebookGraph>(await controller.Graph(notebook.Id, default));
        Assert.Equal(1, graph.Total); Assert.Single(graph.Nodes); Assert.Empty(graph.Edges);
        Assert.IsType<NotFoundResult>(await controller.Shares(notebook.Id, default));
        Assert.IsType<NoContentResult>(await As(owner).RevokeShare(notebook.Id, share.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Context(child.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Pages(notebook.Id, default));
        Assert.Empty(Value<NotebookPageSummary[]>(await controller.Shared(default)));
    }

    [Fact]
    public async Task WholeNotebookGrantIncludesFuturePagesAndOverlapSurvivesOneRevocation()
    {
        var exact = Value<NotebookShareView>(await As(owner).GrantShare(notebook.Id, new(reader, child.Id), default));
        var whole = Value<NotebookShareView>(await As(owner).GrantShare(notebook.Id, new(reader), default));
        Assert.Equal(whole.Id, Value<NotebookShareView>(await As(owner).GrantShare(notebook.Id, new(reader), default)).Id);
        var created = Value<NotebookPageSummary>(await As(owner).Create(notebook.Id, new("Ny side"), default));
        Assert.True(Value<NotebookDocumentContext>(await As(reader).Context(created.Id, default)).CanRead);
        Assert.Equal(5, Value<NotebookPageSummary[]>(await As(reader).Shared(default)).Length);
        Assert.IsType<NoContentResult>(await As(owner).RevokeShare(notebook.Id, whole.Id, default));
        Assert.IsType<NotFoundResult>(await As(reader).Context(created.Id, default));
        Assert.IsType<OkObjectResult>(await As(reader).Context(child.Id, default));
        await As(owner).RevokeShare(notebook.Id, exact.Id, default);
        Assert.IsType<NotFoundResult>(await As(reader).Context(child.Id, default));
    }

    [Fact]
    public async Task OwnerCanEditTextMetadataButCannotCreateParentCyclesOrLeakForeignParents()
    {
        var controller = As(owner);
        var ownContext = Value<NotebookDocumentContext>(await controller.Context(child.Id, default));
        Assert.True(ownContext.CanEdit); Assert.True(ownContext.CanComment); Assert.True(ownContext.CanShare);
        Assert.IsType<OkObjectResult>(await controller.AuthorizeDocument(new("notebook:" + child.Id), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(parent.Id, new("Cyklus", descendant.Id, []), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(child.Id, new("Selv", child.Id, []), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(child.Id, new("Ukendt", Guid.NewGuid(), []), default));
        var updated = Value<NotebookPageSummary>(await controller.Update(child.Id, new("SQL og C#", null, ["SQL", "C#", "sql"]), default));
        Assert.Equal(["sql", "c#"], updated.Tags); Assert.Null(updated.ParentPageId); Assert.Equal(2, updated.Revision);
        var saved = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == child.Id);
        Assert.Equal(child.YjsState, saved.YjsState); Assert.Equal(child.Fingerprint, saved.Fingerprint);
        Assert.Single(Value<NotebookPageList>(await controller.Pages(notebook.Id, default, tag: "SQL")).Items);
        Assert.Empty(Value<NotebookPageList>(await controller.Pages(notebook.Id, default, tag: "s")).Items);
        Assert.IsType<BadRequestObjectResult>(await controller.Update(child.Id, new("Hold", null, [], Guid.NewGuid()), default));
    }

    [Fact]
    public async Task NamedRecipientsAndTeacherOverviewDoNotRequireAuthAdminDirectory()
    {
        var users = Value<NotebookUser[]>(await As(owner).Users(default, "bo"));
        Assert.Equal(reader, Assert.Single(users).Sub);
        Assert.DoesNotContain(Value<NotebookUser[]>(await As(owner).Users(default)), u => u.Sub == owner);
        Assert.IsType<BadRequestObjectResult>(await As(owner).GrantShare(notebook.Id, new(stranger), default));
        Assert.IsType<BadRequestObjectResult>(await As(owner).GrantShare(notebook.Id, new(reader, template.Id), default));
        Assert.IsType<NotFoundResult>(await As(stranger).Pages(notebook.Id, default));
        Assert.IsType<NotFoundResult>(await As(stranger).Graph(notebook.Id, default));
        Assert.IsType<ForbidResult>(await As(owner).Teacher(default));
        var overview = Value<TeacherNotebookView[]>(await As(teacher, "Teacher").Teacher(default));
        Assert.Equal(notebook.Id, Assert.Single(overview).Id);
        Assert.Equal("Anna", overview[0].OwnerName); Assert.Null(overview[0].LatestJournalDate);
    }

    [Fact]
    public async Task DatabaseUniquenessPreventsDuplicateNotebookAndPeriodicEntry()
    {
        db.Notebooks.Add(new() { Id = Guid.NewGuid(), OwnerSub = owner, CreatedAtUtc = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var templateId = Guid.NewGuid();
        var entry = Page("Mandag"); entry.Kind = "journal"; entry.TemplateId = templateId; entry.PeriodKey = "daily:2026-11-07";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var duplicate = Page("Samme dag, anden skabelonversion"); duplicate.Kind = "journal";
        duplicate.TemplateId = templateId; duplicate.TemplateVersionId = Guid.NewGuid(); duplicate.PeriodKey = entry.PeriodKey;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TemplateAndPersonalWriterPermissionsAlwaysFollowOwnerAndRealTeacherRole()
    {
        var access = new NotebookAccess(db);
        Assert.True(await access.CanEditAsync(child.Id, Principal(owner), default));
        Assert.False(await access.CanEditAsync(child.Id, Principal(teacher, "Teacher"), default));
        Assert.True(await access.CanEditAsync(template.Id, Principal(teacher, "Teacher"), default));
        Assert.False(await access.CanEditAsync(template.Id, Principal(owner), default));
        Assert.False(await access.CanReadAsync(template.Id, Principal(owner), default));
        Assert.IsType<BadRequestObjectResult>(await As(owner).AuthorizeDocument(new("notion:" + child.Id), default));
        Assert.IsType<BadRequestObjectResult>(await As(owner).AuthorizeDocument(new("notebook:" + Guid.Empty), default));
    }

    [Fact]
    public async Task RevisionPollOmitsUnchangedBinaryButRefreshesLinkPermissionsAndReauthorizes()
    {
        var controller = As(reader);
        var whole = Value<NotebookShareView>(await As(owner).GrantShare(notebook.Id, new(reader), default));
        var initial = Value<NotebookDocumentContext>(await controller.Context(child.Id, default));
        Assert.NotNull(initial.StateBase64);
        Assert.Contains(initial.Pages, p => p.Id == hidden.Id);
        var exact = Value<NotebookShareView>(await As(owner).GrantShare(notebook.Id, new(reader, child.Id), default));
        await As(owner).RevokeShare(notebook.Id, whole.Id, default);
        var unchanged = Value<NotebookDocumentContext>(await controller.Context(child.Id, default, initial.Revision));
        Assert.Null(unchanged.StateBase64);
        Assert.Equal(initial.Revision, unchanged.Revision);
        Assert.Equal(child.Id, Assert.Single(unchanged.Pages).Id);
        Assert.Null(unchanged.Pages[0].ParentPageId);

        await db.NotebookDocuments.Where(d => d.Id == child.Id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.YjsState, new byte[] { 3, 4, 5 }).SetProperty(d => d.Revision, d => d.Revision + 1));
        var changed = Value<NotebookDocumentContext>(await controller.Context(child.Id, default, initial.Revision));
        Assert.Equal(initial.Revision + 1, changed.Revision);
        Assert.Equal(Convert.ToBase64String([3, 4, 5]), changed.StateBase64);

        await As(owner).RevokeShare(notebook.Id, exact.Id, default);
        Assert.IsType<NotFoundResult>(await controller.Context(child.Id, default, changed.Revision));
    }
}
