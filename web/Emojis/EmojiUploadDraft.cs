using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mercantec.Tech.Web.Emojis;

public static partial class EmojiUploadDraft
{
    private const int MaxNameLength = 32;

    public static string SuggestName(string fileName, IEnumerable<string> existingNames)
    {
        var leaf = fileName.Replace('\\', '/').Split('/').Last();
        var dot = leaf.LastIndexOf('.');
        if (dot >= 0) leaf = leaf[..dot];
        var stem = new StringBuilder();
        foreach (var character in leaf.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (character == 'æ') stem.Append("ae");
            else if (character == 'ø') stem.Append('o');
            else if (character is >= 'a' and <= 'z' or >= '0' and <= '9') stem.Append(character);
            else if (stem.Length > 0 && stem[^1] != '_') stem.Append('_');
        }
        var name = stem.ToString().Trim('_');
        if (name.Length > MaxNameLength) name = name[..MaxNameLength].TrimEnd('_');
        if (name.Length < 2) name = "emoji";

        var names = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.Contains(name)) return name;
        for (var index = 2; ; index++)
        {
            var suffix = "_" + index.ToString(CultureInfo.InvariantCulture);
            var candidate = name[..Math.Min(name.Length, MaxNameLength - suffix.Length)].TrimEnd('_') + suffix;
            if (!names.Contains(candidate)) return candidate;
        }
    }

    public static string? NormalizeName(string? name)
    {
        var value = name?.Trim().Trim(':').ToLowerInvariant();
        return value is not null && ValidName().IsMatch(value) ? value : null;
    }

    public static bool IsValidName(string? name) => NormalizeName(name) is not null;

    public static IReadOnlyList<string> ParseKeywords(string? value) => (value ?? "")
        .Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    [GeneratedRegex("^[a-z0-9_]{2,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidName();
}
