using System.Text.Json;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class ReferencesTests
{
    private static readonly Guid TitledFigure = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff");
    private static readonly Guid DraftFigure = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    // Real Yjs v1 snapshots containing nested pageMedia blocks with displayTitle attributes.
    private const string DraftFiguresState = "AQ8qAAcBDmRvY3VtZW50LXN0b3JlAwpibG9ja0dyb3VwBwAqAAMOYmxvY2tDb250YWluZXIoACoBAmlkAXcHYmxvY2stMAcAKgEDCXBhZ2VNZWRpYSgAKgMNc2NoZW1hVmVyc2lvbgF9ASgAKgMHYXNzZXRJZAF3JGZmZmZmZmZmLWZmZmYtNGZmZi04ZmZmLWZmZmZmZmZmZmZmZigAKgMEa2luZAF3BWltYWdlKAAqAwxkaXNwbGF5VGl0bGUBdxRGaWd1ciAxLjEg4oCTIEtsYWRkZYcqAQMOYmxvY2tDb250YWluZXIoACoIAmlkAXcHYmxvY2stMQcAKggDCXBhZ2VNZWRpYSgAKgoNc2NoZW1hVmVyc2lvbgF9ASgAKgoHYXNzZXRJZAF3JGFhYWFhYWFhLWFhYWEtNGFhYS04YWFhLWFhYWFhYWFhYWFhYSgAKgoEa2luZAF3BWltYWdlKAAqCgxkaXNwbGF5VGl0bGUBdxpGaWd1ciAxLjIg4oCTIElra2UgdWRnaXZldAA=";
    private const string PublishedFigureState = "AQgqAAcBDmRvY3VtZW50LXN0b3JlAwpibG9ja0dyb3VwBwAqAAMOYmxvY2tDb250YWluZXIoACoBAmlkAXcHYmxvY2stMAcAKgEDCXBhZ2VNZWRpYSgAKgMNc2NoZW1hVmVyc2lvbgF9ASgAKgMHYXNzZXRJZAF3JGZmZmZmZmZmLWZmZmYtNGZmZi04ZmZmLWZmZmZmZmZmZmZmZigAKgMEa2luZAF3BWltYWdlKAAqAwxkaXNwbGF5VGl0bGUBdxVGaWd1ciAxLjEg4oCTIFVkZ2l2ZXQA";

    [Fact]
    public async Task Graph_image_titles_respect_draft_and_published_visibility_and_legacy_names()
    {
        var legacy = Guid.NewGuid();
        db.NotionAssets.AddRange(
            Image(TitledFigure, "import.gif"), Image(DraftFigure, "privat.png"), Image(legacy, "aeldre.webp"));
        await db.SaveChangesAsync();
        await db.NotionPages.Where(p => p.Id == page.Id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.YjsState, State(DraftFiguresState))
            .SetProperty(p => p.PublishedState, State(PublishedFigureState))
            .SetProperty(p => p.PublishedAssetIdsJson, JsonSerializer.Serialize(new[] { TitledFigure, legacy })));

        var scope = new ReferenceGraphRequest(ScopeKind: "collection", ScopeId: page.Id.ToString("D"));
        var author = await service.GraphAsync(User(teacher, true), scope, default);
        Assert.Equal("Figur 1.1 – Kladde", AssetTitle(author, "notion-asset", TitledFigure));
        Assert.Equal("Figur 1.2 – Ikke udgivet", AssetTitle(author, "notion-asset", DraftFigure));
        Assert.Equal("aeldre.webp", AssetTitle(author, "notion-asset", legacy));

        var reader = await service.GraphAsync(User(student), scope, default);
        var preview = await service.GraphAsync(User(teacher, true), scope with { PublishedOnly = true }, default);
        foreach (var graph in new[] { reader, preview })
        {
            Assert.Equal("Figur 1.1 – Udgivet", AssetTitle(graph, "notion-asset", TitledFigure));
            Assert.Null(AssetTitle(graph, "notion-asset", DraftFigure));
            Assert.Equal("aeldre.webp", AssetTitle(graph, "notion-asset", legacy));
        }
        Assert.Empty((await service.GraphAsync(User(outsider), scope, default)).Nodes);

        NotionAsset Image(Guid id, string name) => new() { Id = id, PageId = page.Id, Key = "test/" + id,
            OriginalName = name, Kind = "image", ContentType = name.EndsWith(".gif", StringComparison.Ordinal) ? "image/gif" : "image/png" };
    }

    [Fact]
    public async Task Notebook_graph_uses_live_image_title_only_when_the_asset_is_readable()
    {
        db.NotebookAssets.Add(new() { Id = TitledFigure, DocumentId = noteId, Key = "test/notebook-figure",
            OriginalName = "import.png", Kind = "image", ContentType = "image/png" });
        await db.SaveChangesAsync();
        await db.NotebookDocuments.Where(d => d.Id == noteId).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.YjsState, State(PublishedFigureState))
            .SetProperty(d => d.AssetIdsJson, JsonSerializer.Serialize(new[] { TitledFigure })));

        var reference = Ref("notebook-asset", TitledFigure);
        var scope = new ReferenceGraphRequest(Focus: reference);
        var owner = await service.GraphAsync(User(student), scope, default);
        Assert.Equal("Figur 1.1 – Udgivet", AssetTitle(owner, reference.Kind, TitledFigure));
        Assert.Empty((await service.GraphAsync(User(outsider), scope, default)).Nodes);

        db.NotebookShares.Add(new() { NotebookId = notebook.Id, PageId = noteId, RecipientSub = outsider });
        await db.SaveChangesAsync();
        var shared = await service.GraphAsync(User(outsider), scope, default);
        Assert.Equal("Figur 1.1 – Udgivet", AssetTitle(shared, reference.Kind, TitledFigure));
    }

    private static string? AssetTitle(EntityGraph graph, string kind, Guid id) =>
        graph.Nodes.SingleOrDefault(n => n.Entity.Reference == Ref(kind, id))?.Entity.Title;
}
