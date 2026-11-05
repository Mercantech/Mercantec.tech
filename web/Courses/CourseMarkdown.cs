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
        new(Markdown.ToHtml(markdown ?? string.Empty, Pipeline));

    /// <summary>Splitter fenced code ud, så hver blok kan få highlight + Kopiér-knap i UI.</summary>
    public static IReadOnlyList<CourseMarkdownSegment> ToSegments(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return Array.Empty<CourseMarkdownSegment>();

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
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

            // Mermaid m.fl. bevares som code-segment (sprog-hint) — diagram-rendering er separat PR.
            segments.Add(new CourseMarkdownCodeSegment(language, code.ToString()));
            // Lukke-``` er «brugt»; for-løkken stepper videre.
        }

        FlushProse(prose, segments);
        return segments;
    }

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

        segments.Add(new CourseMarkdownHtmlSegment(ToHtml(text)));
    }
}
