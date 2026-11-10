using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotionCollectionsTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), otherTeacher = Guid.NewGuid(), reader = Guid.NewGuid();
    private readonly MemoryStorage storage = new();
    private readonly EphemeralDataProtectionProvider protection = new();
    private static readonly string Fingerprint = new('a', 64);

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private static ClaimsPrincipal User(Guid sub, bool teacher = false) => new(new ClaimsIdentity(teacher
        ? [new Claim("sub", sub.ToString()), new Claim(ClaimTypes.Role, "Underviser")]
        : [new Claim("sub", sub.ToString())], "test"));
    private static ControllerContext Context(ClaimsPrincipal user) => new() { HttpContext = new DefaultHttpContext { User = user } };
    private NotionCollectionsController Collections(Guid sub, bool isTeacher = false) => new(db, new(db)) { ControllerContext = Context(User(sub, isTeacher)) };
    private NotionController Pages(Guid sub, bool isTeacher = false) => new(db, new(db)) { ControllerContext = Context(User(sub, isTeacher)) };
    private NotionAssetsController Assets(Guid sub, bool isTeacher = false) => new(db, new(db), storage, protection) { ControllerContext = Context(User(sub, isTeacher)) };
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<NotionCollectionView> Create() => Value<NotionCollectionView>(await Collections(teacher, true).Create(new("SQL-teori"), default));
    private async Task Publish(Guid id, Guid[]? assets = null)
    {
        await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.YjsState, GraphFixtures.Get("empty"))
            .SetProperty(p => p.DraftFingerprint, Fingerprint).SetProperty(p => p.DraftRevision, 1), default);
        Assert.IsType<OkObjectResult>(await Pages(teacher, true).Publish(id, new(1, Fingerprint, assets ?? []), default));
    }

    [Fact]
    public async Task Theory_has_no_hold_all_teachers_edit_and_only_published_content_is_readable()
    {
        Assert.IsType<NotFoundResult>(await Collections(reader).Create(new("Ulovlig teori"), default));
        var collection = await Create();
        var anonymous = Collections(reader); anonymous.HttpContext.User = new();
        Assert.IsType<UnauthorizedResult>(await anonymous.List(default));
        Assert.Equal(collection.Id, collection.RootPageId); Assert.Null(collection.HoldId);
        Assert.True(collection.CanManage);
        var context = Value<NotionPageContext>(await Pages(otherTeacher, true).Context(collection.RootPageId!.Value, default));
        Assert.True(context.CanEdit); Assert.Equal(collection.Id, context.CollectionId); Assert.Empty(context.Members);
        Assert.IsType<OkObjectResult>(await Pages(otherTeacher, true).AuthorizeDocument(new("notion:" + collection.Id), default));
        Assert.IsType<NotFoundResult>(await Pages(reader).Context(collection.Id, default));
        Assert.IsType<NotFoundResult>(await Pages(reader).AuthorizeDocument(new("notion:" + collection.Id), default));
        Assert.Empty(Value<NotionCollectionView[]>(await Collections(reader).List(default)));
        await Publish(collection.Id);
        var published = Value<NotionPageContext>(await Pages(reader).Context(collection.Id, default));
        Assert.False(published.CanEdit); Assert.Equal("SQL-teori", published.Title); Assert.NotNull(published.PublishedStateBase64);
        Assert.Equal(collection.Id, Assert.Single(Value<NotionCollectionView[]>(await Collections(reader).List(default))).Id);
        await Pages(otherTeacher, true).Rename(collection.Id, new("Hemmelig kladdetitel"), default);
        Assert.Equal("SQL-teori", Assert.Single(Value<NotionCollectionView[]>(await Collections(reader).List(default))).Title);
        Assert.Equal("Hemmelig kladdetitel", Assert.Single(Value<NotionCollectionView[]>(await Collections(otherTeacher, true).List(default))).Title);
        Assert.IsType<OkObjectResult>(await Pages(otherTeacher, true).Unpublish(collection.Id, default));
        Assert.IsType<NotFoundResult>(await Pages(reader).Context(collection.Id, default));
        Assert.Empty(Value<NotionCollectionView[]>(await Collections(reader).List(default)));
    }

    [Fact]
    public async Task Cards_only_show_the_editor_and_timestamp_of_the_readable_version()
    {
        var creator = Collections(teacher, true);
        ((ClaimsIdentity)creator.User.Identity!).AddClaim(new Claim("name", "Anna"));
        var collection = Value<NotionCollectionView>(await creator.Create(new("SQL-teori"), default));
        await Publish(collection.Id);
        var readerCard = Assert.Single(Value<NotionCollectionView[]>(await Collections(reader).List(default)));
        Assert.Equal("Anna", readerCard.LastEditedByName);
        Assert.Equal(collection.UpdatedAtUtc, readerCard.UpdatedAtUtc);

        var editor = Pages(otherTeacher, true);
        ((ClaimsIdentity)editor.User.Identity!).AddClaim(new Claim("name", "Berit"));
        Assert.IsType<OkObjectResult>(await editor.Rename(collection.Id, new("Ny titel"), default));

        var draftCard = Assert.Single(Value<NotionCollectionView[]>(await Collections(otherTeacher, true).List(default)));
        Assert.Equal("Berit", draftCard.LastEditedByName);
        Assert.True(draftCard.UpdatedAtUtc >= readerCard.UpdatedAtUtc);
        Assert.Equal(readerCard, Assert.Single(Value<NotionCollectionView[]>(await Collections(reader).List(default))));
        Assert.Equal(readerCard, Assert.Single(Value<NotionCollectionView[]>(await Collections(otherTeacher, true).List(default, publishedOnly: true))));
    }

    [Fact]
    public async Task Collection_icon_can_be_changed_only_by_a_manager_and_is_visible_to_readers()
    {
        var collection = await Create();
        var icon = CharacterEmojiCatalog.All.First().Id;
        Assert.IsType<NotFoundResult>(await Collections(reader).SetIcon(collection.Id, new(icon), default));
        Assert.IsType<BadRequestObjectResult>(await Collections(teacher, true).SetIcon(collection.Id, new(Guid.NewGuid()), default));
        Assert.IsType<NoContentResult>(await Collections(teacher, true).SetIcon(collection.Id, new(icon), default));
        await Publish(collection.Id);
        Assert.Equal(icon, Assert.Single(Value<NotionCollectionView[]>(await Collections(reader).List(default))).IconEmojiId);
        Assert.IsType<NoContentResult>(await Collections(otherTeacher, true).SetIcon(collection.Id, new(null), default));
        Assert.Null(Assert.Single(Value<NotionCollectionView[]>(await Collections(reader).List(default))).IconEmojiId);
    }

    [Fact]
    public async Task Collection_pages_derive_scope_and_unpublished_roots_do_not_hide_readable_descendants()
    {
        var first = await Create(); var second = await Create();
        var child = Value<NotionPageSummary>(await Pages(otherTeacher, true).Create(new(null, "JOIN", CollectionId: first.Id), default));
        Assert.Equal(first.Id, child.ParentPageId); Assert.Equal(first.Id, child.CollectionId); Assert.Null(child.HoldId);
        Assert.IsType<BadRequestObjectResult>(await Pages(teacher, true).Create(new(null, "Forkert", child.Id, second.Id), default));
        await Publish(child.Id);
        var collection = Assert.Single(Value<NotionCollectionView[]>(await Collections(reader).List(default)));
        Assert.Equal(first.Id, collection.Id); Assert.Equal(child.Id, collection.RootPageId); Assert.Equal("Samling", collection.Title);
        var list = Value<List<NotionPageSummary>>(await Pages(reader).List(null, default, collectionId: first.Id));
        Assert.Equal(child.Id, Assert.Single(list).Id); Assert.Null(list[0].ParentPageId);
        var context = Value<NotionPageContext>(await Pages(reader).Context(child.Id, default));
        Assert.Equal(child.Id, Assert.Single(context.Pages!).Id);
    }

    [Fact]
    public async Task Teaching_tree_supports_multiple_roots_and_restores_a_deleted_landing_page_with_its_branch()
    {
        var collection = await Create();
        var other = await Create();
        var editor = Pages(teacher, true);
        var child = Value<NotionPageSummary>(await editor.Create(new(null, "SQL JOIN", collection.Id, collection.Id), default));
        var top = Value<NotionPageSummary>(await editor.Create(new(null, "Indekser", CollectionId: collection.Id, AtRoot: true), default));
        Assert.Null(top.ParentPageId);
        Assert.IsType<BadRequestObjectResult>(await editor.Move(collection.Id, new(child.Id), default));
        Assert.IsType<BadRequestObjectResult>(await editor.Move(child.Id, new(other.Id), default));
        Assert.IsType<NoContentResult>(await editor.Move(top.Id, new(null, BeforePageId: collection.Id), default));
        Assert.Equal(0, (await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == top.Id)).SortOrder);

        await Publish(collection.Id);
        await Publish(child.Id);
        await Publish(top.Id);
        Assert.Equal(1, Value<NotebookPageDeletionPreview>(await editor.DeletePreview(collection.Id, default)).DescendantCount);
        Assert.IsType<ConflictObjectResult>(await editor.TrashPage(collection.Id, default, 0));
        Assert.IsType<NotFoundResult>(await Pages(reader).TrashPage(collection.Id, default));
        Assert.IsType<NoContentResult>(await editor.TrashPage(collection.Id, default, 1));
        Assert.IsType<NotFoundResult>(await Pages(reader).Context(child.Id, default));
        Assert.IsType<NotFoundResult>(await editor.AuthorizeDocument(new("notion:" + child.Id), default));
        Assert.Equal(top.Id, Value<NotionCollectionView>(await Collections(reader).Get(collection.Id, default)).RootPageId);
        Assert.Equal(top.Id, Assert.Single(Value<NotionGraph>(await Pages(reader).Graph(collection.Id, default)).Nodes).Id);
        Assert.Equal(1, Assert.Single(Value<TrashedNotebookPage[]>(await editor.CollectionTrash(collection.Id, default))).DescendantCount);
        Assert.IsType<NoContentResult>(await editor.RestorePage(collection.Id, default));
        Assert.Equal(collection.Id, (await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == child.Id)).ParentPageId);
        Assert.NotNull(Value<NotionPageContext>(await Pages(reader).Context(child.Id, default)).PublishedStateBase64);
        Assert.Equal(1, (await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == child.Id)).LifecycleVersion);
    }

    [Fact]
    public async Task Standalone_media_and_drawing_parent_use_publication_and_revoke_old_reader_tickets()
    {
        var collection = await Create();
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
        var file = new FormFile(new MemoryStream(png), 0, png.Length, "file", "diagram.png") { Headers = new HeaderDictionary() };
        Assert.IsType<NotFoundResult>(await Assets(reader).Upload(collection.Id, file, default));
        var asset = Value<NotionAssetView>(await Assets(otherTeacher, true).Upload(collection.Id, file, default));
        Assert.IsType<NotFoundResult>(await Assets(reader).Access(asset.Id, default));
        var drawingAccess = new DrawingAccess(db, new(db), new(db));
        var drawing = new Drawing { NotionPageId = collection.Id };
        Assert.False(await drawingAccess.CanReadParentAsync(drawing, User(reader), default));
        Assert.True(await drawingAccess.CanManageAsync(drawing, User(otherTeacher, true), default));
        await Publish(collection.Id, [asset.Id]);
        Assert.True(await drawingAccess.CanReadParentAsync(drawing, User(reader), default));
        var grant = Value<NotionAssetAccess>(await Assets(reader).Access(asset.Id, default));
        var ticket = Uri.UnescapeDataString(grant.Url.Split("ticket=")[1]);
        var anonymous = Assets(reader); anonymous.HttpContext.User = new();
        Assert.IsType<FileStreamResult>(await anonymous.Content(asset.Id, default, ticket));
        await Pages(teacher, true).Unpublish(collection.Id, default);
        Assert.IsType<NotFoundResult>(await anonymous.Content(asset.Id, default, ticket));
        Assert.False(await drawingAccess.CanReadParentAsync(drawing, User(reader), default));
    }

    private sealed class MemoryStorage : IObjectStorage
    {
        public bool IsConfigured => true;
        public string Bucket => "test";
        private readonly Dictionary<string, byte[]> objects = [];
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        { using var copy = new MemoryStream(); await content.CopyToAsync(copy, cancellationToken); objects[key] = copy.ToArray(); return new(key, Bucket, contentType, copy.Length); }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<ObjectGetResult?>(objects.TryGetValue(key, out var bytes) ? new(new MemoryStream(bytes), "image/png", bytes.Length) : null);
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }
}
