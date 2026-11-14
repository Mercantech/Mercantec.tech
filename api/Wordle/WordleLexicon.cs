using System.Text;
using System.Text.Json;
using Mercantec.Tech.Web.Characters;

namespace Mercantec.Tech.Api.Wordle;

internal sealed record WordleAnswer(string Host, string Word, string Explanation);

public sealed class WordleLexicon
{
    internal static readonly string[] HostOrder =
        ["lupp", "vatson", "gummianden", "raymond", "bilbo", "chameleon", "whale", "bee", "bat", "beaver", "fox"];

    private static readonly Dictionary<string, string> Themes = new(StringComparer.Ordinal)
    {
        ["lupp"] = "Spildesign",
        ["vatson"] = "Automation og teknik",
        ["gummianden"] = "Fejlfinding og læring",
        ["raymond"] = "Databaser og SQL",
        ["bilbo"] = "Backend og programmering",
        ["chameleon"] = "Maskinlæring og data",
        ["whale"] = "Containere og deployment",
        ["bee"] = "Cybersikkerhed",
        ["bat"] = "IoT og elektronik",
        ["beaver"] = "Test og robusthed",
        ["fox"] = "Sikkerhed og problemløsning"
    };

    private readonly HashSet<string> guesses;
    private readonly IReadOnlyDictionary<string, WordleAnswer[]> answersByHost;

    internal WordleLexicon(IEnumerable<WordleAnswer> answers, IEnumerable<string> allowedGuesses,
        bool verifyRelease = false)
    {
        var list = answers.ToArray();
        if (list.Length == 0 || list.DistinctBy(answer => answer.Word).Count() != list.Length
            || list.Any(answer => Normalize(answer.Word) is not { } normalized || normalized != answer.Word
                || string.IsNullOrWhiteSpace(answer.Host) || !Themes.ContainsKey(answer.Host)
                || string.IsNullOrWhiteSpace(answer.Explanation) || answer.Explanation.Length > 500))
            throw new InvalidDataException("Wordle contains an invalid or duplicate solution.");

        answersByHost = list.GroupBy(answer => answer.Host).ToDictionary(group => group.Key, group => group.ToArray());
        if (verifyRelease && (list.Length != 960 || Themes.Count != HostOrder.Length
            || !HostOrder.All(host => CharacterCatalog.Find(host) is { ComingSoon: false })
            || HostOrder.Where((host, index) => !answersByHost.TryGetValue(host, out var words)
                || words.Length != (index < 3 ? 88 : 87)).Any()))
            throw new InvalidDataException("Wordle needs 960 unique solutions distributed among its published characters.");

        guesses = allowedGuesses.Where(word => Normalize(word) == word).ToHashSet(StringComparer.Ordinal);
        if (list.Any(answer => !guesses.Contains(answer.Word)))
            throw new InvalidDataException("Every Wordle solution must also be present in the guess dictionary.");
    }

    internal static WordleLexicon Load()
    {
        using var answers = Resource("wordle/answers.json");
        var list = JsonSerializer.Deserialize<WordleAnswer[]>(answers, JsonSerializerOptions.Web)
            ?? throw new InvalidDataException("Missing Wordle solutions.");
        using var guesses = new StreamReader(Resource("wordle/guesses.txt"));
        return new WordleLexicon(list, Lines(guesses), verifyRelease: true);
    }

    internal WordleAnswer[] HostAnswers(int dayNumber, out int occurrence)
    {
        var hosts = HostOrder.Where(answersByHost.ContainsKey).ToArray();
        occurrence = dayNumber / hosts.Length;
        return answersByHost[hosts[dayNumber % hosts.Length]];
    }

    internal static string Theme(string host) => Themes[host];

    internal bool Allows(string guess) => guesses.Contains(guess);

    internal static string? Normalize(string? value)
    {
        if (value is null || value.Length > 32) return null;
        string word;
        try { word = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant(); }
        catch (ArgumentException) { return null; }
        return word.Length == 5 && word.All(letter => letter is >= 'a' and <= 'z' or 'æ' or 'ø' or 'å') ? word : null;
    }

    private static Stream Resource(string name) => typeof(WordleLexicon).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException($"Missing Wordle resource {name}.");

    private static IEnumerable<string> Lines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}
