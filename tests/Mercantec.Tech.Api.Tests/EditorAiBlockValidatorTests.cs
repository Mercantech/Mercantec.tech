using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class EditorAiBlockValidatorTests
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement[] Validate(object value, Dictionary<string, JsonElement>? original = null, params EditorAiSource[] sources) =>
        EditorAiBlockValidator.ValidateBlocks(Json(value), original ?? [], sources);

    [Fact]
    public void LargeDocumentsCodeAndTablesArePreservedWithoutClipping()
    {
        var longText = new string('æ', 24000);
        var rows = Enumerable.Range(0, 32).Select(_ => new { cells = Enumerable.Range(0, 14).Select(_ => new[] { new { type = "text", text = longText[..300], styles = new { bold = true } } }).ToArray() }).ToArray();
        var blocks = Enumerable.Range(0, 45).Select(_ => (object)new { type = "paragraph", content = longText }).Concat([
            new { type = "codeBlock", props = new { language = "csharp" }, content = longText },
            new { type = "table", content = new { type = "tableContent", rows, headerRows = 1 } },
        ]).ToArray();
        var result = Validate(blocks);
        Assert.Equal(47, result.Length);
        Assert.Equal(longText, result[0].GetProperty("content").GetString());
        Assert.Equal(longText, result[45].GetProperty("content").GetString());
        Assert.Equal(32, result[46].GetProperty("content").GetProperty("rows").GetArrayLength());
        Assert.Equal(14, result[46].GetProperty("content").GetProperty("rows")[0].GetProperty("cells").GetArrayLength());
    }

    [Theory]
    [InlineData("drawing")]
    [InlineData("pageMedia")]
    [InlineData("holdPoll")]
    [InlineData("classRoster")]
    [InlineData("futureWidget")]
    public void OpaqueNativeBlocksCanOnlyBePreservedExactly(string type)
    {
        var block = Json(new { id = "native", type, props = new { drawingId = "sensitive-existing-reference" }, children = Array.Empty<object>() });
        Assert.Single(Validate(new[] { block }, new() { ["native"] = block }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { block }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { id = "native", type, props = new { drawingId = "new-reference" } } }, new() { ["native"] = block }));
    }

    [Fact]
    public void ReferencesRequireServerSourcesButExistingMentionsRemainIntact()
    {
        var original = Json(new { id = "p", type = "paragraph", content = new[] { new { type = "entityMention", props = new { kind = "course", id = "course-1" } } } });
        var rewritten = new { id = "p", type = "paragraph", content = new object[] { new { type = "text", text = "Se ", styles = new { italic = true } }, original.GetProperty("content")[0] } };
        Assert.Single(Validate(new[] { rewritten }, new() { ["p"] = original }));
        var newLink = new { type = "pageLink", props = new { pageId = "page-1" } };
        Assert.Throws<ArgumentException>(() => Validate(new[] { newLink }));
        Assert.Single(Validate(new[] { newLink }, null, new EditorAiSource("notion-page:page-1", "notion-page", "page-1", "Tilladt side", "/notion/page-1")));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,evil")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public void UnsafeLinksRejectWholeProposal(string href)
    {
        var blocks = new[] { new { type = "paragraph", content = new[] { new { type = "link", href, content = new[] { new { type = "text", text = "Klik" } } } } } };
        Assert.Throws<ArgumentException>(() => Validate(blocks));
    }

    [Fact]
    public void MalformedTablesAndUnknownFormattingFailExplicitly()
    {
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "table", content = new { type = "tableContent", rows = new[] { new { cells = new[] { "a", "b" } }, new { cells = new[] { "c" } } } } } }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "paragraph", content = new[] { new { type = "text", text = "Hej", styles = new { onClick = "evil" } } } } }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "heading", props = new { level = 7 }, content = "Hej" } }));
    }

    [Fact]
    public void CanonicalFingerprintIgnoresPropertyOrderButNotContent()
    {
        var a = JsonDocument.Parse("{\"props\":{\"bold\":true},\"content\":\"æøå\"}").RootElement;
        var b = JsonDocument.Parse("{\"content\":\"æøå\",\"props\":{\"bold\":true}}").RootElement;
        Assert.Equal(EditorAiBlockValidator.Fingerprint(a), EditorAiBlockValidator.Fingerprint(b));
        Assert.NotEqual(EditorAiBlockValidator.Fingerprint(a), EditorAiBlockValidator.Fingerprint(Json(new { content = "ændret" })));
        Assert.Throws<ArgumentException>(() => EditorAiBlockValidator.Fingerprint(JsonDocument.Parse("{\"type\":\"a\",\"type\":\"b\"}").RootElement));
    }

    [Fact]
    public void CalloutEmojiCannotBeFabricatedWhileExistingEmojiSurvivesRewriting()
    {
        var block = Json(new { id = "tip", type = "callout", props = new { tone = "tip", emojiId = "emoji-existing" }, content = "Gammel tekst" });
        var rewritten = new { id = "tip", type = "callout", props = new { tone = "tip", emojiId = "emoji-existing" }, content = "Ny tekst" };
        Assert.Single(Validate(new[] { rewritten }, new() { ["tip"] = block }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "callout", props = new { emojiId = "unvalidated" }, content = "Tip" } }));
    }

    [Fact]
    public void UnsupportedImportantPropertiesAreRejectedInsteadOfSilentlyLost()
    {
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "paragraph", content = "Bevar alt", caption = "Vigtig ekstra tekst" } }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "paragraph", content = "Bevar alt", props = new { footnote = "Vigtig kilde" } } }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "paragraph", content = new[] { new { type = "text", text = "Fed", styles = new { bold = "yes" } } } } }));
        Assert.Throws<ArgumentException>(() => Validate(new[] { new { type = "paragraph", content = "Hej", props = new { textAlignment = "diagonal" } } }));
    }
}
