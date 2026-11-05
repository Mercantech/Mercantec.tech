using System.Text.RegularExpressions;

namespace Mercantec.Tech.Web.Emojis;

public readonly record struct EmojiTextPart(string Text, CustomEmojiItem? Emoji);

public static partial class EmojiTextParts
{
    [GeneratedRegex(@"(?<![\p{L}\p{N}_]):([a-zA-Z0-9_]{2,32}):", RegexOptions.CultureInvariant)]
    private static partial Regex Shortcode();

    public static IReadOnlyList<EmojiTextPart> Split(string? text, IEnumerable<CustomEmojiItem> emojis)
    {
        text ??= "";
        var map = new Dictionary<string, CustomEmojiItem>(StringComparer.Ordinal);
        foreach (var emoji in emojis)
            map.TryAdd(emoji.Name, emoji);

        var parts = new List<EmojiTextPart>();
        var index = 0;
        foreach (Match match in Shortcode().Matches(text))
        {
            if (!map.TryGetValue(match.Groups[1].Value.ToLowerInvariant(), out var emoji)) continue;
            if (match.Index > index) parts.Add(new(text[index..match.Index], null));
            parts.Add(new("", emoji));
            index = match.Index + match.Length;
        }
        if (index < text.Length || parts.Count == 0) parts.Add(new(text[index..], null));
        return parts;
    }
}
