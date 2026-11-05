using Mercantec.Tech.Web.Courses;

namespace Mercantec.Tech.Web.Tests;

public class CourseMarkdownTests
{
    [Fact]
    public void ToSegments_splits_fenced_code_from_prose()
    {
        const string md = """
            Intro tekst.

            ```csharp
            Console.WriteLine("hej");
            ```

            Mere tekst.

            ```json
            { "ok": true }
            ```
            """;

        var segments = CourseMarkdown.ToSegments(md);

        Assert.Equal(4, segments.Count);
        Assert.IsType<CourseMarkdownHtmlSegment>(segments[0]);
        var cs = Assert.IsType<CourseMarkdownCodeSegment>(segments[1]);
        Assert.Equal("csharp", cs.Language);
        Assert.Contains("Console.WriteLine", cs.Code, StringComparison.Ordinal);
        Assert.IsType<CourseMarkdownHtmlSegment>(segments[2]);
        var json = Assert.IsType<CourseMarkdownCodeSegment>(segments[3]);
        Assert.Equal("json", json.Language);
        Assert.Contains("\"ok\"", json.Code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cs", "C#")]
    [InlineData("csharp", "C#")]
    [InlineData("js", "JavaScript")]
    [InlineData("javascript", "JavaScript")]
    [InlineData("ts", "TypeScript")]
    [InlineData("json", "JSON")]
    [InlineData("html", "HTML")]
    [InlineData("css", "CSS")]
    [InlineData("sql", "SQL")]
    [InlineData("bash", "Bash")]
    [InlineData("shell", "Bash")]
    [InlineData("", "Kode")]
    public void DisplayLanguage_maps_aliases(string raw, string expected) =>
        Assert.Equal(expected, CourseMarkdown.DisplayLanguage(raw));

    [Theory]
    [InlineData("cs")]
    [InlineData("csharp")]
    [InlineData("c#")]
    [InlineData("js")]
    [InlineData("javascript")]
    [InlineData("json")]
    [InlineData("html")]
    [InlineData("css")]
    [InlineData("sql")]
    [InlineData("ts")]
    [InlineData("xml")]
    public void ResolveLanguage_knows_teaching_langs(string raw) =>
        Assert.NotNull(CourseMarkdown.ResolveLanguage(raw));

    [Theory]
    [InlineData("bash")]
    [InlineData("txt")]
    [InlineData("mermaid")]
    [InlineData("")]
    public void ResolveLanguage_returns_null_for_plain(string raw) =>
        Assert.Null(CourseMarkdown.ResolveLanguage(raw));

    [Fact]
    public void HighlightCode_emits_token_spans_for_csharp()
    {
        var html = CourseMarkdown.HighlightCode("public class Foo {}", "cs").Value;
        Assert.Contains("keyword", html, StringComparison.Ordinal);
        Assert.Contains("public", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HighlightCode_escapes_plain_when_unknown()
    {
        var html = CourseMarkdown.HighlightCode("<script>x</script>", "bash").Value;
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSegments_handles_h2_teori_fences()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "web", "wwwroot", "content", "courses", "h2-teori.json"));
        Assert.True(File.Exists(path), $"Mangler {path}");

        var json = File.ReadAllText(path);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var codeCount = 0;
        foreach (var block in doc.RootElement.EnumerateArray())
        {
            var md = block.GetProperty("markdown").GetString() ?? "";
            foreach (var seg in CourseMarkdown.ToSegments(md))
            {
                if (seg is CourseMarkdownCodeSegment)
                    codeCount++;
            }
        }

        // ~155 kolonne-0-par + enkelte indenterede fences i H2-teori
        Assert.True(codeCount >= 155, $"Forventede ≥155 kodeblokke, fik {codeCount}");
    }

    [Fact]
    public void ToSegments_keeps_mermaid_as_code_segment()
    {
        const string md = """
            Se diagram:

            ```mermaid
            flowchart LR
              A --> B
            ```
            """;

        var segments = CourseMarkdown.ToSegments(md);
        var fence = Assert.Single(segments.OfType<CourseMarkdownCodeSegment>());
        Assert.Equal("mermaid", fence.Language);
        Assert.Contains("flowchart LR", fence.Code, StringComparison.Ordinal);
        Assert.Equal("Mermaid", CourseMarkdown.DisplayLanguage(fence.Language));
        Assert.Null(CourseMarkdown.ResolveLanguage("mermaid"));
    }

    [Fact]
    public void ToSegments_accepts_commonmark_indented_fence()
    {
        const string md = "Tekst\n\n   ```cs\n   var x = 1;\n   ```\n";
        var fence = Assert.Single(CourseMarkdown.ToSegments(md).OfType<CourseMarkdownCodeSegment>());
        Assert.Equal("cs", fence.Language);
        Assert.Contains("var x = 1;", fence.Code, StringComparison.Ordinal);
    }
}
