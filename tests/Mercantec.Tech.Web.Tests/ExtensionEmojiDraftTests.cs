using Mercantec.Tech.Web.Emojis;

namespace Mercantec.Tech.Web.Tests;

public sealed class ExtensionEmojiDraftTests
{
    [Theory]
    [InlineData("emojis?lupImport=45ff7a28-2ba1-46e0-965e-3ff85ae46da1", true)]
    [InlineData("emojis?other=1&lupImport=45ff7a28-2ba1-46e0-965e-3ff85ae46da1#form", true)]
    [InlineData("emojis", false)]
    [InlineData("emojis?lupImport=bad", false)]
    [InlineData("emojis?lupImport=45ff7a28-2ba1-46e0-965e-3ff85ae46da1&lupImport=bad", false)]
    [InlineData("teacher?lupImport=45ff7a28-2ba1-46e0-965e-3ff85ae46da1", false)]
    [InlineData("emojis/other?lupImport=45ff7a28-2ba1-46e0-965e-3ff85ae46da1", false)]
    public void AnonymousAccessOnlyShowsTheExplicitImportLoginPrompt(string path, bool expected) =>
        Assert.Equal(expected, ExtensionEmojiDraft.IsImportPath(path));

    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 1];

    [Fact]
    public async Task ImportedBytesUseTheExistingBrowserFileContractWithoutAUrl()
    {
        var draft = new ExtensionEmojiDraft("Sød kat", "image/png", Convert.ToBase64String(Png));
        var file = Assert.IsAssignableFrom<Microsoft.AspNetCore.Components.Forms.IBrowserFile>(draft.ToBrowserFile());
        Assert.Equal("sod_kat.png", file.Name);
        Assert.Equal("image/png", file.ContentType);
        await using var stream = file.OpenReadStream(EmojiClient.MaxBytes);
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        Assert.Equal(Png, output.ToArray());
        Assert.Throws<IOException>(() => file.OpenReadStream(1));
        Assert.Throws<OperationCanceledException>(() => file.OpenReadStream(cancellationToken: new CancellationToken(true)));
    }

    [Theory]
    [InlineData("image/svg+xml", "PHN2Zz48L3N2Zz4=")]
    [InlineData("image/png", "R0lGODlh")]
    [InlineData("image/jpeg", "not-base64!")]
    [InlineData("text/html", "PGh0bWw+")]
    [InlineData("image/gif", "")]
    public void RejectsUnsupportedAndMislabeledPayloads(string type, string payload) =>
        Assert.Null(new ExtensionEmojiDraft("test", type, payload).ToBrowserFile());

    [Fact]
    public void EnforcesDecodedSizeAsWellAsBase64Length()
    {
        var bytes = new byte[EmojiClient.MaxBytes];
        Png.CopyTo(bytes, 0);
        Assert.NotNull(new ExtensionEmojiDraft("test", "image/png", Convert.ToBase64String(bytes)).ToBrowserFile());
        Assert.Null(new ExtensionEmojiDraft("test", "image/png", Convert.ToBase64String([.. bytes, 0])).ToBrowserFile());
        Assert.Null(new ExtensionEmojiDraft("bad\nname", "image/png", Convert.ToBase64String(Png)).ToBrowserFile());
    }

    [Theory]
    [InlineData("image/gif", "R0lGODlh", ".gif")]
    [InlineData("image/jpeg", "/9j/", ".jpg")]
    [InlineData("image/webp", "UklGRgAAAABXRUJQ", ".webp")]
    public void FilenameExtensionComesFromValidatedMime(string type, string base64, string suffix)
    {
        var file = new ExtensionEmojiDraft("../../billede", type, base64).ToBrowserFile();
        Assert.NotNull(file);
        Assert.Equal("billede" + suffix, file.Name);
    }
}
