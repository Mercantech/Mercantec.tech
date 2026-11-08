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
        var context = Value<NotionPageContext>(await Pages(otherTeacher, true).Context(collection.RootPageId, default));
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
