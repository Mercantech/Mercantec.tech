namespace Mercantec.Tech.Api.Gifts;

// Old gifts predate theme tracking. Their sender's theme cannot be inferred from
// the Christmas-themed wrapping or from the theme currently active in a browser.
internal static class GiftThemes
{
    public const string Unknown = "unknown";
    public static bool IsKnown(string? theme) => theme is "system" or "light" or "dark" or "christmas" or "newyear" or "hacker";
}
