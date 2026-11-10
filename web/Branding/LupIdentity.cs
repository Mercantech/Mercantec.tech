namespace Mercantec.Tech.Web.Branding;

public sealed record LupExpansion(string Name, string Description, bool IsOfficial = false);

public static class LupIdentity
{
    public const string OfficialName = "Lokal UndervisningsPlatform";

    public static IReadOnlyList<LupExpansion> Expansions { get; } = Array.AsReadOnly<LupExpansion>(
    [
        new(OfficialName, "Når nogen fra ledelsen spørger.", true),
        new("Læring Uden Panik", "Når du finder dagens opgave på første klik."),
        new("Lidt Uventet Pædagogik", "Når en hurtig quest ender med, at nogen bygger et helt spil."),
        new("Lort, Underviseren Panikker", "Når vi siger »det virkede lige før« foran hele holdet.")
    ]);

    // Most visits use the official name. A playful name stays for the whole visit.
    public static string NameForVisit() => Random.Shared.Next(5) == 0
        ? Expansions[Random.Shared.Next(1, Expansions.Count)].Name
        : OfficialName;
}
