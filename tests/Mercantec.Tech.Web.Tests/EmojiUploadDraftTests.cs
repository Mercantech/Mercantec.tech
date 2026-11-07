using Mercantec.Tech.Web.Emojis;

namespace Mercantec.Tech.Web.Tests;

public sealed class EmojiUploadDraftTests
{
    [Fact]
    public void SearchTermsAreSplitAndDeduplicatedWithoutLosingDanish()
    {
        Assert.Equal(new[] { "glad", "lilla figur", "læring" }, EmojiUploadDraft.ParseKeywords(" glad, GLAD; lilla figur\nlæring "));
    }

    [Theory]
    [InlineData("Min Glade Ælling.png", "min_glade_aelling")]
    [InlineData("Håndtryk & øvelse.JPEG", "handtryk_ovelse")]
    [InlineData("C:\\fakepath\\Logo.v2.png", "logo_v2")]
    [InlineData("/folder/mit logo.webp", "mit_logo")]
    [InlineData("___Hej---  verden__.gif", "hej_verden")]
    [InlineData("🚀.png", "emoji")]
    [InlineData("a.png", "emoji")]
    [InlineData(".png", "emoji")]
    public void FilenameBecomesAValidEditableDefault(string fileName, string expected)
    {
        var suggested = EmojiUploadDraft.SuggestName(fileName, []);
        Assert.Equal(expected, suggested);
        Assert.True(EmojiUploadDraft.IsValidName(suggested));
    }

    [Fact]
    public void ExistingNamesGetASuffixWithinTheNameLimit()
    {
        Assert.Equal("smile_3", EmojiUploadDraft.SuggestName("Smile.png", ["SMILE", "smile_2"]));
        var full = new string('a', 32);
        var suggested = EmojiUploadDraft.SuggestName(full + ".png", [full]);
        Assert.Equal(new string('a', 30) + "_2", suggested);
        Assert.True(EmojiUploadDraft.IsValidName(suggested));
    }

    [Theory]
    [InlineData(":Mit_navn:", "mit_navn")]
    [InlineData(" ny_emoji ", "ny_emoji")]
    [InlineData("x", null)]
    [InlineData("to ord", null)]
    [InlineData("ælling", null)]
    [InlineData("", null)]
    public void OverrideFollowsTheExistingApiNameRules(string input, string? normalized) =>
        Assert.Equal(normalized, EmojiUploadDraft.NormalizeName(input));
}
