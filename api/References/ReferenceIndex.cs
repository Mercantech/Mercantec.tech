using System.Security.Claims;
using System.Security.Cryptography;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.References;

public sealed partial class ReferenceService
{
    // Prefix the state hash so a change to reference extraction can reindex saved pages without changing their contents.
    internal const string IndexFingerprintPrefix = "v2:";

    /// <summary>Read the previous saved state before a write, establishing a silent historical baseline.</summary>
    public async Task BaselineNotionAsync(Guid id, CancellationToken ct)
    {
        var existing = await db.ReferenceIndexStates.AsNoTracking().Where(s => s.SourceKind == "notion-page" && s.SourceId == id.ToString("D")
                && s.Fingerprint.StartsWith(IndexFingerprintPrefix))
            .Select(s => s.Visibility).ToArrayAsync(ct);
        if (existing.Contains("draft") && existing.Contains("published")) return;
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.YjsState, p.PublishedState, p.PublishedAtUtc, p.CreatedBySub, p.PublishedBySub }).SingleOrDefaultAsync(ct);
        if (page is null) return;
        if (!existing.Contains("draft")) await ReplaceIndexAsync(Ref("notion-page", id), "draft", page.YjsState, page.CreatedBySub, false, ct);
        if (!existing.Contains("published")) await ReplaceIndexAsync(Ref("notion-page", id), "published",
            page.PublishedAtUtc is null ? null : page.PublishedState, page.PublishedBySub ?? page.CreatedBySub, false, ct);
    }

    public async Task RefreshNotionAsync(Guid id, CancellationToken ct, bool published = false)
    {
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.YjsState, p.PublishedState, p.PublishedAtUtc, p.CreatedBySub, p.PublishedBySub }).SingleOrDefaultAsync(ct);
        if (page is null) return;
        await ReplaceIndexAsync(Ref("notion-page", id), published ? "published" : "draft",
            published ? page.PublishedAtUtc is null ? null : page.PublishedState : page.YjsState,
            published ? page.PublishedBySub ?? page.CreatedBySub : page.CreatedBySub, true, ct);
    }

    public async Task BaselineNotebookAsync(Guid id, CancellationToken ct)
    {
        if (await db.ReferenceIndexStates.AnyAsync(s => s.SourceKind == "notebook-page" && s.SourceId == id.ToString("D")
                && s.Fingerprint.StartsWith(IndexFingerprintPrefix), ct)) return;
        var document = await db.NotebookDocuments.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.YjsState, OwnerSub = p.Notebook == null ? Guid.Empty : p.Notebook.OwnerSub }).SingleOrDefaultAsync(ct);
        if (document is not null) await ReplaceIndexAsync(Ref("notebook-page", id), "live", document.YjsState, document.OwnerSub, false, ct);
    }

    public async Task RefreshNotebookAsync(Guid id, CancellationToken ct)
    {
        var document = await db.NotebookDocuments.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.YjsState, p.Kind, p.NotebookId, OwnerSub = p.Notebook == null ? Guid.Empty : p.Notebook.OwnerSub }).SingleOrDefaultAsync(ct);
        if (document is not null) await ReplaceIndexAsync(Ref("notebook-page", id), "live", document.YjsState, document.OwnerSub,
            document.NotebookId is not null && document.Kind != "template", ct);
    }

    private async Task ReplaceIndexAsync(EntityReference source, string visibility, byte[]? state, Guid author,
        bool notify, CancellationToken ct)
    {
        var fingerprint = IndexFingerprintPrefix + Convert.ToBase64String(SHA256.HashData(state ?? []));
        var marker = await db.ReferenceIndexStates.SingleOrDefaultAsync(s => s.SourceKind == source.Kind
            && s.SourceId == source.Id && s.Visibility == visibility, ct);
        if (marker?.Fingerprint == fingerprint) return;
        var references = new HashSet<EntityReference>();
        var mentions = new HashSet<EntityReference>();
        // Corrupt/old unparseable documents remain readable and cannot inject client-supplied projections.
        if (state is { Length: > 0 } && !YjsPageLinks.TryReadReferences(state, source.Kind, references, mentions))
        { references.Clear(); mentions.Clear(); }
        var normalized = references.Select(Normalize).OfType<EntityReference>().Where(r => r != source).Distinct().ToArray();
        var explicitMentions = mentions.Select(Normalize).OfType<EntityReference>().Where(r => r != source).ToHashSet();
        var old = await db.EntityReferenceLinks.Where(l => l.SourceKind == source.Kind && l.SourceId == source.Id
            && l.Visibility == visibility).ToArrayAsync(ct);
        var oldTargets = old.Select(l => new EntityReference(l.TargetKind, l.TargetId)).ToHashSet();
        var wanted = normalized.ToHashSet();
        var oldRecipients = await RecipientsAsync(old.Where(l => l.IsMention).Select(l => new EntityReference(l.TargetKind, l.TargetId)), ct);
        var recipients = await RecipientsAsync(explicitMentions, ct);
        var removedRecipients = oldRecipients.Except(recipients).ToArray();
        if (removedRecipients.Length > 0)
            await db.ReferenceMentionEvents.Where(e => e.SourceKind == source.Kind && e.SourceId == source.Id
                && e.Visibility == visibility && removedRecipients.Contains(e.RecipientSub)).ExecuteDeleteAsync(ct);
        db.EntityReferenceLinks.RemoveRange(old.Where(l => !wanted.Contains(new(l.TargetKind, l.TargetId))));
        foreach (var link in old.Where(l => wanted.Contains(new(l.TargetKind, l.TargetId))))
            link.IsMention = explicitMentions.Contains(new(link.TargetKind, link.TargetId));
        var added = normalized.Where(r => !oldTargets.Contains(r)).ToArray();
        db.EntityReferenceLinks.AddRange(added.Select(r => new EntityReferenceLink
        {
            SourceKind = source.Kind, SourceId = source.Id, Visibility = visibility, TargetKind = r.Kind, TargetId = r.Id,
            IsMention = explicitMentions.Contains(r)
        }));
        if (marker is null) db.ReferenceIndexStates.Add(new() { SourceKind = source.Kind, SourceId = source.Id, Visibility = visibility, Fingerprint = fingerprint });
        else marker.Fingerprint = fingerprint;
        // Teaching drafts never notify. Publication and live personal notes start a pending
        // occurrence even if the recipient has no access yet; inbox reads enforce current ACLs.
        if (notify && visibility != "draft")
        {
            foreach (var recipient in recipients.Except(oldRecipients))
            {
                if (recipient == author) continue;
                db.ReferenceMentionEvents.Add(new() { Id = Guid.NewGuid(), RecipientSub = recipient,
                    SourceKind = source.Kind, SourceId = source.Id, Visibility = visibility, CreatedAtUtc = DateTimeOffset.UtcNow });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<HashSet<Guid>> RecipientsAsync(IEnumerable<EntityReference> references, CancellationToken ct)
    {
        var targets = references.ToArray();
        var personIds = targets.Where(r => r.Kind == "person").Select(r => Guid.Parse(r.Id)).ToArray();
        var memberIds = targets.Where(r => r.Kind == "member").Select(r => Guid.Parse(r.Id)).ToArray();
        var people = await db.NotebookProfiles.AsNoTracking().Where(p => personIds.Contains(p.Sub)).Select(p => p.Sub).ToArrayAsync(ct);
        var members = await db.Memberships.AsNoTracking().Where(m => memberIds.Contains(m.Id) && m.UserSub != null)
            .Select(m => m.UserSub!.Value).ToArrayAsync(ct);
        return people.Concat(members).ToHashSet();
    }

    private async Task<ReferenceGraphEdge[]> VisibleEdgesAsync(List<ResolvedReference> catalog, bool publishedOnly,
        CancellationToken ct, bool linkedPagesOnly = false)
    {
        var byKey = catalog.ToDictionary(r => r.Reference.Key);
        var notionIds = catalog.Where(r => r.Reference.Kind == "notion-page").Select(r => r.Reference.Id).ToArray();
        var notebookIds = catalog.Where(r => r.Reference.Kind == "notebook-page").Select(r => r.Reference.Id).ToArray();
        var links = db.EntityReferenceLinks.AsNoTracking().Where(e => e.SourceKind == "notion-page" && notionIds.Contains(e.SourceId)
            || e.SourceKind == "notebook-page" && notebookIds.Contains(e.SourceId));
        if (linkedPagesOnly)
            links = links.Where(e => (e.TargetKind == "notion-page" || e.TargetKind == "notebook-page")
                && (e.SourceKind != e.TargetKind || e.SourceId != e.TargetId));
        var edges = await links.ToArrayAsync(ct);
        return edges.Where(e => byKey.TryGetValue(e.SourceKind + ":" + e.SourceId, out var source)
                && e.Visibility == (e.SourceKind == "notebook-page" ? "live" : source.CanRename && !publishedOnly ? "draft" : "published")
                && byKey.ContainsKey(e.TargetKind + ":" + e.TargetId))
            .Select(e => new ReferenceGraphEdge(e.SourceKind + ":" + e.SourceId, e.TargetKind + ":" + e.TargetId))
            .Distinct().ToArray();
    }

    private static HashSet<string> ConnectedKeys(IEnumerable<ReferenceGraphEdge> edges)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            keys.Add(edge.FromKey);
            keys.Add(edge.ToKey);
        }
        return keys;
    }

    public async Task<EntityGraph> GraphAsync(ClaimsPrincipal user, ReferenceGraphRequest request, CancellationToken ct)
    {
        var focus = Normalize(request.Focus);
        if (request.Focus is not null && (focus is null || !IsGraphContent(focus.Kind))) return new([], [], 0, false);
        var catalog = await CatalogAsync(user, request.PublishedOnly, ct, contentOnly: true, pagesOnly: request.LinkedPagesOnly);
        var edges = await VisibleEdgesAsync(catalog, request.PublishedOnly, ct, request.LinkedPagesOnly);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        if (focus is not null)
        {
            if (!catalog.Any(r => r.Reference == focus)) return new([], [], 0, false);
            selected.Add(focus.Key);
        }
        else if (request.ScopeKind is "collection" or "notebook" && Guid.TryParse(request.ScopeId, out var collection))
        {
            var scopePage = request.ScopeKind == "collection"
                ? catalog.FirstOrDefault(r => r.Reference == Ref("notion-page", collection))
                : null;
            var canonical = scopePage?.CollectionId ?? collection;
            selected.UnionWith(catalog.Where(r => r.CollectionId == canonical).Select(r => r.Reference.Key));
            if (scopePage is { CollectionId: null })
            {
                var legacy = await db.NotionPages.AsNoTracking()
                    .Where(p => p.HoldId == scopePage.HoldId && p.CollectionId == null)
                    .Select(p => new NotebookPageTree.Row(p.Id, p.ParentPageId, null, "", null)).ToListAsync(ct);
                var branch = NotebookPageTree.Branch(legacy, collection);
                selected.UnionWith(catalog.Where(r => r.Reference.Kind == "notion-page" && branch.Contains(Guid.Parse(r.Reference.Id)))
                    .Select(r => r.Reference.Key));
            }
        }
        else if (request.ScopeKind == "hold" && Guid.TryParse(request.ScopeId, out var hold))
            selected.UnionWith(catalog.Where(r => r.HoldId == hold).Select(r => r.Reference.Key));
        else selected.UnionWith(catalog.Select(r => r.Reference.Key));
        var primary = selected.ToHashSet();
        var connected = ConnectedKeys(edges.Where(e => primary.Contains(e.FromKey) || primary.Contains(e.ToKey)));
        if (request.LinkedPagesOnly) selected = connected;
        else selected.UnionWith(connected);
        var nodes = catalog.Where(r => selected.Contains(r.Reference.Key)).OrderBy(r => r.Reference == focus ? 0 : primary.Contains(r.Reference.Key) ? 1 : 2)
            .ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Reference.Key, StringComparer.Ordinal).ToArray();
        var visible = nodes.Skip(request.Offset).Take(request.Limit).Select(r => new ReferenceGraphNode(r.Reference.Key, r)).ToArray();
        if (!request.LinkedPagesOnly) await ApplyGraphAssetTitlesAsync(visible, catalog, ct);
        var keys = visible.Select(n => n.Key).ToHashSet();
        return new(visible, edges.Where(e => keys.Contains(e.FromKey) && keys.Contains(e.ToKey)).ToArray(), nodes.Length,
            request.Offset + visible.Length < nodes.Length);
    }

    public async Task<ResolvedReference[]> BacklinksAsync(ClaimsPrincipal user, EntityReference reference, bool publishedOnly, CancellationToken ct)
    {
        var target = Normalize(reference);
        if (target is null) return [];
        if (!(await CatalogAsync(user, publishedOnly, ct, [target])).Any(r => r.Reference == target)) return [];
        // Page loads need only this target's incoming edges, never the complete visible graph.
        var links = await db.EntityReferenceLinks.AsNoTracking().Where(l => l.TargetKind == target.Kind && l.TargetId == target.Id)
            .Select(l => new { l.SourceKind, l.SourceId, l.Visibility }).ToArrayAsync(ct);
        if (links.Length == 0) return [];
        var wanted = links.Select(l => new EntityReference(l.SourceKind, l.SourceId)).ToHashSet();
        var sources = await CatalogAsync(user, publishedOnly, ct, wanted);
        return sources.Where(source => links.Any(l => l.SourceKind == source.Reference.Kind && l.SourceId == source.Reference.Id
                && l.Visibility == (l.SourceKind == "notebook-page" ? "live" : source.CanRename && !publishedOnly ? "draft" : "published")))
            .OrderBy(r => r.Title).ToArray();
    }

    public async Task<ActionCenterItem[]> MentionsAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return [];
        var query = db.ReferenceMentionEvents.AsNoTracking().Where(e => e.RecipientSub == sub
            && !db.ActionReadReceipts.Any(r => r.UserSub == sub && r.Kind == "mention" && r.ResourceId == e.Id));
        var events = db.Database.IsNpgsql() ? await query.OrderByDescending(e => e.CreatedAtUtc).Take(100).ToArrayAsync(ct)
            : (await query.ToArrayAsync(ct)).OrderByDescending(e => e.CreatedAtUtc).Take(100).ToArray();
        if (events.Length == 0) return [];
        var liveWanted = events.Where(e => e.Visibility != "published").Select(e => new EntityReference(e.SourceKind, e.SourceId)).ToHashSet();
        var publishedWanted = events.Where(e => e.Visibility == "published").Select(e => new EntityReference(e.SourceKind, e.SourceId)).ToHashSet();
        var catalog = (liveWanted.Count == 0 ? [] : await CatalogAsync(user, false, ct, liveWanted)).ToDictionary(r => r.Reference.Key);
        var published = (publishedWanted.Count == 0 ? [] : await CatalogAsync(user, true, ct, publishedWanted)).ToDictionary(r => r.Reference.Key);
        var memberships = (await db.Memberships.AsNoTracking().Where(m => m.UserSub == sub).Select(m => m.Id).ToArrayAsync(ct))
            .Select(id => id.ToString("D")).ToArray();
        var notionIds = events.Where(e => e.SourceKind == "notion-page").Select(e => e.SourceId).Distinct().ToArray();
        var notebookIds = events.Where(e => e.SourceKind == "notebook-page").Select(e => e.SourceId).Distinct().ToArray();
        var currentLinks = await db.EntityReferenceLinks.AsNoTracking().Where(l => l.IsMention
            && (l.SourceKind == "notion-page" && notionIds.Contains(l.SourceId) || l.SourceKind == "notebook-page" && notebookIds.Contains(l.SourceId))
            && (l.TargetKind == "person" && l.TargetId == sub.ToString("D") || l.TargetKind == "member" && memberships.Contains(l.TargetId)))
            .Select(l => new { l.SourceKind, l.SourceId, l.Visibility }).ToArrayAsync(ct);
        var present = currentLinks.Select(l => (l.SourceKind, l.SourceId, l.Visibility)).ToHashSet();
        var items = new List<ActionCenterItem>();
        foreach (var e in events)
        {
            var key = e.SourceKind + ":" + e.SourceId;
            var readable = e.Visibility == "published" ? published : catalog;
            if (!readable.TryGetValue(key, out var source) || e.Visibility == "draft" && !source.CanRename) continue;
            // Removing a mention, revoking a share, withdrawing a publication or losing membership
            // immediately removes its inbox item, including acknowledgements.
            if (!present.Contains((e.SourceKind, e.SourceId, e.Visibility))) continue;
            items.Add(new("mention", "optional", source.HoldId, null, null, e.Id, null,
                source.Title, source.ContextLabel, null, State: e.Visibility, TargetId: Guid.TryParse(e.SourceId, out var id) ? id : null, Href: source.Href));
        }
        return items.ToArray();
    }

    public async Task AcknowledgeMentionsAsync(ClaimsPrincipal user, ActionCenterReadItem[] requested, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return;
        var wanted = requested.Where(i => i is not null && i.Kind == "mention").Select(i => i.ResourceId).ToHashSet();
        if (wanted.Count == 0) return;
        foreach (var item in (await MentionsAsync(user, ct)).Where(i => wanted.Contains(i.ResourceId)))
        {
            // Same receipt key as the existing action center. Parameterized ON CONFLICT makes retries safe.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO action_read_receipts (\"UserSub\", \"Kind\", \"ResourceId\", \"ReadAtUtc\") VALUES ({sub}, {"mention"}, {item.ResourceId}, {DateTimeOffset.UtcNow}) ON CONFLICT (\"UserSub\", \"Kind\", \"ResourceId\") DO NOTHING", ct);
        }
    }
}
