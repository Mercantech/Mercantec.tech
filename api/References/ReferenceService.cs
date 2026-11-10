using System.Security.Claims;
using System.Linq.Expressions;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.Courses;
using Mercantec.Tech.Web.Public;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.References;

/// <summary>Permission-filtered navigation metadata. A reference is never a capability or sharing grant.</summary>
public sealed partial class ReferenceService(AppDbContext db)
{
    private readonly CourseAccess courses = new(db);
    private readonly NotebookAccess notebooks = new(db);

    public static readonly HashSet<string> Kinds = ["notion-page", "notebook-page", "collection", "notebook", "hold",
        "teaching-course", "quest", "material", "poll", "person", "member", "group", "event", "submission", "file",
        "notion-asset", "notebook-asset", "drawing", "learning-plan", "subject", "lesson", "project", "badge", "character"];

    // Connections describe teaching content. People and polls remain available to mentions and their own views.
    private static bool IsGraphContent(string kind) => kind is not ("person" or "member" or "poll");

    public static EntityReference? Normalize(EntityReference? reference)
    {
        if (reference is null || !Kinds.Contains(reference.Kind) || reference.Id is not { Length: > 0 and <= 300 }
            || reference.Id.Any(char.IsControl)) return null;
        if (reference.Kind is "learning-plan" or "subject" or "lesson" or "project" or "badge" or "character")
            return reference.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '/')
                ? reference with { Id = reference.Id.ToLowerInvariant() } : null;
        return Guid.TryParseExact(reference.Id, "D", out var id) && id != Guid.Empty ? reference with { Id = id.ToString("D") } : null;
    }

    public async Task<ReferenceSearchResult> SearchAsync(ClaimsPrincipal user, string? q, EntityReference? source,
        Guid? preferredHold, int offset, int limit, bool publishedOnly, CancellationToken ct, bool contentOnly = false,
        bool linkedPagesOnly = false)
    {
        contentOnly |= linkedPagesOnly;
        var catalog = await CatalogAsync(user, publishedOnly, ct, search: q, contentOnly: contentOnly, pagesOnly: linkedPagesOnly);
        if (linkedPagesOnly)
        {
            var edges = await VisibleEdgesAsync(catalog, publishedOnly, ct, linkedPagesOnly: true);
            var connected = ConnectedKeys(edges);
            catalog.RemoveAll(r => !connected.Contains(r.Reference.Key));
        }
        var current = source is null ? null : catalog.FirstOrDefault(r => r.Reference == Normalize(source));
        var activeHold = current?.HoldId ?? preferredHold;
        var aliases = new Dictionary<string, string>();
        if (!contentOnly)
        {
            var memberIds = catalog.Where(r => r.Reference.Kind == "member").Select(r => Guid.Parse(r.Reference.Id)).ToArray();
            var memberships = await db.Memberships.AsNoTracking().Where(m => memberIds.Contains(m.Id) && m.UserSub != null)
                .Select(m => new { m.Id, m.UserSub, m.HoldId, m.DisplayName, HoldName = m.Hold.Name }).ToArrayAsync(ct);
            var knownPeople = catalog.Where(r => r.Reference.Kind == "person").Select(r => Guid.Parse(r.Reference.Id)).ToHashSet();
            var mergedMembers = memberships.Where(m => knownPeople.Contains(m.UserSub!.Value)).ToArray();
            var duplicateKeys = mergedMembers.Select(m => Ref("member", m.Id).Key).ToHashSet();
            aliases = mergedMembers.GroupBy(m => m.UserSub!.Value).ToDictionary(g => Ref("person", g.Key).Key,
                g => string.Join(" ", g.Select(m => m.DisplayName + " " + m.HoldName)));
            catalog = catalog.Where(r => !duplicateKeys.Contains(r.Reference.Key)).Select(r =>
            {
                if (r.Reference.Kind != "person" || activeHold is not Guid hold) return r;
                var local = mergedMembers.FirstOrDefault(m => m.UserSub == Guid.Parse(r.Reference.Id) && m.HoldId == hold);
                return local is null ? r : r with { HoldId = hold, ContextLabel = local.HoldName + " · " + r.ContextLabel };
            }).ToList();
        }
        var words = (q ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = catalog.Where(r => words.All(word => (r.Title + " " + r.ContextLabel + " " + aliases.GetValueOrDefault(r.Reference.Key, ""))
                .Contains(word, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(r => current?.CollectionId is Guid collection && r.CollectionId == collection ? 0
                : activeHold is Guid hold && r.HoldId == hold ? 1 : 2)
            .ThenBy(r => r.Title.Equals(q?.Trim(), StringComparison.CurrentCultureIgnoreCase) ? 0
                : r.Title.StartsWith(q?.Trim() ?? "", StringComparison.CurrentCultureIgnoreCase) ? 1 : 2)
            .ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Reference.Key, StringComparer.Ordinal).ToArray();
        var items = matches.Skip(offset).Take(limit).ToArray();
        return new(await MentionStatusesAsync(items, current, user, publishedOnly, ct), matches.Length);
    }

    public async Task<ResolvedReference[]> ResolveAsync(ClaimsPrincipal user, IEnumerable<EntityReference> requested,
        EntityReference? source, bool publishedOnly, CancellationToken ct)
    {
        var wanted = requested.Select(Normalize).OfType<EntityReference>().Distinct().ToHashSet();
        var sourceReference = Normalize(source);
        var lookup = wanted.ToHashSet();
        if (sourceReference is not null) lookup.Add(sourceReference);
        var catalog = await CatalogAsync(user, publishedOnly, ct, lookup);
        var current = source is null ? null : catalog.FirstOrDefault(r => r.Reference == Normalize(source));
        return await MentionStatusesAsync(catalog.Where(r => wanted.Contains(r.Reference)).ToArray(), current, user, publishedOnly, ct);
    }

    public async Task<ReferencePerson?> PersonAsync(ClaimsPrincipal user, Guid subject, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is null) return null;
        var profile = await db.NotebookProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Sub == subject, ct);
        if (profile is null) return null;
        var holds = courses.AccessibleHolds(user).Select(h => h.Id);
        var members = await db.Memberships.AsNoTracking().Where(m => m.UserSub == subject && holds.Contains(m.HoldId))
            .Select(m => new { m.Id, m.HoldId, m.DisplayName, HoldName = m.Hold.Name }).ToArrayAsync(ct);
        return new(subject, profile.DisplayName, profile.IsTeacher, members.Select(m => new ResolvedReference(
            Ref("member", m.Id), m.DisplayName ?? profile.DisplayName,
            $"/notion/holds/{m.HoldId:D}/students/{m.Id:D}", m.HoldName, m.HoldId)).ToArray());
    }

    private async Task<ResolvedReference[]> MentionStatusesAsync(ResolvedReference[] items, ResolvedReference? source,
        ClaimsPrincipal user, bool publishedOnly, CancellationToken ct)
    {
        if (source is null) return items;
        var result = new List<ResolvedReference>();
        foreach (var item in items)
        {
            if (item.Reference.Kind is not ("person" or "member")) { result.Add(item); continue; }
            var recipient = item.Reference.Kind == "person" ? Guid.Parse(item.Reference.Id)
                : await db.Memberships.Where(m => m.Id == Guid.Parse(item.Reference.Id)).Select(m => m.UserSub).SingleOrDefaultAsync(ct);
            if (recipient is not Guid recipientSub) { result.Add(item with { MentionStatus = "no-account" }); continue; }
            var status = recipientSub == UserClaims.GetSub(user) ? "self"
                : await RecipientCanReadAsync(recipientSub, source.Reference, publishedOnly, ct) ? "will-notify" : "no-access";
            result.Add(item with { MentionStatus = status });
        }
        return result.ToArray();
    }

    public async Task<bool> RecipientCanReadAsync(Guid recipient, EntityReference source, bool publishedOnly, CancellationToken ct)
    {
        var profile = await db.NotebookProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Sub == recipient, ct);
        var principal = Candidate(recipient, profile?.IsTeacher == true);
        if (!Guid.TryParse(source.Id, out var id)) return false;
        if (source.Kind == "notion-page") return await new NotionAccess(db, courses).CanReadAsync(id, principal, ct, publishedOnly);
        return source.Kind == "notebook-page" && await notebooks.CanReadAsync(id, principal, ct);
    }

    private static ClaimsPrincipal Candidate(Guid sub, bool teacher) => new(new ClaimsIdentity(teacher
        ? [new Claim("sub", sub.ToString()), new Claim("role", "Underviser")]
        : [new Claim("sub", sub.ToString())], "reference-candidate"));

    private static EntityReference Ref(string kind, Guid id) => new(kind, id.ToString("D"));
    private static bool ContainsId(string? manifest, Guid id)
    {
        try { return (JsonSerializer.Deserialize<Guid[]>(manifest ?? "[]") ?? []).Contains(id); }
        catch (JsonException) { return false; }
    }

    private async Task<List<ResolvedReference>> CatalogAsync(ClaimsPrincipal user, bool publishedOnly, CancellationToken ct,
        HashSet<EntityReference>? wanted = null, string? search = null, bool contentOnly = false, bool pagesOnly = false)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return [];
        var result = new List<ResolvedReference>();
        Guid[] Ids(string kind) => wanted?.Where(r => r.Kind == kind && Guid.TryParse(r.Id, out _)).Select(r => Guid.Parse(r.Id)).ToArray() ?? [];
        IQueryable<T> Only<T>(IQueryable<T> query, string kind, Expression<Func<T, Guid>> selector, params Guid[] extra)
        {
            if (wanted is null) return query;
            var ids = Ids(kind).Concat(extra).Distinct().ToArray();
            return query.Where(Expression.Lambda<Func<T, bool>>(Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)],
                Expression.Constant(ids), selector.Body), selector.Parameters));
        }
        var holds = await courses.AccessibleHolds(user).AsNoTracking().Select(h => new { h.Id, h.Name, h.ForloebId }).ToArrayAsync(ct);
        var holdIds = holds.Select(h => h.Id).ToArray();
        var holdNames = holds.ToDictionary(h => h.Id, h => h.Name);
        var manageable = publishedOnly ? [] : await courses.AccessibleHolds(user, true).Select(h => h.Id).ToArrayAsync(ct);
        var manageSet = manageable.ToHashSet();
        string HoldName(Guid? id) => id is Guid h ? holdNames.GetValueOrDefault(h, "Hold") : "Teori";
        void Add(string kind, Guid id, string title, string href, Guid? hold = null, Guid? collection = null,
            bool rename = false, string? context = null) => result.Add(new(Ref(kind, id), title, href, context ?? HoldName(hold), hold, collection, rename));
        if (!pagesOnly)
        foreach (var h in holds.Where(h => wanted is null || wanted.Contains(Ref("hold", h.Id)))) Add("hold", h.Id, h.Name, $"/holds/{h.Id:D}?view=overview", h.Id);

        var extraPages = new List<Guid>(); var extraNotes = new List<Guid>();
        if (wanted is not null)
        {
            var assetIds = Ids("notion-asset");
            extraPages.AddRange(await db.NotionAssets.Where(a => assetIds.Contains(a.Id)).Select(a => a.PageId).ToArrayAsync(ct));
            var noteAssetIds = Ids("notebook-asset");
            extraNotes.AddRange(await db.NotebookAssets.Where(a => noteAssetIds.Contains(a.Id)).Select(a => a.DocumentId).ToArrayAsync(ct));
            var drawingIds = Ids("drawing");
            var parents = await db.Drawings.Where(d => drawingIds.Contains(d.Id)).Select(d => new { d.NotionPageId, d.NotebookDocumentId }).ToArrayAsync(ct);
            extraPages.AddRange(parents.Where(d => d.NotionPageId != null).Select(d => d.NotionPageId!.Value));
            extraNotes.AddRange(parents.Where(d => d.NotebookDocumentId != null).Select(d => d.NotebookDocumentId!.Value));
        }
        var pageQuery = new NotionAccess(db, courses).ReadablePages(user, publishedOnly).AsNoTracking();
        if (wanted is not null)
        {
            var pageIds = Ids("notion-page").Concat(Ids("collection")).Concat(extraPages).ToArray();
            var collectionIds = Ids("collection");
            pageQuery = pageQuery.Where(p => pageIds.Contains(p.Id) || p.CollectionId != null && collectionIds.Contains(p.CollectionId.Value));
        }
        var pages = await pageQuery
            .Select(p => new { p.Id, p.HoldId, p.CollectionId, p.ParentPageId, p.Title, p.PublishedTitle,
                p.PublishedAssetIdsJson, p.PublishedDrawingsJson, p.DrawingIdsJson }).ToArrayAsync(ct);
        foreach (var p in pages)
        {
            var edit = !publishedOnly && (p.HoldId is Guid h ? manageSet.Contains(h) : UserClaims.IsTeacher(user));
            var title = edit ? p.Title : p.PublishedTitle!;
            var href = $"/notion/{p.Id:D}" + (edit ? "" : "?published=true");
            Add("notion-page", p.Id, title, href, p.HoldId, p.CollectionId, edit);
            if (!pagesOnly && p.CollectionId is null && p.ParentPageId is null)
                Add("collection", p.Id, title, href, p.HoldId, p.Id, edit);
        }
        if (!pagesOnly && (wanted is null || Ids("collection").Length > 0))
        {
            var visibleCollections = pages.Where(p => p.CollectionId != null).Select(p => p.CollectionId!.Value).Distinct().ToArray();
            var requestedCollections = Ids("collection");
            var editableHolds = manageSet.ToArray();
            var canEditTheory = !publishedOnly && UserClaims.IsTeacher(user);
            var byId = pages.ToDictionary(p => p.Id);
            var collections = await db.NotionCollections.AsNoTracking()
                .Where(c => visibleCollections.Contains(c.Id)
                    || (wanted == null || requestedCollections.Contains(c.Id))
                    && !publishedOnly && (c.HoldId == null ? canEditTheory : editableHolds.Contains(c.HoldId.Value)))
                .Select(c => new { c.Id, c.HoldId, c.Title, c.PublishedTitle,
                    HasActiveRoot = db.NotionPages.Any(p => p.Id == c.Id) }).ToArrayAsync(ct);
            foreach (var c in collections)
            {
                var edit = !publishedOnly && (c.HoldId is Guid h ? manageSet.Contains(h) : canEditTheory);
                var root = byId.GetValueOrDefault(c.Id);
                var title = edit ? root?.Title ?? c.Title : root?.PublishedTitle ?? (c.HasActiveRoot ? null : c.PublishedTitle);
                Add("collection", c.Id, title ?? "Samling", $"/notion/collections/{c.Id:D}" + (edit ? "" : "?published=true"),
                    c.HoldId, c.Id, edit && root is not null);
            }
        }

        var noteQuery = notebooks.ReadableDocuments(user).AsNoTracking().Where(d => d.NotebookId != null);
        if (wanted is not null)
        {
            var noteIds = Ids("notebook-page").Concat(extraNotes).ToArray(); var notebookIds = Ids("notebook");
            noteQuery = noteQuery.Where(d => noteIds.Contains(d.Id) || notebookIds.Contains(d.NotebookId!.Value));
        }
        var notes = await noteQuery
            .Select(d => new { d.Id, d.Title, d.NotebookId, d.HoldId, d.Notebook!.OwnerSub, d.AssetIdsJson }).ToArrayAsync(ct);
        foreach (var d in notes) Add("notebook-page", d.Id, d.Title, $"/notebook/{d.Id:D}", d.HoldId,
            d.NotebookId, d.OwnerSub == sub, "Personlige noter");
        if (pagesOnly) return result;

        var pageAccess = pages.ToDictionary(p => p.Id);
        var readablePageIds = pages.Select(p => p.Id).ToArray();
        var noteAccess = notes.ToDictionary(d => d.Id);
        var readableNoteIds = notes.Select(d => d.Id).ToArray();
        var ownerIds = notes.Select(d => d.OwnerSub).Distinct().ToArray();
        var profileQuery = Only(db.NotebookProfiles.AsNoTracking(), "person", p => p.Sub, ownerIds);
        if (contentOnly) profileQuery = profileQuery.Where(p => ownerIds.Contains(p.Sub));
        var profiles = await profileQuery.ToArrayAsync(ct);
        var profileNames = profiles.ToDictionary(p => p.Sub, p => p.DisplayName);
        var readableNotebooks = Only(db.Notebooks.AsNoTracking().Where(n => n.OwnerSub == sub || UserClaims.IsTeacher(user)
            || db.NotebookShares.Any(s => s.NotebookId == n.Id && s.RecipientSub == sub && s.PageId == null)), "notebook", n => n.Id);
        foreach (var nb in await readableNotebooks.Select(n => new { n.Id, n.OwnerSub }).ToArrayAsync(ct))
        {
            var owner = nb.OwnerSub;
            Add("notebook", nb.Id, owner == sub ? "Min notesbog" : profileNames.GetValueOrDefault(owner, "LUP-bruger") + "s notesbog",
                owner == sub ? "/notebook" : $"/notebook?owner={nb.Id:D}", collection: nb.Id, context: "Personlig notesbog");
        }
        if (!contentOnly)
        foreach (var p in profiles.Where(p => wanted is null || wanted.Contains(Ref("person", p.Sub)))) Add("person", p.Sub, p.DisplayName, $"/people/{p.Sub:D}",
            context: p.IsTeacher ? "Underviser · LUP-bruger" : "LUP-bruger");

        if (!contentOnly)
        foreach (var m in await Only(db.Memberships.AsNoTracking().Where(m => holdIds.Contains(m.HoldId)), "member", m => m.Id)
            .Select(m => new { m.Id, m.HoldId, m.DisplayName }).ToArrayAsync(ct))
            Add("member", m.Id, m.DisplayName ?? "Elev", $"/notion/holds/{m.HoldId:D}/students/{m.Id:D}", m.HoldId);

        foreach (var c in await Only(db.TeachingCourses.AsNoTracking().Where(c => holdIds.Contains(c.HoldId)
            && (manageable.Contains(c.HoldId) || c.Published && !c.Archived)), "teaching-course", c => c.Id)
            .Select(c => new { c.Id, c.HoldId, c.Title }).ToArrayAsync(ct))
            Add("teaching-course", c.Id, c.Title, $"/holds/{c.HoldId:D}?view=content&course={c.Id:D}", c.HoldId);
        var readableQuests = new QuestAudienceAccess(db).Readable(db.LearningQuests.AsNoTracking(), sub).Select(q => q.Id);
        var questQuery = db.LearningQuests.AsNoTracking().Where(q => holdIds.Contains(q.HoldId)
            && (manageable.Contains(q.HoldId) || readableQuests.Contains(q.Id)));
        if (wanted is not null)
        {
            var wantedQuests = Ids("quest"); var submissionIds = Ids("submission");
            questQuery = questQuery.Where(q => wantedQuests.Contains(q.Id) || db.QuestSubmissions.Any(s => submissionIds.Contains(s.Id) && s.QuestId == q.Id));
        }
        var quests = await questQuery
            .Select(q => new { q.Id, q.HoldId, q.Title }).ToArrayAsync(ct);
        foreach (var q in quests) Add("quest", q.Id, q.Title, $"/quests/{q.Id:D}?hold={q.HoldId:D}", q.HoldId);
        foreach (var m in await Only(db.CourseMaterials.AsNoTracking().Where(m => holdIds.Contains(m.HoldId)
            && (manageable.Contains(m.HoldId) || m.Published)), "material", m => m.Id).Select(m => new { m.Id, m.HoldId, m.Title }).ToArrayAsync(ct))
            Add("material", m.Id, m.Title, $"/holds/{m.HoldId:D}?view=materials&material={m.Id:D}", m.HoldId);
        if (!contentOnly)
        foreach (var p in await Only(db.PollRounds.AsNoTracking().Where(p => holdIds.Contains(p.HoldId)
            && (manageable.Contains(p.HoldId) || p.State != "draft")), "poll", p => p.Id)
            .Select(p => new { p.Id, p.HoldId, p.Question }).ToArrayAsync(ct))
            Add("poll", p.Id, p.Question, $"/polls/{p.Id:D}?hold={p.HoldId:D}", p.HoldId);
        foreach (var g in await Only(db.LearningGroups.AsNoTracking().Where(g => holdIds.Contains(g.HoldId)
            && (manageable.Contains(g.HoldId) || g.Members.Any(m => m.Membership.UserSub == sub))), "group", g => g.Id)
            .Select(g => new { g.Id, g.HoldId, g.Name }).ToArrayAsync(ct))
            Add("group", g.Id, g.Name, $"/holds/{g.HoldId:D}?view=groups&group={g.Id:D}", g.HoldId);
        foreach (var e in await Only(db.HoldCalendarEvents.AsNoTracking().Where(e => holdIds.Contains(e.HoldId)), "event", e => e.Id)
            .Select(e => new { e.Id, e.HoldId, e.Title }).ToArrayAsync(ct))
            Add("event", e.Id, e.Title, $"/holds/{e.HoldId:D}?view=calendar&event={e.Id:D}", e.HoldId);

        var files = new FileAuthorization(db);
        // Narrow candidates in SQL before the full existing policy (which also checks historical
        // participants and current quest audiences). No unrelated uploads are enumerated per request.
        var fileQuery = Only(db.FileAssets.AsNoTracking(), "file", f => f.Id).Where(f =>
            (f.HoldId == null || holdIds.Contains(f.HoldId.Value)) && (f.OwnerSub == sub
                || f.HoldId != null && manageable.Contains(f.HoldId.Value)
                || db.CourseMaterials.Any(m => m.FileAssetId == f.Id && holdIds.Contains(m.HoldId))
                || db.Set<SubmissionDraftFile>().Any(d => d.FileAssetId == f.Id && d.Submission.GroupId != null
                    && db.LearningGroupMembers.Any(g => g.GroupId == d.Submission.GroupId && g.Membership.UserSub == sub))
                || db.Set<SubmissionRevisionFile>().Any(r => r.FileAssetId == f.Id && r.Revision.Participants.Any(p => p.UserSub == sub
                    || p.UserSub == null && db.Memberships.Any(m => m.Id == p.MembershipId && m.UserSub == sub)))
                || db.Set<SubmissionFeedbackFile>().Any(a => a.FileAssetId == f.Id && a.Feedback.Revision.Participants.Any(p => p.UserSub == sub
                    || p.UserSub == null && db.Memberships.Any(m => m.Id == p.MembershipId && m.UserSub == sub)))));
        if (!string.IsNullOrWhiteSpace(search))
        {
            foreach (var word in search.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var term = word.ToLowerInvariant();
                var matchingHolds = holds.Where(h => h.Name.Contains(word, StringComparison.CurrentCultureIgnoreCase)).Select(h => h.Id).ToArray();
                var kindMatches = "fil".Contains(term);
                fileQuery = fileQuery.Where(f => kindMatches || f.OriginalName.ToLower().Contains(term) || f.HoldId != null && matchingHolds.Contains(f.HoldId.Value));
            }
        }
        foreach (var file in await fileQuery.ToArrayAsync(ct))
            if (await files.CanReadAsync(file, user, ct)) Add("file", file.Id, file.OriginalName,
                $"/references/file/file/{file.Id:D}", file.HoldId, context: "Fil · " + HoldName(file.HoldId));
        foreach (var asset in await Only(db.NotionAssets.AsNoTracking().Where(a => readablePageIds.Contains(a.PageId)), "notion-asset", a => a.Id).Select(a => new { a.Id, a.PageId, a.OriginalName }).ToArrayAsync(ct))
            if (pageAccess.TryGetValue(asset.PageId, out var p))
            {
                var edit = !publishedOnly && (p.HoldId is Guid h ? manageSet.Contains(h) : UserClaims.IsTeacher(user));
                if (edit || ContainsId(p.PublishedAssetIdsJson, asset.Id)) Add("notion-asset", asset.Id, asset.OriginalName,
                    $"/references/file/notion-asset/{asset.Id:D}", p.HoldId, p.CollectionId, context: "Vedhæftning · " + HoldName(p.HoldId));
            }
        foreach (var asset in await Only(db.NotebookAssets.AsNoTracking().Where(a => readableNoteIds.Contains(a.DocumentId)), "notebook-asset", a => a.Id).Select(a => new { a.Id, a.DocumentId, a.OriginalName }).ToArrayAsync(ct))
            if (noteAccess.TryGetValue(asset.DocumentId, out var d) && (d.OwnerSub == sub || ContainsId(d.AssetIdsJson, asset.Id)))
                Add("notebook-asset", asset.Id, asset.OriginalName, $"/references/file/notebook-asset/{asset.Id:D}", d.HoldId, d.NotebookId, context: "Vedhæftning · Personlige noter");
        foreach (var drawing in await Only(db.Drawings.AsNoTracking().Where(d => d.NotionPageId != null && readablePageIds.Contains(d.NotionPageId.Value)
            || d.NotebookDocumentId != null && readableNoteIds.Contains(d.NotebookDocumentId.Value)), "drawing", d => d.Id)
            .Select(d => new { d.Id, d.NotionPageId, d.NotebookDocumentId, d.BlockId }).ToArrayAsync(ct))
        {
            if (drawing.NotionPageId is Guid page && pageAccess.TryGetValue(page, out var p))
            {
                var edit = !publishedOnly && (p.HoldId is Guid h ? manageSet.Contains(h) : UserClaims.IsTeacher(user));
                if (edit || DrawingManifestContains(p.PublishedDrawingsJson, drawing.Id)) Add("drawing", drawing.Id, "Tegning · " + (edit ? p.Title : p.PublishedTitle),
                    $"/notion/{page:D}?drawing={drawing.Id:D}" + (edit ? "" : "&published=true"), p.HoldId, p.CollectionId);
            }
            else if (drawing.NotebookDocumentId is Guid note && noteAccess.TryGetValue(note, out var d))
                Add("drawing", drawing.Id, "Tegning · " + d.Title, $"/notebook/{note:D}?drawing={drawing.Id:D}", d.HoldId, d.NotebookId);
        }
        await AddSubmissionsAsync(result, user, quests.Select(q => q.Id).ToArray(), manageable, wanted is null ? null : Ids("submission"), ct);
        AddPublic(result);
        return result.Where(r => (!contentOnly || IsGraphContent(r.Reference.Kind)) && (wanted is null || wanted.Contains(r.Reference))).DistinctBy(r => r.Reference).ToList();
    }

    private static bool DrawingManifestContains(string json, Guid id)
    {
        try { return (JsonSerializer.Deserialize<DrawingPublicationBinding[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []).Any(d => d.DrawingId == id); }
        catch (JsonException) { return false; }
    }

    private async Task AddSubmissionsAsync(List<ResolvedReference> result, ClaimsPrincipal user, Guid[] questIds, Guid[] manageable, Guid[]? wanted, CancellationToken ct)
    {
        var sub = UserClaims.GetSub(user)!.Value;
        var memberIds = await db.Memberships.Where(m => m.UserSub == sub).Select(m => m.Id).ToArrayAsync(ct);
        var candidates = await db.QuestSubmissions.AsNoTracking().Where(s => questIds.Contains(s.QuestId) && (wanted == null || wanted.Contains(s.Id)))
            .Select(s => new { s.Id, s.QuestId, s.Quest.HoldId, s.Quest.Title, s.CreatedBySub, s.GroupId, s.State,
                Participant = s.Revisions.Any(r => r.Participants.Any(p => p.UserSub == sub || p.UserSub == null && memberIds.Contains(p.MembershipId))),
                HasRevisions = s.Revisions.Any(), LatestParticipant = s.Revisions.OrderByDescending(r => r.Number).Take(1)
                    .Any(r => r.Participants.Any(p => p.UserSub == sub || p.UserSub == null && memberIds.Contains(p.MembershipId))) }).ToArrayAsync(ct);
        var audience = new QuestAudienceAccess(db);
        foreach (var s in candidates)
        {
            var manage = manageable.Contains(s.HoldId);
            var quest = await db.LearningQuests.AsNoTracking().SingleAsync(q => q.Id == s.QuestId, ct);
            var edit = !UserClaims.IsTeacher(user) && s.State is "draft" or "changes-requested" && quest.Published
                && await audience.CanParticipateAsync(quest, sub, s.GroupId, ct)
                && (s.GroupId is Guid group ? await db.LearningGroupMembers.AnyAsync(m => m.GroupId == group && m.Membership.UserSub == sub, ct)
                    && (!s.HasRevisions || s.LatestParticipant)
                    : s.CreatedBySub == sub);
            if (!manage && !s.Participant && !edit) continue;
            result.Add(new(Ref("submission", s.Id), "Aflevering · " + s.Title,
                $"/quests/{s.QuestId:D}?hold={s.HoldId:D}&submission={s.Id:D}", "Aflevering", s.HoldId));
        }
    }

    private static void AddPublic(List<ResolvedReference> result)
    {
        void Add(string kind, string id, string title, string href, string context) => result.Add(new(new(kind, id), title, href, context));
        foreach (var c in CourseTemplateCatalog.All) Add("learning-plan", c.Slug, c.Short + " · " + c.Title, c.Path, "Læringsplan · Bibliotek");
        foreach (var s in PublicCatalog.Subjects)
        {
            Add("subject", s.Id, s.Name, s.Path, "Fag · Bibliotek");
            foreach (var l in PublicCatalog.Lessons(s)) Add("lesson", s.Id + "/" + l.Id, l.Name, s.Path + "/" + l.Id, s.Name + " · Emne");
        }
        foreach (var p in PublicCatalog.Projects) Add("project", p.Id, p.Name, p.Path, "Eksempelprojekt · Bibliotek");
        foreach (var b in PublicCatalog.Badges) Add("badge", b.Id, b.Name, "/badges/" + b.Id, "Badgekrav · Bibliotek");
        foreach (var c in CharacterCatalog.All) Add("character", c.Slug, c.Name, c.Path, "Karakter · Bibliotek");
    }
}
