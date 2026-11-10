using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Drawings;

public sealed partial class DrawingService
{
    private readonly AppDbContext _db;
    private readonly DrawingAccess _access;
    private readonly IObjectStorage _storage;
    private readonly DrawingEngineClient _engine;
    private readonly IDataProtector _tickets;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public DrawingService(AppDbContext db, DrawingAccess access, IObjectStorage storage,
        IHttpClientFactory clients, IConfiguration configuration, NotebookServiceKey key, IDataProtectionProvider protection)
    {
        _db = db; _access = access; _storage = storage;
        _engine = new(clients, configuration, key); _tickets = protection.CreateProtector("LUP.DrawingMedia.v1");
    }

    public Task<Drawing?> FindAsync(Guid id, CancellationToken ct) => _db.Drawings.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
    private IQueryable<Drawing> ParentDrawings(string domain, Guid id) => domain == "notion"
        ? _db.Drawings.Where(d => d.NotionPageId == id) : _db.Drawings.Where(d => d.NotebookDocumentId == id);
    private static Drawing Parent(string domain, Guid documentId, string blockId) => new()
    {
        NotionPageId = domain == "notion" ? documentId : null,
        NotebookDocumentId = domain == "notebook" ? documentId : null, BlockId = blockId
    };
    private static void ValidParent(CreateDrawing input)
    {
        if (input.Domain is not ("notion" or "notebook") || input.DocumentId == Guid.Empty
            || string.IsNullOrWhiteSpace(input.BlockId) || input.BlockId.Length > 128 || input.BlockId.Any(char.IsControl)) throw DrawingValidation.Invalid();
    }

