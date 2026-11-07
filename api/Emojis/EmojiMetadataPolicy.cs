using System.Text.Json;
using Mercantec.Tech.Api.Data.Entities;

namespace Mercantec.Tech.Api.Emojis;

public sealed record EmojiResolvedMetadata(string AltText, string Category, string[] Keywords);

public static class EmojiMetadataPolicy
{
    public const int MaxAltText = 240;
    public const int MaxKeywords = 12;
    public const int MaxKeywordLength = 32;
    public static readonly IReadOnlySet<string> Categories = new HashSet<string>(StringComparer.Ordinal)
        { "custom", "smileys", "gestures", "symbols", "learning", "technology", "everyday" };

    public static string CleanText(string? text, int limit)
    {
        var clean = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= limit ? clean : clean[..limit].TrimEnd();
    }

    public static string Category(string? value) =>
        value is not null && Categories.Contains(value.Trim().ToLowerInvariant()) ? value.Trim().ToLowerInvariant() : "custom";

    public static string[] Keywords(IEnumerable<string>? values) =>
        (values ?? []).Where(value => value is not null).Select(value => CleanText(value, MaxKeywordLength).ToLowerInvariant())
            .Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).Take(MaxKeywords).ToArray();

    public static string[] ReadKeywords(string? json)
    {
        try { return Keywords(JsonSerializer.Deserialize<string[]>(json ?? "[]")); }
        catch (JsonException) { return []; }
    }

    public static EmojiResolvedMetadata Resolve(CustomEmoji emoji)
    {
        var standard = StandardEmojiMetadata.Find(emoji.Key);
        var keywords = ReadKeywords(emoji.KeywordsJson);
        var category = Category(emoji.Category);
        var alt = CleanText(emoji.AltText, MaxAltText);
        if (standard is not null)
        {
            if (category == "custom") category = standard.Category;
            if (keywords.Length == 0) keywords = standard.Keywords;
            if (alt.Length == 0) alt = StandardAltText(emoji.Key);
        }
        // Old uploads remain usable even before they receive richer metadata.
        if (alt.Length == 0) alt = emoji.Name.Replace('_', ' ');
        return new(alt, category, keywords);
    }

    private static string StandardAltText(string key) => key switch
    {
        "standard-emojis/1f604.png" => "Smilende ansigt med lukkede øjne",
        "standard-emojis/1f601.png" => "Bredt smilende ansigt",
        "standard-emojis/1f602.png" => "Ansigt der griner med glædestårer",
        "standard-emojis/1f609.png" => "Blinkende ansigt",
        "standard-emojis/1f60d.png" => "Smilende ansigt med hjerteøjne",
        "standard-emojis/1f914.png" => "Tænkende ansigt med hånd ved hagen",
        "standard-emojis/1f62e.png" => "Overrasket ansigt med åben mund",
        "standard-emojis/1f614.png" => "Trist ansigt med sænket blik",
        "standard-emojis/1f622.png" => "Grædende ansigt med en tåre",
        "standard-emojis/1f60e.png" => "Smilende ansigt med solbriller",
        "standard-emojis/1f44f.png" => "Klappende hænder",
        "standard-emojis/1f44d.png" => "Hånd med tommelfingeren opad",
        "standard-emojis/1f44e.png" => "Hånd med tommelfingeren nedad",
        "standard-emojis/1f44b.png" => "Vinkende hånd",
        "standard-emojis/1f91d.png" => "To hænder giver håndtryk",
        "standard-emojis/2764.png" => "Rødt hjerte",
        "standard-emojis/2b50.png" => "Gul stjerne",
        "standard-emojis/2728.png" => "Glimtende stjerner",
        "standard-emojis/1f389.png" => "Festligt konfettirør",
        "standard-emojis/1f525.png" => "Orange flamme",
        "standard-emojis/2705.png" => "Hvidt flueben på grøn baggrund",
        "standard-emojis/274c.png" => "Rødt kryds",
        "standard-emojis/26a0.png" => "Gult advarselsskilt",
        "standard-emojis/2753.png" => "Rødt spørgsmålstegn",
        "standard-emojis/2139.png" => "Blåt informationssymbol",
        "standard-emojis/1f3af.png" => "Dartpil i midten af en målskive",
        "standard-emojis/1f3c6.png" => "Gylden pokal",
        "standard-emojis/1f4a1.png" => "Lysende elektrisk pære",
        "standard-emojis/1f4d6.png" => "Åben bog",
        "standard-emojis/1f4da.png" => "Stak af bøger",
        "standard-emojis/270f.png" => "Blyant",
        "standard-emojis/1f4dd.png" => "Papir og blyant",
        "standard-emojis/1f680.png" => "Raket på vej op",
        "standard-emojis/1f4bb.png" => "Bærbar computer",
        "standard-emojis/1f6e0.png" => "Hammer og skruenøgle",
        "standard-emojis/1f41b.png" => "Grøn larve",
        "standard-emojis/1f512.png" => "Lukket hængelås",
        "standard-emojis/1f4c5.png" => "Kalender",
        "standard-emojis/23f0.png" => "Vækkeur",
        "standard-emojis/2615.png" => "Varm kop kaffe",
        _ => "Emoji"
    };
}
