using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Mercantec.Tech.Api.Tests;

public sealed class DrawingTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private DrawingService _service = null!;
    private DrawingAccess _access = null!;
    private readonly MemoryStorage _storage = new();
    private readonly Engine _engine = new();
    private readonly Guid _owner = Guid.NewGuid(), _reader = Guid.NewGuid(), _teacher = Guid.NewGuid(), _outsider = Guid.NewGuid();
    private readonly Guid _notebook = Guid.NewGuid(), _note = Guid.NewGuid(), _page = Guid.NewGuid(), _hold = Guid.NewGuid();
    private const string Secret = "drawing-tests-only-service-secret-at-least32";
    private static readonly IConfiguration Config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Notion:ServiceSecret"] = Secret, ["Notebook:CollaborationBaseUrl"] = "http://drawing-engine.test" }).Build();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO/jYaoAAAAASUVORK5CYII=");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync(); _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _db.Hold.Add(new() { Id = _hold, Name = "Testhold", Forloeb = new() { Id = Guid.NewGuid(), Name = "Forløb", CreatedBySub = _teacher } });
        _db.Memberships.AddRange(new Membership { Id = Guid.NewGuid(), HoldId = _hold, UserSub = _owner, DisplayName = "Ejer" },
            new Membership { Id = Guid.NewGuid(), HoldId = _hold, UserSub = _reader, DisplayName = "Læser" },
            new Membership { Id = Guid.NewGuid(), HoldId = _hold, UserSub = _outsider, DisplayName = "Uden notesideadgang" });
        _db.Notebooks.Add(new() { Id = _notebook, OwnerSub = _owner });
        _db.NotebookDocuments.Add(new() { Id = _note, NotebookId = _notebook, Title = "Privat note" });
        _db.NotebookShares.Add(new() { Id = Guid.NewGuid(), NotebookId = _notebook, PageId = _note, RecipientSub = _reader });
        _db.NotionPages.Add(new() { Id = _page, HoldId = _hold, Title = "Undervisning", PublishedTitle = "Undervisning", PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedState = [0, 0], YjsState = [0, 0], DraftFingerprint = new('a', 64), DraftRevision = 1 });
        await _db.SaveChangesAsync(); _db.ChangeTracker.Clear();
        _access = new(_db, new(_db), new(_db));
        _service = new(_db, _access, _storage, _engine, Config, new(Config), new EphemeralDataProtectionProvider());
    }
    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); _engine.Dispose(); }
    private ClaimsPrincipal User(Guid sub, bool teacher = false) => DrawingAccess.CandidatePrincipal(sub, teacher);
    private NotionController Notion(Guid sub, bool teacher = false) => new(_db, new(_db), _service)
    { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = User(sub, teacher) } } };
    private static FormFile File(byte[]? bytes = null, string name = "image.png") => new(new MemoryStream(bytes ?? Png), 0, (bytes ?? Png).Length, "file", name);
    private static StoreDrawingDocument Projection(char fingerprint = 'a', string? scene = null, Guid[]? assets = null)
        => new(Convert.ToBase64String([0, 0]), new(fingerprint, 64), scene ?? "{\"elements\":[{\"id\":\"one\",\"type\":\"text\",\"text\":\"Original\",\"isDeleted\":false}],\"files\":{},\"appState\":{\"viewBackgroundColor\":\"#ffffff\"}}", assets ?? []);
    private async Task<DrawingContext> Create(bool notion = false)
    {
        var result = await _service.CreateAsync(new(notion ? "notion" : "notebook", notion ? _page : _note, "block-" + Guid.NewGuid()), User(notion ? _teacher : _owner, notion), default);
        var refs = JsonSerializer.Serialize(new[] { result.Id });
        if (notion) await _db.NotionPages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.DrawingIdsJson, refs));
        else await _db.NotebookDocuments.Where(p => p.Id == _note).ExecuteUpdateAsync(s => s.SetProperty(p => p.DrawingIdsJson, refs));
        return result;
    }
    private async Task Preview(Guid id, ClaimsPrincipal user)
    {
        var drawing = (await _service.FindAsync(id, default))!;
        await _service.UploadPreviewsAsync(id, drawing.Revision, drawing.Fingerprint!, File(), File(), user, default);
    }
    private async Task<DrawingPublicationBinding> Freeze(Guid id, bool notion = false)
    {
        var drawing = (await _service.FindAsync(id, default))!;
        await using var tx = await _db.Database.BeginTransactionAsync();
        var result = await _service.PreparePublicationAsync(notion ? "notion" : "notebook", notion ? _page : _note, [id],
            [new(id, "snapshot", drawing.Revision, drawing.Fingerprint!)], default);
        await tx.CommitAsync(); return Assert.Single(result);
    }

    [Fact]
    public async Task Creation_is_idempotent_and_drawing_grants_never_grant_page_writing_or_parent_reading()
    {
        var drawing = await Create();
        var same = await _service.CreateAsync(new("notebook", _note, drawing.BlockId), User(_owner), default);
        Assert.Equal(drawing.Id, same.Id); Assert.Single(await _db.Drawings.ToArrayAsync());
        Assert.False(await _access.CanEditAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        await _service.GrantAsync(drawing.Id, new(_reader, null), User(_owner), default);
        Assert.True(await _access.CanEditAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        Assert.False(await new NotebookAccess(_db).CanEditAsync(_note, User(_reader), default));
        var denied = await Assert.ThrowsAsync<DrawingException>(() => _service.GrantAsync(drawing.Id, new(_outsider, null), User(_owner), default));
        Assert.Equal("drawing_recipient_must_read_parent", denied.Code);
        await _db.NotebookShares.ExecuteDeleteAsync();
        Assert.False(await _access.CanEditAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        await Assert.ThrowsAsync<DrawingException>(() => _service.ContextAsync(drawing.Id, User(_reader), false, null, default));
    }

    [Fact]
    public async Task Hold_grants_are_intersected_with_current_parent_readers()
    {
        var drawing = await Create();
        await _service.GrantAsync(drawing.Id, new(null, _hold), User(_owner), default);
        var stored = (await _service.FindAsync(drawing.Id, default))!;
        Assert.True(await _access.CanEditAsync(stored, User(_reader), default));
        Assert.False(await _access.CanEditAsync(stored, User(_outsider), default));
        await _db.Memberships.Where(m => m.UserSub == _reader).ExecuteDeleteAsync();
        Assert.False(await _access.CanEditAsync(stored, User(_reader), default));
    }

    [Fact]
    public async Task Drawing_saves_increment_parent_revision_once_and_reject_foreign_assets_without_mutation()
    {
        var drawing = await Create();
        await _service.StoreAsync(drawing.Id, Projection(), default);
        await _service.StoreAsync(drawing.Id, Projection(), default);
        Assert.Equal(1, (await _db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == _note)).Revision);
        var foreign = Guid.NewGuid();
        var scene = JsonSerializer.Serialize(new { elements = new[] { new { id = "picture", fileId = "file" } },
            files = new Dictionary<string, object> { ["file"] = new { assetId = foreign } }, appState = new { viewBackgroundColor = "#fff" } });
        await Assert.ThrowsAsync<DrawingException>(() => _service.StoreAsync(drawing.Id, Projection('b', scene, [foreign]), default));
        Assert.Equal(new('a', 64), (await _service.FindAsync(drawing.Id, default))!.Fingerprint);
        Assert.Equal(1, (await _db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == _note)).Revision);
        await _service.StoreAsync(drawing.Id, Projection('c'), default);
        Assert.Equal(2, (await _db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == _note)).Revision);
    }

    [Fact]
    public async Task Previews_require_matching_fingerprint_revision_and_bounded_png_dimensions()
    {
        var drawing = await Create(); await _service.StoreAsync(drawing.Id, Projection(), default);
        await Assert.ThrowsAsync<DrawingException>(() => _service.UploadPreviewsAsync(drawing.Id, 0, new('a', 64), File(), File(), User(_owner), default));
        var oversized = Png.ToArray(); oversized[16] = 0; oversized[17] = 0; oversized[18] = 8; oversized[19] = 1;
        await Assert.ThrowsAsync<DrawingException>(() => _service.UploadPreviewsAsync(drawing.Id, 1, new('a', 64), File(oversized), File(), User(_owner), default));
        Assert.Empty(_storage.Objects);
        await Preview(drawing.Id, User(_owner)); Assert.Equal(2, _storage.Objects.Count);
        var head = (await _service.FindAsync(drawing.Id, default))!;
        Assert.Equal(head.Fingerprint, head.PreviewFingerprint); Assert.Equal(head.Revision, head.PreviewRevision);
    }

    [Fact]
    public async Task Published_snapshots_hide_tombstones_and_remain_frozen_while_drafts_change()
    {
        var drawing = await Create(true);
        var scene = "{\"elements\":[{\"id\":\"visible\",\"text\":\"Public\",\"isDeleted\":false},{\"id\":\"deleted\",\"text\":\"Private deleted text\",\"isDeleted\":true}],\"files\":{},\"appState\":{\"viewBackgroundColor\":\"#fff\"}}";
        await _service.StoreAsync(drawing.Id, Projection('a', scene), default); await Preview(drawing.Id, User(_teacher, true));
        var binding = await Freeze(drawing.Id, true);
        await _db.NotionPages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedDrawingsJson, JsonSerializer.Serialize(new[] { binding }, Json)));
        var published = await _service.ContextAsync(drawing.Id, User(_reader), true, binding.SnapshotId, default);
        Assert.Null(published.StateBase64); Assert.Contains("Public", published.SceneJson); Assert.DoesNotContain("Private deleted text", published.SceneJson);
        Assert.False(await _access.CanReadHeadAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        await _service.StoreAsync(drawing.Id, Projection('b'), default);
        var again = await _service.ContextAsync(drawing.Id, User(_reader), true, binding.SnapshotId, default);
        Assert.Equal(published.SceneJson, again.SceneJson); Assert.Equal(published.Fingerprint, again.Fingerprint);
        var frozenAsset = (await _db.DrawingSnapshots.SingleAsync(s => s.Id == binding.SnapshotId)).PreviewLightAssetId;
        var frozenTicket = Uri.UnescapeDataString(published.PreviewLightUrl!.Split("ticket=")[1]);
        var frozenViewer = _service.TicketViewer(drawing.Id, frozenAsset, frozenTicket, new ClaimsPrincipal())!;
        var (_, frozenContent) = await _service.ReadAssetAsync(drawing.Id, frozenAsset, frozenViewer, default);
        await frozenContent.DisposeAsync();
        await Assert.ThrowsAsync<DrawingException>(() => _service.ContextAsync(drawing.Id, User(_reader), true, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Stale_head_previews_disappear_and_old_reader_tickets_stop_working_after_scene_changes()
    {
        var drawing = await Create(); await _service.StoreAsync(drawing.Id, Projection(), default); await Preview(drawing.Id, User(_owner));
        var before = await _service.ContextAsync(drawing.Id, User(_reader), false, null, default);
        var asset = (await _service.FindAsync(drawing.Id, default))!.PreviewLightAssetId!.Value;
        var ticket = Uri.UnescapeDataString(before.PreviewLightUrl!.Split("ticket=")[1]);
        var viewer = _service.TicketViewer(drawing.Id, asset, ticket, new ClaimsPrincipal())!;
        var (_, original) = await _service.ReadAssetAsync(drawing.Id, asset, viewer, default); await original.DisposeAsync();
        await _service.StoreAsync(drawing.Id, Projection('b'), default);
        var changed = await _service.ContextAsync(drawing.Id, User(_reader), false, null, default);
        Assert.Null(changed.PreviewLightUrl); Assert.Null(changed.PreviewDarkUrl);
        await Assert.ThrowsAsync<DrawingException>(() => _service.ReadAssetAsync(drawing.Id, asset, viewer, default));
    }

    [Fact]
    public async Task Notion_publication_commits_a_frozen_binding_and_a_stale_drawing_conflict_preserves_the_old_publication()
    {
        var drawing = await Create(true); await _service.StoreAsync(drawing.Id, Projection(), default); await Preview(drawing.Id, User(_teacher, true));
        var draft = await _db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == _page);
        var choice = new DrawingPublicationRequest(drawing.Id, "snapshot", 1, new('a', 64));
        Assert.IsType<OkObjectResult>(await Notion(_teacher, true).Publish(_page, new(draft.DraftRevision, draft.DraftFingerprint!, [], [choice]), default));
        var published = await _db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == _page);
        var context = Assert.IsType<NotionPageContext>(Assert.IsType<OkObjectResult>(await Notion(_reader).PublishedContext(_page, default)).Value);
        var binding = Assert.Single(context.Drawings!);
        Assert.Equal("snapshot", binding.Mode); Assert.NotNull(binding.SnapshotId);
        var publishedDrawing = await _service.ContextAsync(drawing.Id, User(_reader), true, binding.SnapshotId, default);
        Assert.Null(publishedDrawing.StateBase64); Assert.Contains("Original", publishedDrawing.SceneJson);

        await _service.StoreAsync(drawing.Id, Projection('b', Projection().SceneJson.Replace("Original", "Private new draft")), default);
        draft = await _db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == _page);
        var conflict = await Notion(_teacher, true).Publish(_page, new(draft.DraftRevision, draft.DraftFingerprint!, [], [choice]), default);
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(conflict).StatusCode);
        var after = await _db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == _page);
        Assert.Equal(published.PublicationVersion, after.PublicationVersion);
        Assert.Equal(published.PublishedDrawingsJson, after.PublishedDrawingsJson);
        Assert.Equal(published.PublishedRevision, after.PublishedRevision);
        Assert.Single(await _db.DrawingSnapshots.ToArrayAsync());
        var stillPublished = await _service.ContextAsync(drawing.Id, User(_reader), true, binding.SnapshotId, default);
        Assert.Equal(publishedDrawing.SceneJson, stillPublished.SceneJson);
        Assert.DoesNotContain("Private new draft", stillPublished.SceneJson);
    }

    [Fact]
    public async Task Notion_publication_rejects_omitted_or_foreign_drawing_manifests_and_unconfirmed_live_mode()
    {
        var drawing = await Create(true); await _service.StoreAsync(drawing.Id, Projection(), default); await Preview(drawing.Id, User(_teacher, true));
        var before = await _db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == _page);
        var forgedLive = new DrawingPublicationRequest(drawing.Id, "live", 1, new('a', 64));
        var live = await Notion(_teacher, true).Publish(_page, new(before.DraftRevision, before.DraftFingerprint!, [], [forgedLive]), default);
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(live).StatusCode);
        var omitted = await Notion(_teacher, true).Publish(_page, new(before.DraftRevision, before.DraftFingerprint!, []), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(omitted).StatusCode);
        var foreign = new DrawingPublicationRequest(Guid.NewGuid(), "snapshot", 1, new('a', 64));
        var other = await Notion(_teacher, true).Publish(_page, new(before.DraftRevision, before.DraftFingerprint!, [], [foreign]), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(other).StatusCode);
        var after = await _db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == _page);
        Assert.Equal(before.PublicationVersion, after.PublicationVersion);
        Assert.Equal(before.PublishedDrawingsJson, after.PublishedDrawingsJson);
        Assert.Empty(await _db.DrawingSnapshots.ToArrayAsync());
        Assert.False(await _access.CanReadHeadAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
    }

    [Fact]
    public async Task Live_access_requires_an_active_live_publication_and_withdrawal_revokes_old_media_tickets()
    {
        var drawing = await Create(true); await _service.StoreAsync(drawing.Id, Projection(), default); await Preview(drawing.Id, User(_teacher, true));
        await _service.SettingsAsync(drawing.Id, new("live", 1), User(_teacher, true), default);
        Assert.False(await _access.CanReadHeadAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        var binding = new DrawingPublicationBinding(drawing.Id, drawing.BlockId, "live", null, 1, new('a', 64));
        await _db.NotionPages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedDrawingsJson, JsonSerializer.Serialize(new[] { binding }, Json)));
        var published = await _service.ContextAsync(drawing.Id, User(_reader), true, null, default);
        Assert.False(published.CanEditDrawing); Assert.NotNull(published.StateBase64);
        var assetId = (await _service.FindAsync(drawing.Id, default))!.PreviewLightAssetId!.Value;
        var ticket = Uri.UnescapeDataString(published.PreviewLightUrl!.Split("ticket=")[1]);
        var viewer = _service.TicketViewer(drawing.Id, assetId, ticket, new ClaimsPrincipal())!;
        var (_, content) = await _service.ReadAssetAsync(drawing.Id, assetId, viewer, default); await content.DisposeAsync();
        await _db.NotionPages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedState, (byte[]?)null)
            .SetProperty(p => p.PublishedAtUtc, (DateTimeOffset?)null).SetProperty(p => p.PublishedDrawingsJson, "[]"));
        Assert.False(await _access.CanReadHeadAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        await Assert.ThrowsAsync<DrawingException>(() => _service.ReadAssetAsync(drawing.Id, assetId, viewer, default));
    }

    [Fact]
    public async Task Snapshot_publication_does_not_activate_a_delegated_grant_until_explicit_live_publication()
    {
        var drawing = await Create(true); await _service.StoreAsync(drawing.Id, Projection(), default); await Preview(drawing.Id, User(_teacher, true));
        var binding = await Freeze(drawing.Id, true);
        await _db.NotionPages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedDrawingsJson, JsonSerializer.Serialize(new[] { binding }, Json)));
        await _service.GrantAsync(drawing.Id, new(_reader, null), User(_teacher, true), default);
        var head = (await _service.FindAsync(drawing.Id, default))!;
        Assert.False(await _access.CanEditAsync(head, User(_reader), default));
        Assert.False(await _access.CanReadHeadAsync(head, User(_reader), default));
        Assert.False((await _service.ContextAsync(drawing.Id, User(_reader), true, binding.SnapshotId, default)).CanEditDrawing);
        await Assert.ThrowsAsync<DrawingException>(() => _service.ContextAsync(drawing.Id, User(_reader), false, null, default));

        await _service.SettingsAsync(drawing.Id, new("live", head.AccessVersion), User(_teacher, true), default);
        Assert.False(await _access.CanEditAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        var live = binding with { Mode = "live", SnapshotId = null };
        await _db.NotionPages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedDrawingsJson, JsonSerializer.Serialize(new[] { live }, Json)));
        Assert.True(await _access.CanEditAsync((await _service.FindAsync(drawing.Id, default))!, User(_reader), default));
        Assert.False(await new CourseAccess(_db).CanManageAsync(_hold, User(_reader), default));
    }

    [Fact]
    public async Task A_draft_only_drawing_on_a_published_parent_is_not_exposed_by_a_drawing_grant()
    {
        var drawing = await Create(true); await _service.StoreAsync(drawing.Id, Projection(), default);
        await _service.GrantAsync(drawing.Id, new(_reader, null), User(_teacher, true), default);
        var head = (await _service.FindAsync(drawing.Id, default))!;
        Assert.True(await _access.CanReadParentAsync(head, User(_reader), default));
        Assert.True(await _access.IsReferencedAsync(head, default));
        Assert.False(await _access.CanEditAsync(head, User(_reader), default));
        Assert.False(await _access.CanReadHeadAsync(head, User(_reader), default));
        await Assert.ThrowsAsync<DrawingException>(() => _service.ContextAsync(drawing.Id, User(_reader), false, null, default));
        await Assert.ThrowsAsync<DrawingException>(() => _service.ContextAsync(drawing.Id, User(_reader), true, null, default));
        var controller = new DrawingsController(_service, _access)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = User(_reader) } } };
        Assert.IsType<NotFoundResult>(await controller.AuthorizeDocument(new("drawing:" + drawing.Id), default));
        Assert.True((await _service.ContextAsync(drawing.Id, User(_teacher, true), false, null, default)).CanEditDrawing);
    }

    [Fact]
    public async Task Template_clones_use_new_drawings_and_asset_grants_without_copying_collaborators_or_live_mode()
    {
        var drawing = await Create();
        var image = await _service.UploadAssetAsync(drawing.Id, File(), User(_owner), default);
        var scene = JsonSerializer.Serialize(new { elements = new[] { new { id = "image", type = "image", fileId = "file", isDeleted = false } },
            files = new Dictionary<string, object> { ["file"] = new { assetId = image.Id } }, appState = new { viewBackgroundColor = "#fff" } });
        await _service.StoreAsync(drawing.Id, Projection('a', scene, [image.Id]), default); await Preview(drawing.Id, User(_owner));
        await _service.GrantAsync(drawing.Id, new(_reader, null), User(_owner), default);
        var frozen = await Freeze(drawing.Id);
        var target = Guid.NewGuid();
        var result = await _service.CloneTemplateAsync(_note, target, [frozen], default);
        var cloneId = Guid.Parse(result.DrawingMap[drawing.Id.ToString()]);
        _db.NotebookDocuments.Add(new() { Id = target, NotebookId = _notebook, Title = "Uafhængig kopi", DrawingIdsJson = JsonSerializer.Serialize(new[] { cloneId }) });
        _service.BindClonedBlocks(new Dictionary<string, string> { [cloneId.ToString()] = "new-block" });
        await _db.SaveChangesAsync(); _db.ChangeTracker.Clear();
        var clone = await _db.Drawings.SingleAsync(d => d.Id == cloneId);
        Assert.Equal("new-block", clone.BlockId); Assert.Equal("snapshot", clone.Mode);
        Assert.Empty(await _db.DrawingGrants.Where(g => g.DrawingId == cloneId).ToArrayAsync());
        var cloneImage = DrawingAccess.Ids(clone.AssetIdsJson).Single(); Assert.NotEqual(image.Id, cloneImage);
        Assert.Equal((await _db.DrawingAssets.SingleAsync(a => a.Id == image.Id)).Key, (await _db.DrawingAssets.SingleAsync(a => a.Id == cloneImage)).Key);
        Assert.Equal(3, _storage.Objects.Count);
        Assert.False(await _access.CanReadParentAsync(clone, User(_reader), default));
    }

    [Fact]
    public async Task Copying_a_published_snapshot_uses_only_that_snapshot_when_the_pupil_cannot_read_the_newer_head()
    {
        var source = await Create(true);
        var publicImage = await _service.UploadAssetAsync(source.Id, File(), User(_teacher, true), default);
        var publicScene = JsonSerializer.Serialize(new
        {
            elements = new object[] { new { id = "text", type = "text", text = "Public frozen drawing", isDeleted = false },
                new { id = "picture", type = "image", fileId = "public-file", isDeleted = false } },
            files = new Dictionary<string, object> { ["public-file"] = new { assetId = publicImage.Id } },
            appState = new { viewBackgroundColor = "#fff" }
        });
        await _service.StoreAsync(source.Id, Projection('a', publicScene, [publicImage.Id]), default);
        await Preview(source.Id, User(_teacher, true));
        var frozen = await Freeze(source.Id, true);
        await _db.NotionPages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedDrawingsJson, JsonSerializer.Serialize(new[] { frozen }, Json)));
        await _service.GrantAsync(source.Id, new(_owner, null), User(_teacher, true), default);
        var head = (await _service.FindAsync(source.Id, default))!;
        await _service.SettingsAsync(source.Id, new("live", head.AccessVersion), User(_teacher, true), default);
        var privateImage = await _service.UploadAssetAsync(source.Id, File(), User(_teacher, true), default);
        var privateScene = JsonSerializer.Serialize(new
        {
            elements = new object[] { new { id = "text", type = "text", text = "Private new draft", isDeleted = false },
                new { id = "private-picture", type = "image", fileId = "private-file", isDeleted = false } },
            files = new Dictionary<string, object> { ["private-file"] = new { assetId = privateImage.Id } },
            appState = new { viewBackgroundColor = "#fff" }
        });
        await _service.StoreAsync(source.Id, Projection('b', privateScene, [privateImage.Id]), default);
        Assert.False(await _access.CanReadHeadAsync((await _service.FindAsync(source.Id, default))!, User(_owner), default));

        var copy = await _service.CreateAsync(new("notebook", _note, "copied-published-drawing", source.Id), User(_owner), default);
        Assert.NotEqual(source.Id, copy.Id); Assert.Equal("snapshot", copy.Mode);
        Assert.True(copy.CanEditDrawing); Assert.True(copy.CanManageDrawing);
        Assert.Contains("Public frozen drawing", copy.SceneJson); Assert.DoesNotContain("Private new draft", copy.SceneJson);
        Assert.DoesNotContain("private-file", copy.SceneJson); Assert.DoesNotContain(privateImage.Id.ToString(), copy.SceneJson);
        Assert.NotNull(copy.PreviewLightUrl); Assert.NotNull(copy.PreviewDarkUrl);
        var copiedImage = Assert.Single(copy.Files); Assert.NotEqual(publicImage.Id, copiedImage.Id);
        Assert.Contains(copy.Id.ToString(), copiedImage.ContentUrl);
        Assert.Equal((await _db.DrawingAssets.SingleAsync(a => a.Id == publicImage.Id)).Key,
            (await _db.DrawingAssets.SingleAsync(a => a.Id == copiedImage.Id)).Key);
        Assert.Equal(3, await _db.DrawingAssets.CountAsync(a => a.DrawingId == copy.Id));
        Assert.Empty(await _db.DrawingGrants.Where(g => g.DrawingId == copy.Id).ToArrayAsync());
        Assert.Equal(4, _storage.Objects.Count);
        var retry = await _service.CreateAsync(new("notebook", _note, "copied-published-drawing", source.Id), User(_owner), default);
        Assert.Equal(copy.Id, retry.Id);
    }

    [Fact]
    public async Task Internal_storage_rejects_missing_keys_and_browser_origins_before_writing()
    {
        var drawing = await Create();
        var controller = new DrawingStorageController(_service, new(Config)) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<UnauthorizedResult>(await controller.Store(drawing.Id, Projection(), default));
        controller.Request.Headers["X-Notion-Service-Key"] = Secret; controller.Request.Headers.Origin = "https://lup.dk";
        Assert.IsType<UnauthorizedResult>(await controller.Store(drawing.Id, Projection(), default));
        Assert.Equal(0, (await _service.FindAsync(drawing.Id, default))!.Revision);
        controller.Request.Headers.Remove("Origin"); Assert.IsType<OkObjectResult>(await controller.Store(drawing.Id, Projection(), default));
    }

    [Fact]
    public void Scene_limit_and_private_namespace_are_enforced()
    {
        Assert.Throws<DrawingException>(() => DrawingValidation.Validate(Projection() with { StateBase64 = Convert.ToBase64String(new byte[DrawingValidation.MaxSceneBytes + 1]) }));
        Assert.True(NotionMediaPolicy.IsPrivateKey("prefix/drawing-private/hidden"));
        Assert.False(DrawingValidation.IsPreviewPng("not a png"u8));
    }

    private sealed class Engine : HttpMessageHandler, IHttpClientFactory
    {
        private int _counter;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/clone")) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { }) };
            var input = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject();
            var scene = JsonNode.Parse(input["sceneJson"]!.GetValue<string>())!.AsObject();
            var map = input["assetMap"]!.AsObject(); var assets = new List<Guid>();
            foreach (var (_, value) in scene["files"]!.AsObject())
            {
                var old = value!["assetId"]!.GetValue<string>(); var replacement = map[old]!.GetValue<string>();
                value["assetId"] = replacement; assets.Add(Guid.Parse(replacement));
            }
            var json = scene.ToJsonString();
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json + Interlocked.Increment(ref _counter))));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new StoreDrawingDocument(Convert.ToBase64String([0, 0]), fingerprint, json, assets.ToArray())) };
        }
    }
    private sealed class MemoryStorage : IObjectStorage
    {
        public bool IsConfigured => true; public string Bucket => "test";
        public Dictionary<string, byte[]> Objects { get; } = [];
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        { using var copy = new MemoryStream(); await content.CopyToAsync(copy, cancellationToken); Objects[key] = copy.ToArray(); return new(key, Bucket, contentType, copy.Length); }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<ObjectGetResult?>(Objects.TryGetValue(key, out var bytes) ? new(new MemoryStream(bytes), "image/png", bytes.Length) : null);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Objects.Remove(key); return Task.CompletedTask; }
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }
}