    public async Task<DrawingContext> CreateAsync(CreateDrawing input, ClaimsPrincipal user, CancellationToken ct)
    {
        ValidParent(input);
        var scope = Parent(input.Domain, input.DocumentId, input.BlockId);
        if (!await _access.CanManageAsync(scope, user, ct)) throw DrawingValidation.Denied();
        var existing = await ParentDrawings(input.Domain, input.DocumentId).AsNoTracking().SingleOrDefaultAsync(d => d.BlockId == input.BlockId, ct);
        if (existing is not null) return await ContextAsync(existing.Id, user, false, null, ct);
        Drawing drawing;
        if (input.SourceDrawingId is Guid sourceId)
        {
            var source = await FindAsync(sourceId, ct) ?? throw DrawingValidation.Denied();
            if (!await _access.CanReadParentAsync(source, user, ct)) throw DrawingValidation.Denied();
            DrawingSnapshot? snapshot = null;
            if (!await _access.CanReadHeadAsync(source, user, ct))
            {
                var binding = await _access.PublishedBindingAsync(source, ct);
                snapshot = binding?.SnapshotId is Guid sid ? await _db.DrawingSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sid && s.DrawingId == source.Id, ct) : null;
                if (snapshot is null) throw DrawingValidation.Denied();
            }
            var copy = await CloneOneAsync(source, snapshot, input.Domain, input.DocumentId, input.BlockId, ct);
            drawing = copy;
        }
        else
        {
            drawing = scope; drawing.Id = Guid.NewGuid(); drawing.CreatedBySub = UserClaims.GetSub(user)!.Value;
            drawing.CreatedAtUtc = drawing.UpdatedAtUtc = DateTimeOffset.UtcNow; _db.Drawings.Add(drawing);
        }
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            existing = await ParentDrawings(input.Domain, input.DocumentId).AsNoTracking().SingleOrDefaultAsync(d => d.BlockId == input.BlockId, ct);
            if (existing is null) throw;
            return await ContextAsync(existing.Id, user, false, null, ct);
        }
        return await ContextAsync(drawing.Id, user, false, null, ct);
    }

    public async Task<DrawingContext> ContextAsync(Guid id, ClaimsPrincipal user, bool published, Guid? snapshotId, CancellationToken ct)
    {
        var drawing = await FindAsync(id, ct) ?? throw DrawingValidation.Denied();
        if (!await _access.CanReadParentAsync(drawing, user, ct)) throw DrawingValidation.Denied();
        var edit = await _access.CanEditAsync(drawing, user, ct); var manage = await _access.CanManageAsync(drawing, user, ct);
        var binding = published || snapshotId is not null ? await _access.PublishedBindingAsync(drawing, ct) : null;
        if ((published || snapshotId is not null) && drawing.NotionPageId is not null && binding is null) throw DrawingValidation.Denied();
        if (snapshotId is not null && binding?.SnapshotId != snapshotId) throw DrawingValidation.Denied();
        DrawingSnapshot? snapshot = null;
        if (binding?.Mode == "snapshot" && binding.SnapshotId is Guid frozen)
            snapshot = await _db.DrawingSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == frozen && s.DrawingId == id, ct) ?? throw DrawingValidation.Denied();
        if (snapshot is null && !await _access.CanReadHeadAsync(drawing, user, ct)) throw DrawingValidation.Denied();
        var assetIds = DrawingAccess.Ids(snapshot?.AssetIdsJson ?? drawing.AssetIdsJson);
        var files = await _db.DrawingAssets.AsNoTracking().Where(a => a.DrawingId == id && assetIds.Contains(a.Id)).ToArrayAsync(ct);
        var freshPreview = drawing.PreviewFingerprint == drawing.Fingerprint && drawing.PreviewRevision == drawing.Revision;
        var light = snapshot?.PreviewLightAssetId ?? (freshPreview ? drawing.PreviewLightAssetId : null);
        var dark = snapshot?.PreviewDarkAssetId ?? (freshPreview ? drawing.PreviewDarkAssetId : null);
        return new(id, drawing.Domain, drawing.DocumentId, drawing.BlockId, true, edit, manage, binding?.Mode ?? drawing.Mode,
            snapshot?.Revision ?? drawing.Revision, snapshot?.Fingerprint ?? drawing.Fingerprint, drawing.AccessVersion,
            snapshot is null && drawing.YjsState is not null ? Convert.ToBase64String(drawing.YjsState) : null,
            snapshot?.SceneJson ?? drawing.SceneJson, snapshot?.Id,
            light is Guid l ? MediaUrl(id, l, user) : null, dark is Guid d ? MediaUrl(id, d, user) : null,
            files.Select(a => FileView(a, user)).ToArray(), drawing.UpdatedAtUtc, await ParentLifecycleVersionAsync(drawing, ct));
    }

    public async Task<DrawingStored> StoreAsync(Guid id, StoreDrawingDocument input, CancellationToken ct)
    {
        var bytes = DrawingValidation.Validate(input);
        var drawing = await FindAsync(id, ct) ?? throw DrawingValidation.Denied();
        if (await _db.DrawingAssets.CountAsync(a => a.DrawingId == id && a.Kind == "image" && input.AssetIds.Contains(a.Id), ct) != input.AssetIds.Length)
            throw DrawingValidation.Invalid("invalid_drawing_assets");
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await LockParentAsync(drawing.Domain, drawing.DocumentId, ct);
        drawing = await FindAsync(id, ct) ?? throw DrawingValidation.Denied();
        if (await ParentLifecycleVersionAsync(drawing, ct) != input.LifecycleVersion)
            throw new DrawingException(409, "stale_document_session", "Åbn tegningen igen for at fortsætte.");
        var changed = drawing.Fingerprint != input.Fingerprint; var now = DateTimeOffset.UtcNow;
        var revision = drawing.Revision + (changed ? 1 : 0);
        await _db.Drawings.Where(d => d.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.YjsState, bytes).SetProperty(d => d.SceneJson, input.SceneJson)
            .SetProperty(d => d.Fingerprint, input.Fingerprint).SetProperty(d => d.AssetIdsJson, JsonSerializer.Serialize(input.AssetIds))
            .SetProperty(d => d.Revision, revision).SetProperty(d => d.UpdatedAtUtc, now), ct);
        if (changed && !input.PreserveEditMetadata)
            await TouchParentAsync(drawing.Domain, drawing.DocumentId, now, UserClaims.CleanEditorName(input.EditorName), ct);
        await transaction.CommitAsync(ct);
        return new(now, revision, input.Fingerprint, drawing.AccessVersion);
    }

    public async Task LockParentAsync(string domain, Guid documentId, CancellationToken ct)
    {
        var count = domain == "notion"
            ? await _db.NotionPages.Where(p => p.Id == documentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAtUtc, p => p.UpdatedAtUtc), ct)
            : await _db.NotebookDocuments.Where(p => p.Id == documentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAtUtc, p => p.UpdatedAtUtc), ct);
        if (count != 1) throw DrawingValidation.Denied();
    }
    public async Task<long> ParentLifecycleVersionAsync(Drawing drawing, CancellationToken ct)
    {
        var id = drawing.DocumentId;
        var version = drawing.Domain == "notion"
            ? await _db.NotionPages.Where(p => p.Id == id).Select(p => (long?)p.LifecycleVersion).SingleOrDefaultAsync(ct)
            : await _db.NotebookDocuments.Where(p => p.Id == id).Select(p => (long?)p.LifecycleVersion).SingleOrDefaultAsync(ct);
        return version ?? throw DrawingValidation.Denied();
    }
    private Task<int> TouchParentAsync(string domain, Guid id, DateTimeOffset now, string? editorName, CancellationToken ct) => domain == "notion"
        ? _db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.DraftRevision, p => p.DraftRevision + 1)
            .SetProperty(p => p.UpdatedAtUtc, now).SetProperty(p => p.LastEditedByName, editorName), ct)
        : _db.NotebookDocuments.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Revision, p => p.Revision + 1)
            .SetProperty(p => p.UpdatedAtUtc, now).SetProperty(p => p.LastEditedByName, editorName), ct);
    public Task CheckpointAsync(IEnumerable<Guid> ids, CancellationToken ct) => _engine.CheckpointAsync(ids, ct);

    public async Task<DrawingPublicationBinding[]> PreparePublicationAsync(string domain, Guid documentId,
        IReadOnlyCollection<Guid> referencedIds, IReadOnlyCollection<DrawingPublicationRequest>? requests, CancellationToken ct)
    {
        var refs = referencedIds.Distinct().ToArray();
        if (refs.Length > 1000 || refs.Any(id => id == Guid.Empty) || (requests?.Count ?? 0) != refs.Length
            || requests is not null && (requests.Select(r => r.DrawingId).Distinct().Count() != requests.Count || !refs.ToHashSet().SetEquals(requests.Select(r => r.DrawingId))))
            throw DrawingValidation.Invalid("invalid_drawing_publication");
        if (refs.Length == 0) return [];
        await LockParentAsync(domain, documentId, ct);
        var result = new List<DrawingPublicationBinding>();
        foreach (var request in requests!.OrderBy(r => r.DrawingId))
        {
            if (request.Mode is not ("snapshot" or "live") || !DrawingValidation.Fingerprint(request.ExpectedFingerprint)) throw DrawingValidation.Invalid();
            var drawing = await ParentDrawings(domain, documentId).AsNoTracking().SingleOrDefaultAsync(d => d.Id == request.DrawingId, ct) ?? throw DrawingValidation.Invalid();
            if (domain == "notion" && request.Mode != drawing.Mode) throw DrawingValidation.Changed();
            if (await _db.Drawings.Where(d => d.Id == drawing.Id && d.Revision == request.ExpectedRevision && d.Fingerprint == request.ExpectedFingerprint)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.UpdatedAtUtc, d => d.UpdatedAtUtc), ct) != 1) throw DrawingValidation.Changed();
            var snapshot = request.Mode == "snapshot" ? await FreezeAsync(drawing, ct) : null;
            result.Add(new(drawing.Id, drawing.BlockId, request.Mode, snapshot?.Id, drawing.Revision, drawing.Fingerprint!));
        }
        await _db.SaveChangesAsync(ct);
        return result.ToArray();
    }

    public async Task<DrawingPublicationBinding[]> PrepareTemplateAsync(Guid templateDocumentId, IReadOnlyCollection<Guid> referencedIds,
        IReadOnlyCollection<DrawingPublicationRequest>? requests, CancellationToken ct)
    {
        if (requests?.Any(r => r.Mode != "snapshot") == true) throw DrawingValidation.Invalid();
        return await PreparePublicationAsync("notebook", templateDocumentId, referencedIds, requests, ct);
    }

    private async Task<DrawingSnapshot> FreezeAsync(Drawing drawing, CancellationToken ct)
    {
        if (drawing.YjsState is null || drawing.Fingerprint is null || drawing.PreviewFingerprint != drawing.Fingerprint
            || drawing.PreviewRevision != drawing.Revision || drawing.PreviewLightAssetId is null || drawing.PreviewDarkAssetId is null)
            throw new DrawingException(409, "drawing_preview_required", "Åbn tegningen, og vent på, at forhåndsvisningen er gemt, før du udgiver.");
        var previous = await _db.DrawingSnapshots.SingleOrDefaultAsync(s => s.DrawingId == drawing.Id && s.Revision == drawing.Revision, ct);
        if (previous is not null) return previous;
        var scene = DrawingValidation.SnapshotScene(drawing.SceneJson);
        var identity = DrawingAccess.Ids(drawing.AssetIdsJson).ToDictionary(id => id.ToString(), id => id.ToString());
        var canonical = await _engine.CloneAsync(null, scene, identity, ct);
        var snapshot = new DrawingSnapshot { Id = Guid.NewGuid(), DrawingId = drawing.Id, Revision = drawing.Revision,
            Fingerprint = drawing.Fingerprint, YjsState = Convert.FromBase64String(canonical.StateBase64), SceneJson = canonical.SceneJson,
            AssetIdsJson = JsonSerializer.Serialize(canonical.AssetIds), PreviewLightAssetId = drawing.PreviewLightAssetId.Value,
            PreviewDarkAssetId = drawing.PreviewDarkAssetId.Value, CreatedAtUtc = DateTimeOffset.UtcNow };
        _db.DrawingSnapshots.Add(snapshot); return snapshot;
    }

    public async Task<DrawingCloneResult> CloneTemplateAsync(Guid sourceDocumentId, Guid targetDocumentId,
        IReadOnlyCollection<DrawingPublicationBinding> bindings, CancellationToken ct)
    {
        var map = new Dictionary<string, string>();
        foreach (var binding in bindings)
        {
            var source = await ParentDrawings("notebook", sourceDocumentId).AsNoTracking().SingleOrDefaultAsync(d => d.Id == binding.DrawingId, ct) ?? throw DrawingValidation.Invalid();
            var snapshot = binding.SnapshotId is Guid sid ? await _db.DrawingSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sid && s.DrawingId == source.Id, ct) : null;
            if (snapshot is null) throw DrawingValidation.Invalid();
            var copy = await CloneOneAsync(source, snapshot, "notebook", targetDocumentId, "pending-" + Guid.NewGuid().ToString("N"), ct);
            map[source.Id.ToString()] = copy.Id.ToString();
        }
        return new(map);
    }

    public void BindClonedBlocks(IReadOnlyDictionary<string, string>? bindings)
    {
        foreach (var entry in _db.ChangeTracker.Entries<Drawing>().Where(e => e.State == EntityState.Added && e.Entity.BlockId.StartsWith("pending-")))
        {
            if (bindings is null || !bindings.TryGetValue(entry.Entity.Id.ToString(), out var block) || string.IsNullOrWhiteSpace(block) || block.Length > 128)
                throw DrawingValidation.Invalid("missing_drawing_binding");
            entry.Entity.BlockId = block;
        }
    }

    private async Task<Drawing> CloneOneAsync(Drawing source, DrawingSnapshot? snapshot, string domain, Guid documentId, string blockId, CancellationToken ct)
    {
        var assetIds = DrawingAccess.Ids(snapshot?.AssetIdsJson ?? source.AssetIdsJson).ToHashSet();
        var light = snapshot?.PreviewLightAssetId ?? source.PreviewLightAssetId; var dark = snapshot?.PreviewDarkAssetId ?? source.PreviewDarkAssetId;
        if (light is Guid l) assetIds.Add(l); if (dark is Guid d) assetIds.Add(d);
        var originals = await _db.DrawingAssets.AsNoTracking().Where(a => a.DrawingId == source.Id && assetIds.Contains(a.Id)).ToArrayAsync(ct);
        if (originals.Length != assetIds.Count) throw DrawingValidation.Invalid();
        var drawing = Parent(domain, documentId, blockId); drawing.Id = Guid.NewGuid(); drawing.CreatedBySub = source.CreatedBySub;
        drawing.CreatedAtUtc = drawing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var map = originals.ToDictionary(a => a.Id.ToString(), _ => Guid.NewGuid().ToString());
        var canonical = await _engine.CloneAsync(null, snapshot?.SceneJson ?? DrawingValidation.SnapshotScene(source.SceneJson), map, ct);
        drawing.YjsState = Convert.FromBase64String(canonical.StateBase64); drawing.Fingerprint = canonical.Fingerprint;
        drawing.SceneJson = canonical.SceneJson; drawing.AssetIdsJson = JsonSerializer.Serialize(canonical.AssetIds); drawing.Revision = 1;
        if (light is Guid oldLight && dark is Guid oldDark && (snapshot is not null || source.PreviewFingerprint == source.Fingerprint))
        {
            drawing.PreviewLightAssetId = Guid.Parse(map[oldLight.ToString()]); drawing.PreviewDarkAssetId = Guid.Parse(map[oldDark.ToString()]);
            drawing.PreviewFingerprint = canonical.Fingerprint; drawing.PreviewRevision = 1;
        }
        _db.Drawings.Add(drawing);
        foreach (var original in originals) _db.DrawingAssets.Add(new DrawingAsset { Id = Guid.Parse(map[original.Id.ToString()]), DrawingId = drawing.Id,
            Key = original.Key, OriginalName = original.OriginalName, ContentType = original.ContentType, Kind = original.Kind,
            SizeBytes = original.SizeBytes, UploadedBySub = original.UploadedBySub, CreatedAtUtc = drawing.CreatedAtUtc });
        return drawing;
    }
}
