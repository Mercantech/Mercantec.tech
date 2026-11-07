using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Tests;

public sealed class RouletteNameCatalogTests
{
    [Fact]
    public void EveryGeneratedNameUsesTwoReusableClips()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var assets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var first in RouletteNameCatalog.Starts)
            foreach (var last in RouletteNameCatalog.Ends)
            {
                Assert.True(names.Add(first + last));
                var clips = Assert.IsType<string[]>(RouletteNameCatalog.GetClipUrls(first + last));
                Assert.Equal(2, clips.Length);
                Assert.StartsWith(RouletteNameCatalog.AssetRoot + "/prefix-", clips[0]);
                Assert.StartsWith(RouletteNameCatalog.AssetRoot + "/suffix-", clips[1]);
                assets.UnionWith(clips);
            }
        Assert.Equal(576, names.Count);
        Assert.Equal(48, assets.Count);
    }

    [Theory]
    [InlineData("NeonOpfinderne", "prefix-16.wav", "suffix-04.wav")]
    [InlineData("BølgeVaskebjørnene", "prefix-08.wav", "suffix-21.wav")]
    [InlineData("PopcornEnhjørningerne", "prefix-23.wav", "suffix-23.wav")]
    public void KnownNamesResolveToTheirPronouncedWordParts(string name, string first, string last)
    {
        Assert.Equal([RouletteNameCatalog.AssetRoot + "/" + first, RouletteNameCatalog.AssetRoot + "/" + last],
            Assert.IsType<string[]>(RouletteNameCatalog.GetClipUrls(name)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Neon")]
    [InlineData("NeonOpfinderne igen")]
    [InlineData("neonOpfinderne")]
    [InlineData("Neon../../elevnavn")]
    [InlineData("Mit eget holdnavn")]
    public void CustomNamesNeverBecomeAssetPaths(string? name)
        => Assert.Null(RouletteNameCatalog.GetClipUrls(name));
}
