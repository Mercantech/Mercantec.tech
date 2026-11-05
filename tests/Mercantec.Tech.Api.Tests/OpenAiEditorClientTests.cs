using System.Text.Json;
using Mercantec.Tech.Api.Integrations.OpenAI;

namespace Mercantec.Tech.Api.Tests;

public sealed class OpenAiEditorClientTests
{
    [Fact]
    public void IsConfigured_RequiresApiKey()
    {
        var opts = new OpenAiOptions();
        Assert.False(opts.IsConfigured);
        opts.ApiKey = "sk-test";
        Assert.True(opts.IsConfigured);
    }

    [Theory]
    [InlineData(null, "generate")]
    [InlineData("GENERATE", "generate")]
    [InlineData("rewrite", "rewrite")]
    [InlineData("diagram", "mermaid")]
    [InlineData("mermaid", "mermaid")]
    public void NormalizeAction_MapsAliases(string? input, string expected)
    {
        Assert.Equal(expected, OpenAiEditorClient.NormalizeAction(input));
    }

    [Fact]
    public void SanitizeBlocks_KeepsWhitelistAndStripsUnknown()
    {
        var raw = new[]
        {
            new EditorAiBlock { Type = "paragraph", Content = JsonSerializer.SerializeToElement("Hej") },
            new EditorAiBlock { Type = "script", Content = JsonSerializer.SerializeToElement("evil") },
            new EditorAiBlock
            {
                Type = "callout",
                Props = new Dictionary<string, JsonElement>
                {
                    ["tone"] = JsonSerializer.SerializeToElement("tip")
                },
                Content = JsonSerializer.SerializeToElement("Tip")
            },
            new EditorAiBlock
            {
                Type = "heading",
                Props = new Dictionary<string, JsonElement>
                {
                    ["level"] = JsonSerializer.SerializeToElement(9)
                },
                Content = JsonSerializer.SerializeToElement("Titel")
            }
        };

        var blocks = OpenAiEditorClient.SanitizeBlocks(raw, "generate");
        Assert.Equal(3, blocks.Count);
        Assert.Equal("paragraph", blocks[0].Type);
        Assert.Equal("callout", blocks[1].Type);
        Assert.Equal("tip", blocks[1].Props!["tone"].GetString());
        Assert.Equal(3, blocks[2].Props!["level"].GetInt32());
    }

    [Fact]
    public void SanitizeBlocks_MermaidAction_OnlyDiagram()
    {
        var raw = new[]
        {
            new EditorAiBlock { Type = "paragraph", Content = JsonSerializer.SerializeToElement("x") },
            new EditorAiBlock
            {
                Type = "mermaidDiagram",
                Props = new Dictionary<string, JsonElement>
                {
                    ["source"] = JsonSerializer.SerializeToElement("flowchart TD\nA-->B")
                }
            }
        };

        var blocks = OpenAiEditorClient.SanitizeBlocks(raw, "mermaid");
        Assert.Single(blocks);
        Assert.Equal("mermaidDiagram", blocks[0].Type);
        Assert.Contains("flowchart", blocks[0].Props!["source"].GetString());
    }

    [Fact]
    public void ParseModelJson_AcceptsFencedPayload()
    {
        var payload = OpenAiEditorClient.ParseModelJson("""
            ```json
            {"blocks":[{"type":"paragraph","content":"Ok"}],"assistantNote":"klar"}
            ```
            """);
        Assert.NotNull(payload.Blocks);
        Assert.Single(payload.Blocks!);
        Assert.Equal("klar", payload.AssistantNote);
    }
}
