using System.Globalization;
using System.Text.RegularExpressions;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Electives;

internal static partial class ElectiveDisplay
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Copenhagen).DateTime);

    public static string Date(DateOnly value) => value.ToString("d. MMM yyyy", Danish);

    public static string DateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Copenhagen).ToString("d. MMM yyyy 'kl.' HH:mm", Danish);

    public static string? Period(DateOnly? start, DateOnly? end) => (start, end) switch
    {
        ({ } first, { } last) when first != last => $"{Date(first)} – {Date(last)}",
        ({ } first, _) => Date(first),
        (_, { } last) => $"Til og med {Date(last)}",
        _ => null
    };

    public static string? EnrollmentPeriod(DateOnly? start, DateOnly? end) =>
        start is { } first && end is null ? $"Fra {Date(first)}" : Period(start, end);

    public static bool IsPast(ElectiveView elective) =>
        elective.Status == "archived" || (elective.EndsOn ?? elective.StartsOn) < Today;

    public static string Status(string status) => status switch
    {
        "template" => "Fagkatalog",
        "draft" => "Kladde",
        "open" => "Åben",
        "closed" => "Lukket",
        "archived" => "Arkiveret",
        _ => "Ukendt status"
    };

    public static (string Label, string Tone) Registration(ElectiveView elective)
    {
        if (elective.IsEnrolled)
            return ("Du er tilmeldt", "enrolled");
        if (IsPast(elective))
            return ("Tidligere hold", "neutral");
        if (elective.Status != "open" || elective.RegistrationClosesOn < Today)
            return ("Tilmelding lukket", "neutral");
        if (elective.RegistrationOpensOn is { } opens && opens > Today)
            return ($"Åbner {Date(opens)}", "neutral");
        if (elective.Capacity is { } capacity && elective.EnrollmentCount >= capacity)
            return ("Fyldt", "neutral");
        return ("Tilmelding åben", "enrolled");
    }

    public static string? SafeSourceUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Length > 0 && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;

    public static IEnumerable<(string Text, string? Href)> TextLinks(string text)
    {
        var position = 0;
        foreach (Match match in HttpsLinks().Matches(text))
        {
            var url = match.Value.TrimEnd('.', ',', ')', ']', ';');
            if (SafeSourceUrl(url) is not { } href)
                continue;

            if (match.Index > position)
                yield return (text[position..match.Index], null);
            yield return (url, href);
            position = match.Index + url.Length;
        }

        if (position < text.Length)
            yield return (text[position..], null);
    }

    [GeneratedRegex(@"https://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpsLinks();
}
