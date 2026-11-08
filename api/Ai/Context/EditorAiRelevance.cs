using System.Text.Json;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

/// <summary>Domain matching, separate from task wording. Relationship boosts cannot manufacture relevance.</summary>
public sealed class EditorAiRelevance
{
    private readonly Dictionary<string, int> weights = new(StringComparer.Ordinal);
    private readonly HashSet<string> topics;
    private readonly HashSet<string> languages;
    private static readonly HashSet<string> LanguageTerms = ["c#", "f#", "c++", "dotnet", "java", "javascript", "typescript", "python", "rust", "golang", "sql"];
    private static readonly HashSet<string> TopicTerms = ["interface", "polymorphism", "inheritance", "encapsulation", "abstraction", "generics", "linq", "blazor", "entityframework", "efcore",
        "mqtt", "arduino", "sensor", "signalr", "dashboard", "database", "normalisering", "normalization", "sql", "unity", "godot", "coreloop", "playtest", "gdd", "machinelearning", "maskinlæring", "docker", "kubernetes", "git", "oauth", "oidc", "jwt", "pkce", "asp.net"];

    public EditorAiRelevance(string instruction, string title, string activeText, string targetText, string headings)
    {
        var asked = EditorAiText.Words(instruction); var focused = EditorAiText.Words(targetText);
        var heading = EditorAiText.Words(headings); var live = EditorAiText.Words(activeText); var named = EditorAiText.Words(title);
        Add(asked.Take(64), 7); Add(focused.Take(96), 6); Add(heading.Take(96), 5); Add(live.Take(96), 3); Add(named.Take(32), 2);
        topics = FirstNonEmpty(asked, focused, heading, live, named, TopicTerms);
        languages = FirstNonEmpty(asked, focused, heading, live, named, LanguageTerms);
        // Always reserve query slots for the active subject, even when a long instruction contains many words.
        SearchTerms = topics.Concat(languages).Concat(weights.OrderByDescending(x => x.Value).Select(x => x.Key)).Distinct().Take(32).ToArray();
    }
    public string[] SearchTerms { get; }
    private void Add(IEnumerable<string> words, int weight) { foreach (var word in words) weights[word] = Math.Max(weights.GetValueOrDefault(word), weight); }
    private static HashSet<string> FirstNonEmpty(string[] asked, string[] focused, string[] heading, string[] live, string[] named, HashSet<string> allowed) =>
        new[] { asked, focused, heading, live, named }.Select(words => words.Where(allowed.Contains).ToHashSet(StringComparer.Ordinal)).FirstOrDefault(x => x.Count > 0) ?? [];

    public int MetadataScore(string title) => EditorAiText.Words(title).Sum(w => weights.GetValueOrDefault(w));

    public int Score(string title, string text)
    {
        var titleWords = EditorAiText.Words(title).ToHashSet(StringComparer.Ordinal);
        var bodyWords = EditorAiText.Words(text).ToHashSet(StringComparer.Ordinal);
        var all = titleWords.Concat(bodyWords).ToHashSet(StringComparer.Ordinal);
        // C++/Arduino, Python/ML and C# may all contain the word "code"; language disagreement is evidence against a source.
        var candidateLanguages = all.Where(LanguageTerms.Contains).ToHashSet(StringComparer.Ordinal);
        if (languages.Count > 0 && candidateLanguages.Count > 0 && !languages.Overlaps(candidateLanguages)) return 0;
        if (topics.Count > 0 && !topics.Overlaps(all)) return 0;
        var matches = all.Where(weights.ContainsKey).ToArray();
        if (matches.Length == 0 || topics.Count == 0 && !languages.Overlaps(all)
            && !matches.Any(w => weights[w] >= 5) && matches.Length < 2) return 0;
        return matches.Sum(w => weights[w] * (titleWords.Contains(w) ? 4 : 1)) + topics.Count(all.Contains) * 20;
    }

    public static (string Target, string Headings) LiveFocus(JsonElement document, JsonElement? target)
    {
        var blocks = EditorAiChangeValidation.Blocks(document);
        var focused = new List<string>();
        if (target is { ValueKind: JsonValueKind.Object } value)
        {
            if (value.TryGetProperty("selection", out var selection) && selection.ValueKind == JsonValueKind.Object
                && selection.TryGetProperty("text", out var selected) && selected.ValueKind == JsonValueKind.String) focused.Add(selected.GetString() ?? "");
            if (value.TryGetProperty("blockIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
                foreach (var id in ids.EnumerateArray()) if (id.ValueKind == JsonValueKind.String && blocks.TryGetValue(id.GetString()!, out var block)) focused.Add(EditorAiText.Plain(block));
        }
        var headings = string.Join('\n', blocks.Values.Where(b => b.TryGetProperty("type", out var type) && type.GetString() == "heading").Take(32).Select(EditorAiText.Plain));
        return (string.Join('\n', focused), headings);
    }
}
