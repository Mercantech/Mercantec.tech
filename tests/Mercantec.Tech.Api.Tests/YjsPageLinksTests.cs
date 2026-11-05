using Mercantec.Tech.Api.Notion;

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
