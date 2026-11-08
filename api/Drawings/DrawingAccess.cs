using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Drawings;

public sealed class DrawingAccess(AppDbContext db, CourseAccess courses, NotebookAccess notebooks)
{
    private readonly NotionAccess teachingPages = new(db, courses);
    public async Task<bool> CanReadParentAsync(Drawing drawing, ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is null) return false;
        if (drawing.NotebookDocumentId is Guid notebook) return await notebooks.CanReadAsync(notebook, user, ct);
        return drawing.NotionPageId is Guid page && await teachingPages.CanReadAsync(page, user, ct);
    }

    public async Task<bool> CanManageAsync(Drawing drawing, ClaimsPrincipal user, CancellationToken ct)
    {
        if (drawing.NotebookDocumentId is Guid notebook) return await notebooks.CanEditAsync(notebook, user, ct);
        return drawing.NotionPageId is Guid page && await teachingPages.CanManageAsync(page, user, ct);
    }

    public async Task<bool> CanEditAsync(Drawing drawing, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanReadParentAsync(drawing, user, ct)) return false;
        if (await CanManageAsync(drawing, user, ct)) return true;
        // A page reader's grant must never expose a new draft drawing or the head behind a frozen publication.
        if (drawing.NotionPageId is not null && (await PublishedBindingAsync(drawing, ct))?.Mode != "live") return false;
        if (UserClaims.GetSub(user) is not Guid sub || !await IsReferencedAsync(drawing, ct)) return false;
        return await db.DrawingGrants.AsNoTracking().AnyAsync(g => g.DrawingId == drawing.Id
            && (g.UserSub == sub || g.HoldId != null && db.Memberships.Any(m => m.HoldId == g.HoldId && m.UserSub == sub)), ct);
    }

    public async Task<bool> CanReadHeadAsync(Drawing drawing, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanReadParentAsync(drawing, user, ct)) return false;
        return drawing.NotebookDocumentId is not null || await CanEditAsync(drawing, user, ct)
            || (await PublishedBindingAsync(drawing, ct))?.Mode == "live";
    }

    public async Task<DrawingPublicationBinding?> PublishedBindingAsync(Drawing drawing, CancellationToken ct)
    {
        if (drawing.NotionPageId is not Guid id) return null;
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { HasPublication = p.PublishedAtUtc != null && p.PublishedState != null, p.PublishedDrawingsJson }).SingleOrDefaultAsync(ct);
        if (page?.HasPublication != true) return null;
        return Bindings(page.PublishedDrawingsJson).SingleOrDefault(b => b.DrawingId == drawing.Id && b.BlockId == drawing.BlockId);
    }

    public async Task<bool> CanReadAssetAsync(Drawing drawing, DrawingAsset asset, ClaimsPrincipal user, CancellationToken ct)
    {
        if (asset.DrawingId != drawing.Id || !await CanReadParentAsync(drawing, user, ct)) return false;
        if (await CanEditAsync(drawing, user, ct)) return true;
        var freshPreview = drawing.PreviewFingerprint == drawing.Fingerprint && drawing.PreviewRevision == drawing.Revision;
        if (await CanReadHeadAsync(drawing, user, ct)
            && (Ids(drawing.AssetIdsJson).Contains(asset.Id) || freshPreview && (drawing.PreviewLightAssetId == asset.Id || drawing.PreviewDarkAssetId == asset.Id))) return true;
        var binding = await PublishedBindingAsync(drawing, ct);
        if (binding?.Mode != "snapshot" || binding.SnapshotId is not Guid snapshotId) return false;
        var snapshot = await db.DrawingSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == snapshotId && s.DrawingId == drawing.Id, ct);
        return snapshot is not null && (Ids(snapshot.AssetIdsJson).Contains(asset.Id)
            || snapshot.PreviewLightAssetId == asset.Id || snapshot.PreviewDarkAssetId == asset.Id);
    }

    public async Task<bool> IsReferencedAsync(Drawing drawing, CancellationToken ct)
    {
        string? refs;
        if (drawing.NotionPageId is Guid page)
        {
            refs = await db.NotionPages.Where(p => p.Id == page).Select(p => p.DrawingIdsJson).SingleOrDefaultAsync(ct);
            if (Ids(refs).Contains(drawing.Id)) return true;
            return await PublishedBindingAsync(drawing, ct) is not null;
        }
        refs = await db.NotebookDocuments.Where(p => p.Id == drawing.NotebookDocumentId).Select(p => p.DrawingIdsJson).SingleOrDefaultAsync(ct);
        return Ids(refs).Contains(drawing.Id);
    }

    public async Task<DrawingCandidates> CandidatesAsync(Drawing drawing, ClaimsPrincipal manager, string? search, CancellationToken ct)
    {
        var query = search?.Trim() ?? "";
        var profiles = await db.NotebookProfiles.AsNoTracking().Where(p => query == "" || p.DisplayName.Contains(query)).Take(150).ToArrayAsync(ct);
        var members = await db.Memberships.AsNoTracking().Where(m => m.UserSub != null
            && (query == "" || m.DisplayName != null && m.DisplayName.Contains(query)))
            .Select(m => new { Sub = m.UserSub!.Value, Name = m.DisplayName ?? "Bruger" }).Take(250).ToArrayAsync(ct);
        var people = new Dictionary<Guid, DrawingCandidate>();
        foreach (var person in profiles.Select(p => new { p.Sub, Name = p.DisplayName, p.IsTeacher })
            .Concat(members.Select(m => new { m.Sub, m.Name, IsTeacher = false })))
        {
            if (people.ContainsKey(person.Sub)) continue;
            var identity = CandidatePrincipal(person.Sub, person.IsTeacher);
            if (await CanReadParentAsync(drawing, identity, ct)) people[person.Sub] = new(person.Sub, person.Name);
        }
        var holds = await courses.AccessibleHolds(manager).AsNoTracking()
            .Where(h => query == "" || h.Name.Contains(query)).Select(h => new DrawingCandidate(h.Id, h.Name)).Take(100).ToArrayAsync(ct);
        var eligibleHolds = new List<DrawingCandidate>();
        foreach (var hold in holds)
        {
            var subs = await db.Memberships.Where(m => m.HoldId == hold.Id && m.UserSub != null).Select(m => m.UserSub!.Value).Distinct().ToArrayAsync(ct);
            foreach (var sub in subs)
                if (await CanReadParentAsync(drawing, CandidatePrincipal(sub, false), ct)) { eligibleHolds.Add(hold); break; }
        }
        return new(people.Values.OrderBy(p => p.DisplayName).Take(100).ToArray(), eligibleHolds.ToArray());
    }

    public static ClaimsPrincipal CandidatePrincipal(Guid sub, bool teacher) => new(new ClaimsIdentity(
        teacher ? [new Claim("sub", sub.ToString()), new Claim(ClaimTypes.Role, "Underviser")] : [new Claim("sub", sub.ToString())], "drawing-candidate"));
    public static Guid[] Ids(string? json) { try { return JsonSerializer.Deserialize<Guid[]>(json ?? "[]") ?? []; } catch (JsonException) { return []; } }
    public static DrawingPublicationBinding[] Bindings(string? json) { try { return JsonSerializer.Deserialize<DrawingPublicationBinding[]>(json ?? "[]", new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; } catch (JsonException) { return []; } }
}
