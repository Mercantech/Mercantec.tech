using Mercantec.Tech.Web.Emojis;

namespace Mercantec.Tech.Web.Tests;

public sealed class EmojiPickerCatalogTests
{
    [Fact]
    public void UploadedEmojiCanBeFoundBySemanticCategoryAndAltText()
    {
        var own = Item("vores_figur", false, "technology") with { AltText = "En lilla robot, der skriver kode" };
        Assert.Equal(own, Assert.Single(EmojiPickerCatalog.Filter([own], "robot kode", "technology")));
        Assert.Equal(own, Assert.Single(EmojiPickerCatalog.Filter([own], "robot", "custom")));
        Assert.Empty(EmojiPickerCatalog.Filter([own], "", "gestures"));
    }

    private static CustomEmojiItem Item(string name, bool standard = false, string category = "custom", params string[] keywords) =>
        new(Guid.NewGuid(), name, "image/png", 20, DateTimeOffset.UtcNow, "/image.png", standard, category, keywords);

    [Fact]
    public void SearchCombinesDanishAliasesShortcodesAndCategory()
    {
        var up = Item("thumbs_up", true, "gestures", "tommel op", "godt", "👍");
        var book = Item("book", true, "learning", "bog", "læse", "idé");
        CustomEmojiItem[] items = [up, book];
        Assert.Equal(up, Assert.Single(EmojiPickerCatalog.Filter(items, " :THUMBS_UP: ", "all")));
        Assert.Equal(up, Assert.Single(EmojiPickerCatalog.Filter(items, "tommel godt", "gestures")));
        Assert.Empty(EmojiPickerCatalog.Filter(items, "tommel", "learning"));
        Assert.Equal(book, Assert.Single(EmojiPickerCatalog.Filter(items, "LAESE ide", "all")));
        Assert.Equal(up, Assert.Single(EmojiPickerCatalog.Filter(items, "👍", "all")));
    }

    [Fact]
    public void OwnAndStandardCategoriesFollowMetadataRatherThanNames()
    {
        var standard = Item("omdoebt", true, "smileys", "glad");
        var own = Item("smile");
        var ownSecond = Item("hold_logo");
        CustomEmojiItem[] items = [standard, own, ownSecond];
        Assert.Equal([own, ownSecond], EmojiPickerCatalog.Filter(items, "", "custom"));
        Assert.Equal(standard, Assert.Single(EmojiPickerCatalog.Filter(items, "glad", "smileys")));
        Assert.Equal(ownSecond, Assert.Single(EmojiPickerCatalog.Filter(items, "hold logo", "custom")));
        Assert.Empty(EmojiPickerCatalog.Filter(items, "ikke fundet", "all"));
        Assert.Equal(3, EmojiPickerCatalog.Filter(items, "::", "all").Count);
    }
}
