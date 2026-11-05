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
    [InlineData("razor", "Razor")]
    [InlineData("blazor", "Razor")]
    [InlineData("cshtml", "Razor")]
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
    [InlineData("razor")]
    [InlineData("blazor")]
    [InlineData("cshtml")]
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

    [Theory]
    [InlineData("razor")]
    [InlineData("blazor")]
    [InlineData("cshtml")]
    public void HighlightCode_emits_razor_tokens(string language)
    {
        const string code = """
            @page "/hello"
            @* note *@
            <h1>Hej @name</h1>
            <button @onclick="Save">Gem</button>
            @code {
                private string name = "Verden";
            }
            """;

        var html = CourseMarkdown.HighlightCode(code, language).Value;
        Assert.Contains("htmlServerSideScript", html, StringComparison.Ordinal);
        Assert.Contains("htmlElementName", html, StringComparison.Ordinal);
        Assert.Contains("htmlComment", html, StringComparison.Ordinal);
        Assert.Contains("keyword", html, StringComparison.Ordinal);
        Assert.Contains("@page", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSegments_keeps_razor_fence_language()
    {
        const string md = """
            Blazor-komponent:

            ```blazor
            @page "/demo"
            <p>@msg</p>
            ```
            """;

        var fence = Assert.Single(CourseMarkdown.ToSegments(md).OfType<CourseMarkdownCodeSegment>());
        Assert.Equal("blazor", fence.Language);
        Assert.Equal("Razor", CourseMarkdown.DisplayLanguage(fence.Language));
        Assert.NotNull(CourseMarkdown.ResolveLanguage(fence.Language));
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
        // Ukendt for ColorCode → escaped plain (CourseMermaidDiagram tegner SVG i stedet).
        var highlighted = CourseMarkdown.HighlightCode(fence.Code, fence.Language).Value;
        Assert.Contains("flowchart LR", highlighted, StringComparison.Ordinal);
        Assert.DoesNotContain("keyword", highlighted, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSegments_extracts_h2_teori_mermaid_fences()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "web", "wwwroot", "content", "courses", "h2-teori.json"));
        Assert.True(File.Exists(path), $"Mangler {path}");

        var json = File.ReadAllText(path);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var mermaid = new List<CourseMarkdownCodeSegment>();
        foreach (var block in doc.RootElement.EnumerateArray())
        {
            var md = block.GetProperty("markdown").GetString() ?? "";
            foreach (var seg in CourseMarkdown.ToSegments(md))
            {
                if (seg is CourseMarkdownCodeSegment code
                    && code.Language.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
                    mermaid.Add(code);
            }
        }

        Assert.True(mermaid.Count >= 2, $"Forventede ≥2 Mermaid-blokke i H2-teori, fik {mermaid.Count}");
        Assert.All(mermaid, m => Assert.Contains("graph TD", m.Code, StringComparison.Ordinal));
    }

    [Fact]
    public void ToSegments_accepts_commonmark_indented_fence()
    {
        const string md = "Tekst\n\n   ```cs\n   var x = 1;\n   ```\n";
        var fence = Assert.Single(CourseMarkdown.ToSegments(md).OfType<CourseMarkdownCodeSegment>());
        Assert.Equal("cs", fence.Language);
        Assert.Contains("var x = 1;", fence.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeImportedTables_rebuilds_notion_fordel_forklaring()
    {
        const string md = """
            ### Fordele ved 1:n multirolle

            Fordel

            Forklaring

            ✅ Simpelt

            Nemmere end n:m – ingen join-tabel nødvendig

            ✅ JWT-venlig

            Du kan stadig sende flere roller som claims i JWT-tokenet

            ---

            ### Claims
            """;

        var normalized = CourseMarkdown.NormalizeImportedTables(md);
        Assert.Contains("| Fordel | Forklaring |", normalized, StringComparison.Ordinal);
        Assert.Contains("| --- | --- |", normalized, StringComparison.Ordinal);
        Assert.Contains("| ✅ Simpelt | Nemmere end n:m – ingen join-tabel nødvendig |", normalized, StringComparison.Ordinal);
        Assert.Contains("| ✅ JWT-venlig | Du kan stadig sende flere roller som claims i JWT-tokenet |", normalized, StringComparison.Ordinal);

        var html = CourseMarkdown.ToHtml(md).Value;
        Assert.Contains("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<th", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fordel", html, StringComparison.Ordinal);
        Assert.Contains("Forklaring", html, StringComparison.Ordinal);
        Assert.Contains("Simpelt", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>Fordel</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeImportedTables_rebuilds_three_column_stacked_table()
    {
        const string md = """
            ### Oversigt

            Entitet

            Relation

            Type

            User → Role

            1:1

            Én bruger har én rolle

            Hotel → Room

            1:n

            Et hotel har mange værelser
            """;

        var normalized = CourseMarkdown.NormalizeImportedTables(md);
        Assert.Contains("| Entitet | Relation | Type |", normalized, StringComparison.Ordinal);
        Assert.Contains("| --- | --- | --- |", normalized, StringComparison.Ordinal);
        Assert.Contains("| User → Role | 1:1 | Én bruger har én rolle |", normalized, StringComparison.Ordinal);

        var html = CourseMarkdown.ToHtml(md).Value;
        Assert.Contains("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Entitet", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeImportedTables_leaves_existing_pipe_tables()
    {
        const string md = """
            | Parameter | Forklaring |
            | --- | --- |
            | `-d` | Baggrund |
            """;

        var normalized = CourseMarkdown.NormalizeImportedTables(md);
        Assert.Equal(md.Replace("\r\n", "\n").TrimEnd(), normalized.TrimEnd());
    }

    [Fact]
    public void NormalizeImportedTables_rebuilds_rolle_rettigheder_two_columns()
    {
        const string md = """
            ### Hierarki

            Rolle

            Rettigheder

            **Admin**

            Alt, inkl. oprettelse af brugere og systemstyring

            **Receptionist**

            Alt hvad **Rengøring** kan, plus bookinger

            **Rengøring**

            Se værelser, som skal gøres rene

            **Kunde**

            Book værelser og se egne bookinger

            Det vil sige at en **Receptionist** kan både se værelser.
            """;

        var normalized = CourseMarkdown.NormalizeImportedTables(md);
        Assert.Contains("| Rolle | Rettigheder |", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("| Rolle | Rettigheder | **Admin** |", normalized, StringComparison.Ordinal);
        Assert.Contains("| **Admin** | Alt, inkl. oprettelse af brugere og systemstyring |", normalized, StringComparison.Ordinal);
        Assert.Contains("Det vil sige at en **Receptionist**", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_renders_h2_multirolle_section_as_table()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "web", "wwwroot", "content", "courses", "h2-teori.json"));
        Assert.True(File.Exists(path), $"Mangler {path}");

        var json = File.ReadAllText(path);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        string? section = null;
        foreach (var block in doc.RootElement.EnumerateArray())
        {
            var md = block.GetProperty("markdown").GetString() ?? "";
            var idx = md.IndexOf("### Fordele ved 1:n multirolle", StringComparison.Ordinal);
            if (idx < 0)
                continue;
            section = md[idx..];
            break;
        }

        Assert.False(string.IsNullOrEmpty(section));
        var html = CourseMarkdown.ToHtml(section).Value;
        Assert.Contains("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fordel", html, StringComparison.Ordinal);
        Assert.Contains("Forklaring", html, StringComparison.Ordinal);
        Assert.Contains("Simpelt", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>Fordel</p>", html, StringComparison.Ordinal);
    }
}
