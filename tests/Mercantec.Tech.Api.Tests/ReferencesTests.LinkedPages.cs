using System.Text.Json;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class ReferencesTests
{
    [Fact]
    public async Task LinkedPageModeFiltersBeforePaginationAndRespectsScopeAndPublication()
    {
        var isolated = Page("Uden sidelinks", hold);
        var file = new NotionAsset
        {
            Id = Guid.NewGuid(), PageId = isolated.Id, OriginalName = "diagram.png",
            Key = "test/figure", Kind = "image", ContentType = "image/png", UploadedBySub = teacher
        };
        isolated.PublishedAssetIdsJson = JsonSerializer.Serialize(new[] { file.Id });
        db.NotionAssets.Add(file);

        var source = Ref("notion-page", page.Id);
        var target = Ref("notion-page", theory.Id);
        var draftTarget = Ref("notion-page", draft.Id);
        var unlinked = Ref("notion-page", isolated.Id);
        var fileRef = Ref("notion-asset", file.Id);
        Link(source, target, "published");
        Link(source, draftTarget, "published");
        Link(source, draftTarget, "draft");
        Link(unlinked, unlinked, "published");
        Link(unlinked, fileRef, "published");
        await db.SaveChangesAsync();

        var request = new ReferenceGraphRequest(ScopeKind: "hold", ScopeId: hold.ToString("D"), LinkedPagesOnly: true);
        var studentView = await service.GraphAsync(User(student), request, default);
        Assert.Equal(2, studentView.Total);
        Assert.Equal([source.Key, target.Key], studentView.Nodes.Select(n => n.Key));
        Assert.Equal(new ReferenceGraphEdge(source.Key, target.Key), Assert.Single(studentView.Edges));

        var slice = await service.GraphAsync(User(student), request with { Limit = 1 }, default);
        Assert.Equal(2, slice.Total);
        Assert.True(slice.HasMore);
        Assert.Equal(source, Assert.Single(slice.Nodes).Entity.Reference);
        Assert.Empty((await service.GraphAsync(User(student), request with { Focus = unlinked }, default)).Nodes);

        var draftView = await service.GraphAsync(User(teacher, true), request, default);
        Assert.Equal(new ReferenceGraphEdge(source.Key, draftTarget.Key), Assert.Single(draftView.Edges));
        var preview = await service.GraphAsync(User(teacher, true), request with { PublishedOnly = true }, default);
        Assert.Equal(new ReferenceGraphEdge(source.Key, target.Key), Assert.Single(preview.Edges));

        var search = await service.SearchAsync(User(student), "", null, null, 0, 30, false, default, linkedPagesOnly: true);
        Assert.Equal(2, search.Total);
        Assert.True(new[] { source, target }.ToHashSet().SetEquals(search.Items.Select(item => item.Reference)));

        var full = await service.GraphAsync(User(student), request with { LinkedPagesOnly = false }, default);
        Assert.Contains(full.Nodes, node => node.Entity.Reference == fileRef);
        Assert.Contains(full.Nodes, node => node.Entity.Reference == unlinked);

        void Link(EntityReference from, EntityReference to, string visibility) => db.EntityReferenceLinks.Add(new()
        {
            SourceKind = from.Kind, SourceId = from.Id, TargetKind = to.Kind, TargetId = to.Id, Visibility = visibility
        });
    }
}
