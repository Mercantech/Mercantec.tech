using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Courses;

/// <summary>Henter fulde «Teori for dagen»-blokke fra statisk JSON under wwwroot.</summary>
public sealed class CourseTheoryDocumentLoader(HttpClient http, NavigationManager nav)
{
    private static readonly Dictionary<string, CourseTheoryDocumentBlock[]> Cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<CourseTheoryDocumentBlock[]> LoadAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Cache.TryGetValue(relativePath, out var cached))
            return cached;

        var uri = nav.ToAbsoluteUri(relativePath.TrimStart('/'));
        var dto = await http.GetFromJsonAsync<TheoryBlockDto[]>(uri, ct)
            ?? Array.Empty<TheoryBlockDto>();

        var blocks = dto
            .OrderBy(b => b.Number)
            .Select(b => new CourseTheoryDocumentBlock(
                b.Number,
                string.IsNullOrWhiteSpace(b.Title) ? $"Teori {b.Number}" : b.Title.Trim(),
                b.Markdown ?? string.Empty))
            .ToArray();

        Cache[relativePath] = blocks;
        return blocks;
    }

    private sealed class TheoryBlockDto
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("markdown")]
        public string? Markdown { get; set; }
    }
}
