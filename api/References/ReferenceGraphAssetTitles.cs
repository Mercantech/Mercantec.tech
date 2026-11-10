using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.References;

public sealed partial class ReferenceService
{
    private async Task ApplyGraphAssetTitlesAsync(ReferenceGraphNode[] nodes, IReadOnlyList<ResolvedReference> catalog,
        CancellationToken ct)
    {
        var titles = new Dictionary<EntityReference, string>();
        Guid[] VisibleAssetIds(string kind) => nodes.Where(n => n.Entity.Reference.Kind == kind)
            .Select(n => Guid.Parse(n.Entity.Reference.Id)).ToArray();

        // Read snapshots only for assets in the already permission-filtered graph slice.
        // Group by the actual asset owner to avoid reading one large document per image.
        var notionIds = VisibleAssetIds("notion-asset");
        if (notionIds.Length > 0)
        {
            var assets = await db.NotionAssets.AsNoTracking().Where(a => notionIds.Contains(a.Id) && a.Kind == "image")
                .Select(a => new { a.PageId, a.Id }).ToArrayAsync(ct);
            var assetsByPage = assets.ToLookup(a => a.PageId, a => a.Id);
            var pageIds = assetsByPage.Select(group => group.Key).ToArray();
            // CanRename on the owner already reflects draft access and the request's PublishedOnly flag.
            var editablePages = catalog.Where(r => r.Reference.Kind == "notion-page" && r.CanRename)
                .Select(r => Guid.Parse(r.Reference.Id)).Where(id => assetsByPage.Contains(id)).ToArray();
            var pages = db.NotionPages.AsNoTracking().Where(p => pageIds.Contains(p.Id))
                .Select(p => new { p.Id, State = editablePages.Contains(p.Id) ? p.YjsState : p.PublishedState });
            await foreach (var page in pages.AsAsyncEnumerable().WithCancellation(ct))
                AddTitles(page.State, assetsByPage[page.Id], "notion-asset");
        }

        var notebookIds = VisibleAssetIds("notebook-asset");
        if (notebookIds.Length > 0)
        {
            var assets = await db.NotebookAssets.AsNoTracking().Where(a => notebookIds.Contains(a.Id) && a.Kind == "image")
                .Select(a => new { a.DocumentId, a.Id }).ToArrayAsync(ct);
            var assetsByDocument = assets.ToLookup(a => a.DocumentId, a => a.Id);
            var documentIds = assetsByDocument.Select(group => group.Key).ToArray();
            var documents = db.NotebookDocuments.AsNoTracking().Where(d => documentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.YjsState });
            await foreach (var document in documents.AsAsyncEnumerable().WithCancellation(ct))
                AddTitles(document.YjsState, assetsByDocument[document.Id], "notebook-asset");
        }

        for (var i = 0; i < nodes.Length; i++)
            if (titles.TryGetValue(nodes[i].Entity.Reference, out var title))
                nodes[i] = nodes[i] with { Entity = nodes[i].Entity with { Title = title } };

        void AddTitles(byte[]? state, IEnumerable<Guid> assets, string kind)
        {
            var documentTitles = YjsPageLinks.ReadImageTitles(state);
            foreach (var asset in assets)
                if (documentTitles.TryGetValue(asset, out var title)) titles.Add(Ref(kind, asset), title);
        }
    }
}
