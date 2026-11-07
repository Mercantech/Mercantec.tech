namespace Mercantec.Tech.Web.Characters;

/// <summary>Shared editorial profiles; independent of accounts and course data.</summary>
public static class CharacterCatalog
{
    public static IReadOnlyList<CharacterProfile> All { get; } =
    [
        new("lupp", "LUPP", "Din AI-hjælper i LUP", "LUP · Notion-editoren", false,
            "Den lilla hjælper, du møder i editoren og med fyrværkerirøret i nytårstemaet.",
            "LUPP er LUPs AI-hjælper. I Notion-editoren hjælper han med at få idéer ned på siden, arbejde videre med en tekst og gøre sammenhænge synlige. Når vi skifter til nytårstemaet, er det også LUPP, der tager fyrværkerirøret frem.",
            ["Skrivehjælp", "Idéer", "Fyrværkeri"],
            [new("Nytår", "/images/newyear/lup-fireworks-player.png", "LUPP, en lille lilla lerfigur med store øjne og et fyrværkerirør")],
            [new("En hjælpende hånd i editoren", "Få hjælp til ny tekst, omskrivning og Mermaid-diagrammer. Du vælger selv, hvad du vil arbejde videre med."),
             new("Klar til nytår", "I nytårstemaet skyder LUPP fyrværkeri mod programmeringsfejl. Spillet startes frivilligt og gendanner siden, når du afslutter.")],
            [new("Åbn Notion-editoren", "/notion", false, true), new("Vælg nytårstema", "/settings", false, true)]),
        new("vatson", "Vatson", "Karakteren fra VATEK", "VATEK", true,
            "Et kendt ansigt fra vores emojis og en del af det kommende VATEK-projekt.",
            "Vatson hører til VATEK, som vi bygger i fremtiden. Allerede nu kan du møde hans udtryk som emojis i LUPs editor. Hans univers vokser med, når VATEK tager form.",
            ["VATEK", "Emojis", "På vej"],
            [new("Portræt", "/images/characters/vatson/vatson.webp", "Vatson, en lille hvid robot med et turkis skærmansigt og en åben ringmappe")],
            [new("Et projekt på vej", "VATEK er et kommende projekt. Vatsons rolle og univers får mere plads, efterhånden som vi bygger videre."),
             new("Små udtryk i hverdagen", "Vatson kan vinke, få en idé og give en tommel op. Hans emojis kan bruges i tekst og callouts i editoren.")],
            [new("Åbn Notion-editoren", "/notion", false, true)]),
        new("raymond", "Raymond", "Professor Codd · SQL-underviser", "SQL-H1", false,
            "Den blå elefantprofessor, der hjælper dig med SQL, relationer og databaser.",
            "Raymond hedder egentlig Raymond Stonebraker Codd, men du må gerne kalde ham Professor Codd. Han er den venlige SQL-underviser fra SQL-H1 og hjælper dig med at forstå databaser ét skridt ad gangen.",
            ["SQL", "PostgreSQL", "Rolige forklaringer"],
            [new("Portræt", "/images/characters/raymond/neutral.webp", "Raymond, en venlig blå elefantprofessor med runde bronzebriller"),
             new("SQL-underviser", "/images/characters/raymond/professor.webp", "Professor Codd som SQL-underviser"),
             new("Festtøj", "/images/characters/raymond/fancy.webp", "Raymond i sit fine festtøj"),
             new("Sommerferie", "/images/characters/raymond/summer-holiday.webp", "Raymond i sommertøj, klar til ferie")],
            [new("Forstå din SQL", "Raymond forklarer forespørgsler, relationer og fejl med små, konkrete eksempler. Han giver gradvise hints, så du selv kan finde frem til løsningen."),
             new("En professor med historie", "Hans fulde navn hylder databasepionererne Raymond Boyce, Michael Stonebraker og Edgar F. Codd. Du møder ham på SQL-H1, hvor SQL og PostgreSQL er i centrum.")],
            [new("Mød Raymond på SQL-H1", "https://sql.mercantec.tech", true, false), new("Udforsk databaser", "/subjects/database", false, false)])
    ];

    public static CharacterProfile? Find(string? slug) =>
        All.FirstOrDefault(character => string.Equals(character.Slug, slug, StringComparison.OrdinalIgnoreCase));
}

public sealed record CharacterProfile(string Slug, string Name, string Role, string Home, bool ComingSoon,
    string Summary, string Description, IReadOnlyList<string> Tags, IReadOnlyList<CharacterAppearance> Appearances,
    IReadOnlyList<CharacterFeature> Features, IReadOnlyList<CharacterLink> Links)
{
    public string Path => "/characters/" + Slug;
}

public sealed record CharacterAppearance(string Name, string Image, string Alt);
public sealed record CharacterFeature(string Title, string Description);
public sealed record CharacterLink(string Label, string Href, bool External, bool RequiresLogin);
