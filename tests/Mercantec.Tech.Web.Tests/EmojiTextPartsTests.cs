using Mercantec.Tech.Web.Emojis;

namespace Mercantec.Tech.Web.Tests;

public sealed class EmojiTextPartsTests
{
    private static readonly CustomEmojiItem Lup = new(Guid.NewGuid(), "lup", "image/png", 12, DateTimeOffset.UtcNow, "/api/emojis/x");

    [Fact]
    public void KnownShortcodesBecomeImagesAndUnknownTextStays()
    {
        var parts = EmojiTextParts.Split("Start :lup: og :mangler: slut", [Lup]);
        Assert.Equal(3, parts.Count);
        Assert.Equal("Start ", parts[0].Text);
        Assert.Null(parts[0].Emoji);
        Assert.Equal(Lup, parts[1].Emoji);
        Assert.Equal(" og :mangler: slut", parts[2].Text);
    }

    [Fact]
    public void ShortcodeInsideAWordIsLeftAlone()
    {
        var parts = EmojiTextParts.Split("se:lup:ikke", [Lup]);
        Assert.Null(Assert.Single(parts).Emoji);
        Assert.Equal("se:lup:ikke", parts[0].Text);
    }

    [Fact]
    public void HyphenatedAndUnderscoredShortcodesWorkTogetherAndUnknownNamesStayText()
    {
        var vatson = Lup with { Name = "vatson-vinker" };
        var legacy = Lup with { Id = Guid.NewGuid(), Name = "thumbs_up" };
        var parts = EmojiTextParts.Split("Hej :Vatson-Vinker: :thumbs_up: :vatson-mangler:", [vatson, legacy]);
        Assert.Equal(new[] { vatson, legacy }, parts.Where(part => part.Emoji is not null).Select(part => part.Emoji));
        Assert.Equal(" :vatson-mangler:", parts[^1].Text);
        Assert.Equal(":vatson-vinker:", EmojiInsert.Append("", vatson.Name, 100));
    }

    [Fact]
    public void AppendStopsAtTheLengthLimit()
    {
        Assert.Equal(":lup:", EmojiInsert.Append("  ", "lup", 20));
        Assert.Equal("hej :lup:", EmojiInsert.Append("hej", "lup", 20));
        Assert.Equal("for lang", EmojiInsert.Append("for lang", "lup", 8));
    }
}
