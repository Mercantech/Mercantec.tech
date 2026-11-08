using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Updates;

/// <summary>Curated release notes and private screenshots, shipped together with the API.</summary>
public static class PlatformUpdatesCatalog
{
    public static IReadOnlyList<PlatformUpdate> Entries { get; } = Array.AsReadOnly<PlatformUpdate>(
    [
        new("characters", new(2026, 11, 7), "Raymond viser SQL i bevægelse",
            "Karakteren Raymond har fået små animationer om JOIN, ORDER BY og WHERE. Afspil dem på profilen, eller hent en GIF til undervisningen.",
            new("characters", "Raymonds tre SQL-animationer om JOIN, ORDER BY og WHERE.")),
        new("notebook", new(2026, 11, 7), "En notesbog til noter og logbog",
            "Elever kan vælge en daglig, ugentlig eller fri skabelon til deres noter og logbog. Undervisere kan følge arbejdet og give kommentarer.",
            new("notebook", "Elevens notesbog med logbog, notesider og valg af skabelon.")),
        new("roulette", new(2026, 11, 7), "Gruppedannelse med lidt mere spænding",
            "Fordel holdet med grupperoulette, og afslør én gruppe ad gangen. Gem grupperne, og brug dem videre i holdets opgaver.",
            new("roulette", "Grupperoulettens afsløring af en ny gruppe fra holdet.")),
        new("notification-dropdown", new(2026, 11, 7), "Det næste skridt ligger i klokken",
            "Åbn klokken for at se opgaver, der kræver din opmærksomhed. Gå direkte videre til afleveringen eller aktiviteten fra beskeden.",
            new("notification-dropdown", "Notifikationsklokken åbnet med opgaver og direkte handlinger.")),
        new("library", new(2026, 11, 6), "Undervisningen har fået et bibliotek",
            "Find undervisningsplaner, fag, projekter og badgekriterier fra samme sted. Biblioteket giver et samlet overblik, når du leder efter inspiration eller næste emne.",
            new("library", "Biblioteket med indgange til undervisningsplaner, fag, projekter og badges.")),
        new("hold-navigation", new(2026, 11, 6), "Mere ro på holdet",
            "Holdets faner samler undervisning, quests, kalender, grupper og materialer. Afstemninger ligger sammen med undervisningen, så klassens aktiviteter er lettere at finde.",
            new("hold-navigation", "Holdets oversigt med den forenklede navigation og faner til undervisningen.")),
        new("published-pages", new(2026, 11, 5), "Skriv sammen, udgiv når det er klar",
            "Samarbejd om undervisningssider og undersider, og udgiv dem til holdet. Eleverne læser den udgivne version, mens du kan arbejde videre med kladden.",
            new("published-pages", "Forløbseditoren med undervisningsside og udgivelse til holdet.")),
        new("poll-studio", new(2026, 11, 5), "Giv klassen en stemme",
            "Spørg med et stemningstjek, fælles valg, en skala eller et kort svar. Se klassens samlede svar, og åbn resultaterne på tavlen.",
            new("poll-studio", "Afstemningsværkstedet med spørgsmål, svarmuligheder og elevens forhåndsvisning.")),
        new("gift-opening", new(2026, 11, 5), "Julepost med personlig indpakning",
            "Send en hilsen til en anden bruger, og lad modtageren åbne sin egen pakke. Den nye gaveåbning giver hilsenen lidt mere julestemning.",
            new("gift-after", "Den nye personlige gaveåbning med pakke og hilsen."),
            new("gift-before", "Den tidligere gaveåbning før den nye personlige scene.")),
        new("newyear", new(2026, 11, 5), "Programmeringsfejl møder fyrværkeri",
            "Nytårstemaets lilla figur skyder fyrværkeri mod faldende programmeringsfejl. Saml boosts, kombiner dem, og gendan siden, når spillet er slut.",
            new("newyear", "Nytårsspillets lilla figur, faldende fejl og fyrværkeri.")),
        new("student-front", new(2026, 11, 5), "Elevens dag samlet på forsiden",
            "Elevforsiden samler det aktive forløb, genveje og den personlige udvikling. Lectio, VUF og studieplan får plads i et kompakt panel ved siden af undervisningen.",
            new("student-after", "Elevforsiden med aktivt forløb, genveje og kompakt Lectio- og VUF-panel."),
            new("student-before", "Den tidligere elevforside før indholdet blev samlet i det nye layout.")),
        new("avatar", new(2026, 11, 4), "Gør avataren til din egen",
            "Vælg udseende og tøj til din avatar, og tag den med ind i Rune Run Mini. Dine garderobe- og udseendevalg gemmes i din browser.",
            new("avatar", "Avatareditoren med udseendevalg og garderobe.")),
        new("apartment", new(2026, 11, 4), "Et lille sted til store idéer",
            "Indret dit rum med møbler fra inventaret, flyt dem rundt, og prøv en ny indretning. Dit rum og dine lokale butikskøb gemmes i din browser.",
            new("apartment", "Lejligheden med møbler, inventar og placering i rummet."))
    ]);

    private static readonly HashSet<string> ImageIds = Entries
        .SelectMany(update => update.Before is { } before ? new[] { update.After.Id, before.Id } : [update.After.Id])
        .ToHashSet(StringComparer.Ordinal);

    public static Stream? OpenImage(string id)
    {
        // Only images referenced by these notes are addressable; request paths never reach the filesystem.
        if (!ImageIds.Contains(id)) return null;
        return typeof(PlatformUpdatesCatalog).Assembly.GetManifestResourceStream($"platform-updates/{id}.webp");
    }
}
