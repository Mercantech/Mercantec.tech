using System.Globalization;
using System.Text;

namespace Mercantec.Tech.Web.Emojis;

public sealed record EmojiCategory(string Id, string Label);

public static class EmojiPickerCatalog
{
    public static IReadOnlyList<EmojiCategory> Categories { get; } =
    [
        new("all", "Alle"), new("custom", "Egne"), new("smileys", "Smileys"), new("gestures", "Hænder"),
        new("symbols", "Symboler"), new("learning", "Læring"), new("technology", "Teknik"), new("everyday", "Hverdag")
    ];

    public static IReadOnlyList<CustomEmojiItem> Filter(IEnumerable<CustomEmojiItem> items, string? query, string category)
    {
        var terms = Normalize(query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return items.Where(item => InCategory(item, category) && Matches(item, terms)).ToList();
    }

    private static bool InCategory(CustomEmojiItem item, string category) => category switch
    {
        "all" => true,
        "custom" => !item.IsStandard,
        _ => item.Category == category
    };

    private static bool Matches(CustomEmojiItem item, string[] terms)
    {
        if (terms.Length == 0) return true;
        var text = Normalize(item.Name + " " + item.AltText + " " + string.Join(' ', item.Keywords ?? []));
        return terms.All(term => text.Contains(term, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        var text = new StringBuilder();
        foreach (var character in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            text.Append(character switch
            {
                'æ' => "ae", 'ø' => "o", ':' or '_' or '-' => " ",
                _ when char.IsWhiteSpace(character) => " ",
                _ => character.ToString()
            });
        }
        return text.ToString();
    }
}
