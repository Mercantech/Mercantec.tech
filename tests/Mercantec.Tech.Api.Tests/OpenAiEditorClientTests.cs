using System.Text.Json;
using System.Text.RegularExpressions;
using Mercantec.Tech.Api.Integrations.OpenAI;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class OpenAiEditorClientTests
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
        Assert.Equal(6, blocks[2].Props!["level"].GetInt32());
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

    [Theory]
    [InlineData("generate")]
    [InlineData("rewrite")]
    [InlineData("mermaid")]
    public void BuildSystemPrompt_SendsBlockCatalogForEveryAction(string action)
    {
        var prompt = OpenAiEditorClient.BuildSystemPrompt(action);
        Assert.StartsWith(OpenAiEditorClient.BlockCatalog, prompt);
        Assert.Contains("pageLink", prompt);
        Assert.Contains("mermaidDiagram", prompt);
        Assert.Contains("tableContent", prompt);
        Assert.Contains("Aldrig HTML", prompt);
    }

    [Fact]
    public void BlockCatalog_MatchesCheckedInPromptFile()
    {
        var file = File.ReadAllText(Path.Combine(RepoRoot(), "api", "Integrations", "OpenAI", "editor-block-prompt.txt"));
        Assert.Equal(Normalize(file), Normalize(OpenAiEditorClient.BlockCatalog));
    }

    [Fact]
    public void EditorBlockPrompt_NamesEveryCustomBlockType()
    {
        var prompt = OpenAiEditorClient.BlockCatalog;
        var types = CustomBlockTypes();
        Assert.NotEmpty(types);
        foreach (var type in types)
            Assert.Contains(type, prompt);

        foreach (var required in new[]
        {
            "paragraph", "heading", "bulletListItem", "numberedListItem", "checkListItem", "toggleListItem",
            "quote", "codeBlock", "callout", "table", "divider", "learningGoal", "pageMention", "studentMention"
        })
            Assert.Contains(required, prompt);

        var callout = File.ReadAllText(Path.Combine(RepoRoot(), "editor", "src", "callout-block.tsx"));
        var tones = Regex.Match(callout, @"const TONES = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value;
        Assert.NotEmpty(tones);
        foreach (Match tone in Regex.Matches(tones, @"'([^']+)'"))
            Assert.Contains(tone.Groups[1].Value, prompt);

        var code = File.ReadAllText(Path.Combine(RepoRoot(), "editor", "src", "code-block.ts"));
        var order = Regex.Match(code, @"const preferredOrder = \[([\s\S]*?)\]").Groups[1].Value;
        Assert.NotEmpty(order);
        foreach (Match lang in Regex.Matches(order, @"'([^']+)'"))
            Assert.Matches($@"(?<![A-Za-z0-9]){Regex.Escape(lang.Groups[1].Value)}(?![A-Za-z0-9])", prompt);
    }

    [Fact]
    public void SanitizeBlocks_KeepsTablePageLinkMentionAndDropsLiveBlocks()
    {
        var pageId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var holdId = Guid.NewGuid();
        var catalog = EditorAiCatalog.From(new EditorAiRequest(
            "generate",
            "x",
            HoldId: holdId.ToString(),
            Pages: [new EditorAiPageRef(pageId.ToString(), "Loops")],
            Members: [new EditorAiMemberRef(memberId.ToString(), "Ada")]));

        var raw = new[]
        {
            new EditorAiBlock { Type = "classRoster", Props = Props(("holdId", holdId.ToString())) },
            new EditorAiBlock { Type = "pageMedia", Props = Props(("assetId", Guid.NewGuid().ToString())) },
            new EditorAiBlock { Type = "image", Props = Props(("url", "https://example.test/a.png")) },
            new EditorAiBlock
            {
                Type = "pageLink",
                Props = Props(("pageId", Guid.NewGuid().ToString()))
            },
            new EditorAiBlock
            {
                Type = "pageLink",
                Props = Props(("pageId", pageId.ToString()))
            },
            new EditorAiBlock
            {
                Type = "paragraph",
                Content = JsonSerializer.SerializeToElement(new object[]
                {
                    new { type = "text", text = "Se ", styles = new { bold = true } },
                    new { type = "pageMention", props = new { pageId = pageId.ToString() } },
                    new { type = "studentMention", props = new { membershipId = memberId.ToString() } },
                    new { type = "link", href = "javascript:alert(1)", content = "farlig" }
                })
            },
            new EditorAiBlock
            {
                Type = "table",
                Content = JsonSerializer.SerializeToElement(new
                {
                    type = "tableContent",
                    headerRows = 1,
                    rows = new object[]
                    {
                        new { cells = new[] { "Emne", "Eksempel" } },
                        new { cells = new[] { "Loop" } }
                    }
                })
            },
            new EditorAiBlock
            {
                Type = "codeBlock",
                Props = Props(("language", "C#")),
                Content = JsonSerializer.SerializeToElement("List<string> xs;")
            },
            new EditorAiBlock
            {
                Type = "bulletListItem",
                Content = JsonSerializer.SerializeToElement("Ydre"),
                Children =
                [
                    new EditorAiBlock { Type = "bulletListItem", Content = JsonSerializer.SerializeToElement("Indre") },
                    new EditorAiBlock { Type = "script", Content = JsonSerializer.SerializeToElement("nej") }
                ]
            },
            new EditorAiBlock
            {
                Type = "mermaidDiagram",
                Props = Props(("source", "```mermaid\nflowchart TD\nA-->B\n```"))
            }
        };

        var blocks = OpenAiEditorClient.SanitizeBlocks(raw, "generate", catalog);
        Assert.Equal(6, blocks.Count);
        Assert.Equal("pageLink", blocks[0].Type);
        Assert.Equal(pageId.ToString(), blocks[0].Props!["pageId"].GetString());
        Assert.Null(blocks[0].Content);

        var inline = blocks[1].Content!.Value;
        Assert.Equal(JsonValueKind.Array, inline.ValueKind);
        Assert.Equal("pageMention", inline[1].GetProperty("type").GetString());
        Assert.Equal(pageId.ToString(), inline[1].GetProperty("props").GetProperty("pageId").GetString());
        Assert.Equal("studentMention", inline[2].GetProperty("type").GetString());
        Assert.Equal(holdId.ToString(), inline[2].GetProperty("props").GetProperty("holdId").GetString());
        Assert.Equal("text", inline[3].GetProperty("type").GetString());
        Assert.Equal("farlig", inline[3].GetProperty("text").GetString());

        var table = blocks[2].Content!.Value;
        Assert.Equal("tableContent", table.GetProperty("type").GetString());
        Assert.Equal(1, table.GetProperty("headerRows").GetInt32());
        Assert.Equal(2, table.GetProperty("rows")[1].GetProperty("cells").GetArrayLength());
        Assert.Equal("", table.GetProperty("rows")[1].GetProperty("cells")[1].GetString());

        Assert.Equal("csharp", blocks[3].Props!["language"].GetString());
        Assert.Equal("List<string> xs;", blocks[3].Content!.Value.GetString());

        Assert.Equal("bulletListItem", blocks[4].Children![0].Type);
        Assert.Single(blocks[4].Children!);

        Assert.Equal("mermaidDiagram", blocks[5].Type);
        Assert.Equal("flowchart TD\nA-->B", blocks[5].Props!["source"].GetString());
    }

    [Fact]
    public void SanitizeBlocks_DropsPageLinkWithoutCatalog()
    {
        var raw = new[]
        {
            new EditorAiBlock
            {
                Type = "pageLink",
                Props = Props(("pageId", Guid.NewGuid().ToString()))
            },
            new EditorAiBlock { Type = "paragraph", Content = JsonSerializer.SerializeToElement("Kun tekst") }
        };

        var blocks = OpenAiEditorClient.SanitizeBlocks(raw, "generate");
        Assert.Single(blocks);
        Assert.Equal("paragraph", blocks[0].Type);
    }

    private static Dictionary<string, JsonElement> Props(params (string Key, string Value)[] items)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (key, value) in items)
            dict[key] = JsonSerializer.SerializeToElement(value);
        return dict;
    }

    private static IReadOnlySet<string> CustomBlockTypes()
    {
        var root = Path.Combine(RepoRoot(), "editor", "src");
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(file) is not (".ts" or ".tsx" or ".mjs"))
                continue;
            foreach (Match match in TypePattern().Matches(File.ReadAllText(file)))
                types.Add(match.Groups[1].Value);
        }

        return types;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "editor", "src", "editor.tsx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo_root_missing");
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n").Trim();

    [GeneratedRegex(@"type:\s*'([A-Za-z][A-Za-z0-9]*)'")]
    private static partial Regex TypePattern();
}
