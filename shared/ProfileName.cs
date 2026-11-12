using System.Text.Json.Serialization;

namespace Mercantec.Tech.Contracts;

public sealed record ProfileNameDto(string? DisplayName);

public sealed record UpdateProfileNameRequest([property: JsonRequired] string? DisplayName);

public static class ProfileNameRules
{
    public const int MaxLength = 60;
    public const string SubjectHeader = "X-LUP-Profile-Subject";

    public static bool TryNormalize(string? value, out string? normalized)
    {
        normalized = null;
        if (value is null)
            return true;

        if (value.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029'))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length is 0 or > MaxLength)
            return false;

        normalized = trimmed;
        return true;
    }
}
