using System.Net;
using System.Text;
using ColorCode;
using Markdig;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Courses;

/// <summary>Markdown-segment: prose (HTML) eller fenced code-blok.</summary>
public abstract record CourseMarkdownSegment;

/// <summary>Prose rendret til sikker HTML via Markdig.</summary>
public sealed record CourseMarkdownHtmlSegment(MarkupString Html) : CourseMarkdownSegment;

/// <summary>Fenced code med sprog-hint (kan være tom).</summary>
public sealed record CourseMarkdownCodeSegment(string Language, string Code) : CourseMarkdownSegment;

/// <summary>Markdown → sikker HTML / kodeblokke til kursusteori (ingen rå HTML fra kilden).</summary>
public static class CourseMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    // HtmlClassFormatter er ikke trådsikker pr. instans — én pr. highlight-kald.
    private static readonly RazorLanguage Razor = CreateRazorLanguage();

    private static RazorLanguage CreateRazorLanguage()
    {
        var lang = new RazorLanguage();
        Languages.Load(lang);
        return lang;
    }

    public static MarkupString ToHtml(string? markdown) =>
        RenderHtml(PrepareMarkdown(markdown));

    private static MarkupString RenderHtml(string markdown) =>
        new(Markdown.ToHtml(markdown, Pipeline));

    /// <summary>
    /// Notion/Lectio eksporterer ofte tabeller som én celle pr. linje med blanklinje imellem
    /// (uden <c>| … |</c>). Genopbyg GFM pipe-tables før Markdig, så vi får rigtige &lt;table&gt;.
    /// </summary>
    public static string NormalizeImportedTables(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var output = new List<string>(lines.Length);
        var inFence = false;

        for (var i = 0; i < lines.Length;)
        {
            var line = lines[i];
            if (IsFenceMarker(line))
            {
                inFence = !inFence;
                output.Add(line);
                i++;
                continue;
            }

            if (inFence || line.Length > 0 && line.TrimStart().StartsWith('|'))
            {
                output.Add(line);
                i++;
                continue;
            }

            if (TryReadStackedTable(lines, i, out var consumed, out var tableMarkdown))
            {
                if (output.Count > 0 && output[^1].Length > 0)
                    output.Add(string.Empty);
                foreach (var tableLine in tableMarkdown.Split('\n'))
                    output.Add(tableLine);
                i += consumed;
                // Bevar én trailing blank hvis kilden havde det efter tabellen.
                if (i < lines.Length && lines[i].Length == 0)
                {
                    output.Add(string.Empty);
                    i++;
                }

                continue;
            }

            output.Add(line);
            i++;
        }

        return string.Join('\n', output);
    }

    /// <summary>Splitter fenced code ud, så hver blok kan få highlight + Kopiér-knap i UI.</summary>
    public static IReadOnlyList<CourseMarkdownSegment> ToSegments(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return Array.Empty<CourseMarkdownSegment>();

        var normalized = PrepareMarkdown(markdown);
        var lines = normalized.Split('\n');
        var segments = new List<CourseMarkdownSegment>();
        var prose = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!TryReadFenceOpener(line, out var language))
            {
                if (prose.Length > 0)
                    prose.Append('\n');
                prose.Append(line);
                continue;
            }

            FlushProse(prose, segments);

            var code = new StringBuilder();
            i++;
            while (i < lines.Length && !IsFenceMarker(lines[i]))
            {
                if (code.Length > 0)
                    code.Append('\n');
                code.Append(lines[i]);
                i++;
            }

            // Mermaid bevares som code-segment; CourseMarkdownBody tegner dem via CourseMermaidDiagram.
            segments.Add(new CourseMarkdownCodeSegment(language, code.ToString()));
            // Lukke-``` er «brugt»; for-løkken stepper videre.
        }

        FlushProse(prose, segments);
        return segments;
    }

    private static string PrepareMarkdown(string? markdown) =>
        NormalizeImportedTables(markdown);

    /// <summary>Syntax-highlightet HTML, eller escaped plain &lt;pre&gt; hvis sproget er ukendt.</summary>
    public static MarkupString HighlightCode(string code, string? language)
    {
        var lang = ResolveLanguage(language);
        if (lang is null)
        {
            var escaped = WebUtility.HtmlEncode(code);
            return new MarkupString($"<pre><code>{escaped}</code></pre>");
        }

        // Ny formatter pr. kald — HtmlClassFormatter muterer intern Writer under GetHtmlString.
        return new MarkupString(new HtmlClassFormatter().GetHtmlString(code, lang));
    }

    /// <summary>Kort label til toolbar (dansk UI viser tekniske sprog-navne).</summary>
    public static string DisplayLanguage(string? language)
    {
        var key = NormalizeLanguageKey(language);
        return key switch
        {
            "cs" or "csharp" or "c#" => "C#",
            "js" or "javascript" => "JavaScript",
            "ts" or "typescript" => "TypeScript",
            "json" => "JSON",
            "html" or "htm" => "HTML",
            "css" => "CSS",
            "sql" => "SQL",
            "xml" => "XML",
            "bash" or "shell" or "sh" => "Bash",
            "powershell" or "ps1" => "PowerShell",
            "python" or "py" => "Python",
            "razor" or "blazor" or "cshtml" => "Razor",
            "txt" or "text" or "plaintext" => "Tekst",
            "mermaid" => "Mermaid",
            "" => "Kode",
            _ => string.IsNullOrEmpty(language) ? "Kode" : language.Trim()
        };
    }

    public static ILanguage? ResolveLanguage(string? language)
    {
        var key = NormalizeLanguageKey(language);
        if (key.Length == 0)
            return null;

        if (key is "razor" or "blazor" or "cshtml")
            return Razor;

        // ColorCode FindById kender bl.a. cs, c#, csharp, js, json, ts, …
        var found = Languages.FindById(key);
        if (found is not null)
            return found;

        return key switch
        {
            "c#" or "csharp" or "cs" => Languages.CSharp,
            "js" or "javascript" => Languages.JavaScript,
            "ts" or "typescript" => Languages.Typescript,
            "htm" => Languages.Html,
            "powershell" or "ps1" => Languages.PowerShell,
            "py" => Languages.Python,
            _ => null
        };
    }

    private static string NormalizeLanguageKey(string? language) =>
        (language ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

    /// <summary>CommonMark: op til 3 mellemrum før fence.</summary>
    private static bool IsFenceMarker(string line)
    {
        var i = 0;
        while (i < line.Length && i < 3 && line[i] == ' ')
            i++;
        return i < line.Length && line.AsSpan(i).StartsWith("```", StringComparison.Ordinal);
    }

    private static bool TryReadFenceOpener(string line, out string language)
    {
        language = string.Empty;
        if (!IsFenceMarker(line))
            return false;

        var i = 0;
        while (i < line.Length && i < 3 && line[i] == ' ')
            i++;
        var afterTicks = line[(i + 3)..].Trim();
        if (afterTicks.Length > 0)
        {
            language = afterTicks.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
                ? parts[0].Trim()
                : string.Empty;
        }

        return true;
    }

    private static void FlushProse(StringBuilder prose, List<CourseMarkdownSegment> segments)
    {
        if (prose.Length == 0)
            return;

        var text = prose.ToString();
        prose.Clear();
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Prose er allerede PrepareMarkdown't i ToSegments — undgå dobbelt-normalisering.
        segments.Add(new CourseMarkdownHtmlSegment(RenderHtml(text)));
    }

    /// <summary>
    /// Læser en Notion/Lectio-«stablet» tabel: celle, blanklinje, celle, …
    /// Kræver mindst header + én datarække og header-lignende første række.
    /// </summary>
    private static bool TryReadStackedTable(
        string[] lines,
        int start,
        out int consumed,
        out string tableMarkdown)
    {
        consumed = 0;
        tableMarkdown = string.Empty;

        if (start >= lines.Length || lines[start].Length == 0)
            return false;
        if (IsStackedTableTerminator(lines[start]))
            return false;

        var cells = new List<string>();
        var i = start;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Length == 0)
                break;
            if (IsStackedTableTerminator(line))
                break;
            if (line.TrimStart().StartsWith('|'))
                break;
            // Prosa efter Notion-tabel (ingen ---) må ikke indgå som celle.
            if (cells.Count > 0 && LooksLikeProseParagraph(line))
                break;

            cells.Add(line);
            i++;

            if (i >= lines.Length)
                break;

            // Forvent præcis én blanklinje mellem celler.
            if (lines[i].Length != 0)
                break;

            var next = i + 1;
            if (next >= lines.Length || lines[next].Length == 0)
                break; // dobbelt blank / slut → afslut run
            if (IsStackedTableTerminator(lines[next]))
                break;
            if (lines[next].TrimStart().StartsWith('|'))
                break;
            if (LooksLikeProseParagraph(lines[next]))
                break;

            i = next; // spring blank over, næste iteration læser cellen
        }

        // Mindst header + 2 body-rækker (4 celler ved 2 col) — undgår prosa-par som falske tabeller.
        if (cells.Count < 6)
            return false;

        if (!TryInferColumnCount(cells, out var cols))
            return false;

        var rowCount = cells.Count / cols;
        if (rowCount < 3)
            return false;

        var used = rowCount * cols;
        var sb = new StringBuilder();
        for (var r = 0; r < rowCount; r++)
        {
            sb.Append('|');
            for (var c = 0; c < cols; c++)
            {
                sb.Append(' ');
                sb.Append(EscapePipeCell(cells[r * cols + c]));
                sb.Append(' ');
                sb.Append('|');
            }

            sb.Append('\n');
            if (r == 0)
            {
                sb.Append('|');
                for (var c = 0; c < cols; c++)
                    sb.Append(" --- |");
                sb.Append('\n');
            }
        }

        // Forbrugte kildelinjer: hver celle + blanklinje mellem dem (ikke efter sidste brugte).
        // start..i pegete på næste ulæste; genberegn præcist fra used celler.
        consumed = CountStackedSourceLines(lines, start, used);
        tableMarkdown = sb.ToString().TrimEnd('\n');
        return consumed > 0;
    }

    private static int CountStackedSourceLines(string[] lines, int start, int cellCount)
    {
        var seen = 0;
        var i = start;
        while (i < lines.Length && seen < cellCount)
        {
            if (lines[i].Length == 0)
            {
                i++;
                continue;
            }

            seen++;
            i++;
            if (seen < cellCount && i < lines.Length && lines[i].Length == 0)
                i++; // blank mellem celler
        }

        return i - start;
    }

    private static bool TryInferColumnCount(IReadOnlyList<string> cells, out int cols)
    {
        cols = 0;
        var bestScore = int.MinValue;
        var maxCols = Math.Min(6, cells.Count / 2);

        for (var candidate = 2; candidate <= maxCols; candidate++)
        {
            if (cells.Count < candidate * 2)
                continue;

            var usable = (cells.Count / candidate) * candidate;
            if (usable < candidate * 2)
                continue;

            var header = new string[candidate];
            for (var c = 0; c < candidate; c++)
                header[c] = cells[c];

            if (!LooksLikeHeaderRow(header))
                continue;

            var firstBody = cells[candidate];
            if (LooksLikeProseParagraph(firstBody))
                continue;

            var rows = usable / candidate;
            // Kræv header + mindst 2 body-rækker allerede i scoring (ellers kan 5-col med
            // 2 rækker vinde på «exact divide» og derefter blive kasseret → mistet header-celle).
            if (rows < 3)
                continue;

            var score = 10 + rows * 2 + HeaderQuality(header);

            // Jævn division er stærkt signal (Notion-tabeller eksporteres komplet).
            if (cells.Count % candidate == 0)
                score += 16;
            else
                score -= 8; // rest-celler tilbage → sandsynligvis forkert col-count

            // Notion-body bruger ofte emoji/✅ i første kolonne — beløn det.
            if (StartsWithEmojiOrSymbol(firstBody))
                score += 6;
            if (LooksLikeDataCell(firstBody))
                score += 4;

            // Ved lige score: færre kolonner (undgår at æde første body-celle ind i header).
            if (score > bestScore || (score == bestScore && (cols == 0 || candidate < cols)))
            {
                bestScore = score;
                cols = candidate;
            }
        }

        return cols >= 2;
    }

    private static int HeaderQuality(IReadOnlyList<string> header)
    {
        var score = 0;
        foreach (var cell in header)
        {
            var t = cell.Trim();
            if (LooksLikeDataCell(t))
            {
                score -= 10;
                continue;
            }

            if (t.Length <= 24)
                score += 2;
            if (!t.Contains('`') && !StartsWithEmojiOrSymbol(t))
                score += 2;
            var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length is >= 1 and <= 3)
                score += 2;
        }

        return score;
    }

    private static bool LooksLikeHeaderRow(IReadOnlyList<string> header)
    {
        foreach (var cell in header)
        {
            if (!LooksLikeHeaderCell(cell))
                return false;
        }

        return header.Any(c => !StartsWithEmojiOrSymbol(c.Trim()));
    }

    private static bool LooksLikeHeaderCell(string cell)
    {
        var t = cell.Trim();
        if (t.Length == 0 || t.Length > 48)
            return false;
        if (LooksLikeProseParagraph(t))
            return false;
        if (StartsWithEmojiOrSymbol(t))
            return false;
        if (LooksLikeDataCell(t))
            return false;
        // "Åbn følgende adresse:" / "Det afhænger af …:" er labels, ikke kolonneheaders.
        if (t.EndsWith(':'))
            return false;
        if (t.Contains("](", StringComparison.Ordinal))
            return false;

        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 4)
            return false;

        return true;
    }

    /// <summary>Værdier typiske i Notion-body (relationer, kardinalitet, rene kode-spans, bold labels).</summary>
    private static bool LooksLikeDataCell(string cell)
    {
        var t = cell.Trim();
        if (t.Contains('→') || t.Contains("->", StringComparison.Ordinal))
            return true;
        if (t is "1:1" or "1:n" or "n:1" or "n:m" or "N:M" or "1:N")
            return true;
        if (t.Length >= 2 && t[0] == '`' && t[^1] == '`' && !t.Contains(' '))
            return true;
        // Rolle-navne o.l. i body: **Admin**, **Kunde** — ikke kolonneheaders.
        if (t.Length >= 4 && t.StartsWith("**", StringComparison.Ordinal) && t.EndsWith("**", StringComparison.Ordinal)
            && t.IndexOf("**", 2, StringComparison.Ordinal) == t.Length - 2)
            return true;
        return false;
    }

    private static bool StartsWithEmojiOrSymbol(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        var c = char.ConvertToUtf32(text, 0);
        // Typiske Notion-status/emoji i tabeller (✅ ⚡ 📦 …) samt øvrige symbol-blokke.
        return c is >= 0x1F300 and <= 0x1FAFF
            or >= 0x2600 and <= 0x27BF
            or >= 0x2300 and <= 0x23FF
            or 0x2705 or 0x274C or 0x2B50;
    }

    private static bool LooksLikeProseParagraph(string cell)
    {
        var t = cell.Trim();
        if (t.Length >= 100)
            return true;

        // Dansk prosa efter Notion-tabel (uden ---). Kræv længde, så ægte celler
        // som «Du kan stadig sende flere roller…» ikke stoppes midt i tabellen.
        if (t.Length >= 55
            && (t.StartsWith("Det vil sige", StringComparison.Ordinal)
                || t.StartsWith("Brug kun", StringComparison.Ordinal)
                || t.StartsWith("Brug kun disse", StringComparison.Ordinal)))
            return true;

        if (t.EndsWith('.') && t.Length > 70 && !t.StartsWith('`') && !t.StartsWith("**")
            && !StartsWithEmojiOrSymbol(t))
            return true;

        return false;
    }

    private static bool IsStackedTableTerminator(string line)
    {
        if (line.Length == 0)
            return true;

        var t = line.TrimStart();
        if (t.StartsWith('#'))
            return true;
        if (t.StartsWith("---", StringComparison.Ordinal))
            return true;
        if (t.StartsWith("```", StringComparison.Ordinal))
            return true;
        if (t.StartsWith("- ", StringComparison.Ordinal) || t.StartsWith("* ", StringComparison.Ordinal))
            return true;
        if (t.Length > 2 && char.IsDigit(t[0]) && t.Contains(". ", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string EscapePipeCell(string cell) =>
        cell.Replace("|", "\\|", StringComparison.Ordinal).Trim();
}
