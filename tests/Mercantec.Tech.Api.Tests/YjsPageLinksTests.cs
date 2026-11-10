using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class YjsPageLinksTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid C = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid D = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");

    [Fact]
    public void ReadsPageLinksAndMentionsAndIgnoresMediaIds()
    {
        var links = YjsPageLinks.Read(Bytes("basic"));
        Assert.Equal(new[] { B, C }.Order(), links.Order());
    }

    [Fact]
    public void DeletedMentionStaysInTheBinaryButIsNotAnEdge()
    {
        var bytes = Bytes("deleted");
        Assert.Contains("YmJiYmJi", Convert.ToBase64String(bytes));
        Assert.Equal(new[] { C }, YjsPageLinks.Read(bytes).Order());
    }

    [Fact]
    public void OverwrittenPageIdUsesOnlyTheLiveValue()
    {
        var bytes = Bytes("overwrite");
        Assert.Contains("YmJiYmJi", Convert.ToBase64String(bytes));
        Assert.Equal(new[] { C }, YjsPageLinks.Read(bytes).Order());
    }

    [Fact]
    public void MergedClientsKeepBothLiveLinks()
    {
        Assert.Equal(new[] { B, C }.Order(), YjsPageLinks.Read(Bytes("merged")).Order());
    }

    [Fact]
    public void NestedMentionsIncludeDuplicatesAsOneTargetAndANestedLink()
    {
        Assert.Equal(new[] { A, B, D }.Order(), YjsPageLinks.Read(Bytes("nested")).Order());
    }

    [Fact]
    public void OrdinaryHyperlinksRequireLiveVisibleTextAndCanonicalLupPageTargets()
    {
        // The fixture also contains external and deceptive URLs, a cleared format, deleted text and a blank link.
        var references = new HashSet<EntityReference>();
        var mentions = new HashSet<EntityReference>();

        Assert.True(YjsPageLinks.TryReadReferences(Bytes("hyperlinks"), "notebook-page", references, mentions));
        Assert.Equal(new EntityReference[]
        {
            new("notion-page", A.ToString("D")),
            new("notebook-page", B.ToString("D")),
            new("notion-page", C.ToString("D")),
            new("notion-page", D.ToString("D"))
        }.OrderBy(r => r.Id), references.OrderBy(r => r.Id));
        Assert.Empty(mentions);
    }

    [Fact]
    public void EmptyDocumentHasNoLinks() => Assert.Empty(YjsPageLinks.Read(Bytes("empty")));

    [Fact]
    public void CorruptStateDoesNotInventLinks()
    {
        var links = new HashSet<Guid> { A };
        Assert.False(YjsPageLinks.TryRead([1, 2, 3, 4, 5], links));
        Assert.Equal([A], links);
    }

    private static byte[] Bytes(string name) => GraphFixtures.Get(name);
}
