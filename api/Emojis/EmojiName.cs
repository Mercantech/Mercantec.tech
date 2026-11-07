using System.Text.RegularExpressions;

namespace Mercantec.Tech.Api.Emojis;

public static partial class EmojiName
{
    public const int MaxBytes = 256 * 1024;
    public const int MaxCount = 400;

    [GeneratedRegex("^[a-z0-9_-]{2,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidName();

    /// <summary>Kolon-navn som <c>:lup:</c> bliver til <c>lup</c>. Kun a-z, tal, _ og -.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var name = value.Trim().Trim(':').ToLowerInvariant();
        return ValidName().IsMatch(name) ? name : null;
    }
}
