using System.Text.Json;
using Mercantec.Tech.Api.Integrations.OpenAI;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class OpenAiEditorClientTests
{
    [Fact]
    public void EmojiCatalogueCannotBeDeserializedFromTheClient()
    {
        var request = JsonSerializer.Deserialize<EditorAiRequest>("""
            {"action":"generate","prompt":"Brug vores emoji","emojis":[
              {"id":"11111111-1111-4111-8111-111111111111","name":"forged","altText":"Falsk"}
            ]}
            """, JsonSerializerOptions.Web)!;
        Assert.Empty(request.Emojis);
        Assert.DoesNotContain("emojis", JsonSerializer.Serialize(request, JsonSerializerOptions.Web));
    }

    [Fact]
    public async Task CompleteAsyncSendsAuthoritativeEmojiContextWithoutImageTickets()
    {
        var id = Guid.NewGuid().ToString();
        using var transport = new CapturingTransport();
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://openai.test/v1/") };
        await Client(http).CompleteAsync(new("generate", "Brug en relevant emoji")
        {
            Emojis = [new(id, "vores_raket", "En lilla raket på vej op", "technology", ["rumfart", "start"])]
        }, default);

        using var payload = JsonDocument.Parse(transport.Body!);
        var prompt = payload.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!;
        Assert.Contains("Emoji-katalog", prompt);
        Assert.Contains(id, prompt);
        Assert.Contains("vores_raket", prompt);
        Assert.Contains("technology", prompt);
        Assert.Contains("rumfart", prompt);
        Assert.DoesNotContain("/api/emojis/", prompt);
        Assert.DoesNotContain("ticket=", prompt);
        Assert.Contains("customEmoji", OpenAiEditorClient.BuildSystemPrompt("generate"));
        Assert.DoesNotContain("Lav ikke customEmoji", OpenAiEditorClient.BlockCatalog);
    }

    [Fact]
    public void SanitizerKeepsKnownEmojiWithCurrentNameInParagraphsListsTablesAndLinks()
    {
        var id = Guid.NewGuid().ToString();
        var unknown = Guid.NewGuid().ToString();
        var catalog = EditorAiCatalog.From(new("generate", "Brug emoji")
        {
            Emojis = [new(id, "omdoebt_emoji", "Venlig lilla figur")]
        });
        var parts = new object[]
        {
            new { type = "text", text = "Hej ", styles = new { bold = true } },
            new { type = "customEmoji", props = new { emojiId = id, name = "gammelt_navn", src = "javascript:alert(1)" } },
            new { type = "customEmoji", props = new { emojiId = unknown, name = "forged" } },
            new { type = "customEmoji", props = new { emojiId = "not-a-guid", name = "broken" } }
        };
        var raw = new[]
        {
            new EditorAiBlock { Type = "paragraph", Content = JsonSerializer.SerializeToElement(parts) },
            new EditorAiBlock { Type = "bulletListItem", Content = JsonSerializer.SerializeToElement(parts) },
            new EditorAiBlock { Type = "table", Content = JsonSerializer.SerializeToElement(new
            {
                type = "tableContent", rows = new[] { new { cells = new object[] { parts } } }
            }) },
            new EditorAiBlock { Type = "paragraph", Content = JsonSerializer.SerializeToElement(new[]
            {
                new { type = "link", href = "https://example.test", content = parts }
            }) }
        };
        var blocks = OpenAiEditorClient.SanitizeBlocks(raw, "generate", catalog);
        Assert.Equal(4, blocks.Count);
        var contents = new[]
        {
            blocks[0].Content!.Value,
            blocks[1].Content!.Value,
            blocks[2].Content!.Value.GetProperty("rows")[0].GetProperty("cells")[0],
            blocks[3].Content!.Value[0].GetProperty("content")
        };
        foreach (var content in contents)
        {
            Assert.Equal(2, content.GetArrayLength());
            var emoji = content[1];
            Assert.Equal("customEmoji", emoji.GetProperty("type").GetString());
            Assert.Equal(id, emoji.GetProperty("props").GetProperty("emojiId").GetString());
            Assert.Equal("omdoebt_emoji", emoji.GetProperty("props").GetProperty("name").GetString());
            Assert.Equal(2, emoji.GetProperty("props").EnumerateObject().Count());
        }
    }

    [Fact]
    public void SanitizerDropsEmojiWhenNoCurrentCatalogueExists()
    {
        var raw = new[] { new EditorAiBlock { Type = "paragraph", Content = JsonSerializer.SerializeToElement(new object[]
        {
            new { type = "text", text = "Bevar teksten" },
            new { type = "customEmoji", props = new { emojiId = Guid.NewGuid().ToString(), name = "forged" } }
        }) } };
        var block = Assert.Single(OpenAiEditorClient.SanitizeBlocks(raw, "generate"));
        Assert.Equal("Bevar teksten", block.Content!.Value[0].GetProperty("text").GetString());
        Assert.Equal(1, block.Content.Value.GetArrayLength());
    }
}
