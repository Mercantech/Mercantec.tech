using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Characters;

/// <summary>Shared editorial profiles; independent of accounts and course data.</summary>
public static class CharacterCatalog
{
    public static IReadOnlyList<CharacterProfile> All { get; } =
    [
        new("lupp", "LUPP", "Underviser i Game Design", "Game Design · LUP", false,
            "Den lilla Game Design-underviser, der hjælper dig fra den første spilidé til gameplay, styring og feedback.",
            "LUPP underviser i Game Design. Med blyanten klar hjælper han dig med at undersøge, hvad spilleren gør, hvem spillet er til, og hvordan styring og feedback hænger sammen. Han giver plads til flere idéer, før du vælger den, du vil afprøve. Du møder ham også som AI-hjælper i Notion-editoren og som fyrværkerifigur i nytårstemaet.",
            ["Game Design", "Spilidéer", "Gameplay og feedback"],
            [new("Game Design", "/images/characters/lupp/lupp-game-design.png", "LUPP, en lilla lerfigur med store øjne og en blyant, klar til at undervise i Game Design"),
             new("Nytår", "/images/newyear/lup-fireworks-player.png", "LUPP med et fyrværkerirør i nytårstemaet")],
            [new("Fra idé til spil", "LUPP hjælper med gameplay loops, målgrupper, styring, regler og balancen mellem held og færdigheder. Idéen samles i et mini-GDD, så andre kan forstå og afprøve den."),
             new("Vis, lyt og ret til", "Vis din spilidé, lyt til én styrke, ét spørgsmål og ét forslag, og vælg derefter en forbedring. Det er en del af designarbejdet at undersøge og prøve igen."),
             new("En hjælpende hånd i editoren", "I Notion-editoren hjælper LUPP med ny tekst, omskrivning og Mermaid-diagrammer. Du vælger selv, hvad du vil arbejde videre med."),
             new("Klar til nytår", "I nytårstemaet skyder LUPP fyrværkeri mod programmeringsfejl. Spillet startes frivilligt og gendanner siden, når du afslutter.")],
            [new("Se Game Design-forløbet", "/courses/template/game-design", false, false),
             new("Læs Game Design-lektionen", "https://mercantec.notion.site/Game-design-400dab5ca23780bbaa93fa9e9e9f2e6b?pvs=74", true, false),
             new("Åbn Notion-editoren", "/notion", false, true), new("Vælg nytårstema", "/settings", false, true)])
        {
            Reactions = ReactionsFor("lupp"),
            LargeReactionPreviews = true,
            ReactionsDescription = "LUPPs reaktioner fra Game Design: fra den første spilidé og skitse til playtest, feedback og et nyt forsøg. Find et motiv, åbn originalen, eller hent den som transparent PNG.",
            Illustrations =
            [
                new("Gameplay loop", "/images/characters/lupp/01-gameplay-loop.png", "LUPP viser et gameplay loop: se, handl, se resultatet og ret til.", 1536, 1024),
                new("Målgruppe og spillesituation", "/images/characters/lupp/02-maalgruppe-og-spillesituation.png", "LUPP undersøger, hvem der spiller, og om spillet bruges i køen, sammen eller med én hånd.", 1536, 1024),
                new("Fra input til handling", "/images/characters/lupp/03-fra-input-til-handling.png", "Illustration af vejen fra spillerens input gennem spillets regel til en handling og synlig feedback.", 1536, 1024),
                new("Sensorer og input", "/images/characters/lupp/04-sensorer-og-input.png", "LUPP præsenterer sensorer og mulige input til spil med Arduino Oplà som controller.", 1536, 1024),
                new("Output og feedback", "/images/characters/lupp/05-output-og-feedback.png", "Illustration af lys, lyd og skærm som feedback, der fortæller spilleren, hvad der skete.", 1536, 1024),
                new("Held og færdigheder", "/images/characters/lupp/06-held-og-faerdigheder.png", "LUPP sammenligner tilfældighed med timing, præcision og andre færdigheder i spil.", 1536, 1024),
                new("Spin en spilidé", "/images/characters/lupp/07-spin-en-spilide.png", "LUPP kombinerer gameplay, controller og målgruppe for at finde flere forskellige spilidéer.", 1536, 1024),
                new("Mini-GDD", "/images/characters/lupp/09-mini-gdd.png", "LUPP samler spilidé, spiller, regler, gameplay loop, styring, feedback og første version i et mini-GDD.", 1536, 1024),
                new("Præsentation og feedback", "/images/characters/lupp/10-praesentation-og-feedback.png", "LUPP viser sin idé, lytter til feedback og retter den til før næste afprøvning.", 1536, 1024)
            ]
        },
        new("vatson", "Vatson", "Digital guide til automation og teknologi", "VATek · Eksternt univers", false,
            "Den nysgerrige digitale guide til VATeks værktøjer, undervisningsmaterialer og teknologiske idéer.",
            "Vatson hjælper dig med at finde vej i VATeks værktøjer og viden. Han introducerer digitale læringsværktøjer og undervisningsmaterialer og hjælper med at skabe virksomhedsfortællinger, hvor fagligheden kan prøves af. Hans verden handler om at undersøge teknologi, stille spørgsmål og omsætte idéer til noget, man kan arbejde med.",
            ["VATek", "Teknologiforståelse", "Prototyper"],
            [new("Portræt", "/images/characters/vatson/vatson.webp", "Vatson, en lille hvid robot med et turkis skærmansigt og en åben ringmappe")],
            [new("Et andet hjemsted", "Vatson hører til VATek, som er Videnscenter for Automation og Teknologis digitale univers. VATek.dk og Videnscenterportalen er andre hjemmesider end LUP. Kasper arbejder ved videnscenteret. På Videnscenterportalen finder du centerets undervisningsmaterialer, aktiviteter og nyheder."),
             new("En nysgerrig værkstedsvært", "Som karakter er Vatson en nysgerrig og imødekommende værkstedsvært. Ringmappen er fuld af materialer, og næsten enhver løs idé får ham til at sige: Det kan vi da bygge en lille prototype af! Han stiller spørgsmål og inviterer dig til at undersøge, afprøve og bygge videre."),
             new("Fra viden til afprøvning", "Vatson viser vej mellem digitale værktøjer, undervisningsmaterialer og praktiske aktiviteter. I Virk som/lig hed hjælper han underviseren med at skabe fiktive virksomheder som ramme for elevernes faglige undersøgelser."),
             new("På besøg i LUP", "Her på LUP kan du møde Vatson som gæst og bruge hans emojis i tekst og callouts i editoren. Han kan vinke, få en idé og give en tommel op. Hans eget univers og værktøjer finder du hos VATek.")],
            [new("Besøg VATek.dk", "https://vatek.dk/", true, false),
             new("Find materialer på Videnscenterportalen", "https://videnscenterportalen.dk/vat/", true, false)])
        {
            Reactions = VatsonEmojiCatalog.All,
            LargeReactionPreviews = true,
            ReactionsDescription = "Alle Vatsons emojis i stor visning – fra vinken og kaffepause til PLC, VR og værkstedsbesøg. Åbn en original i fuld størrelse, eller hent den som PNG."
        },
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
        {
            Animations =
            [
                new("join-tables", "Forbind tabeller", "JOIN · Relationer", "Raymond viser vejen mellem to tabeller. Med en fælles nøgle kan en JOIN samle data, der hører sammen.",
                    "/images/characters/raymond/animations/join-tables.gif", "/images/characters/raymond/animations/join-tables.png", "Raymond forbinder to datatabeller med sin pegepind og følger de matchende nøgler.", 512, 512),
                new("sort-rows", "Sæt rækkerne i orden", "ORDER BY · Sortering", "Raymond ordner rækkerne efter deres værdi. ORDER BY bestemmer rækkefølgen i resultatet; data i tabellen bliver ikke flyttet.",
                    "/images/characters/raymond/animations/sort-rows.gif", "/images/characters/raymond/animations/sort-rows.png", "Raymond flytter nummererede rækkekort, så de ender i rækkefølgen et, to, tre.", 512, 512),
                new("find-row", "Find den manglende række", "WHERE · Undersøg data", "Raymond tager luppen frem, når en række mangler. Tjek først dine filtre: Matcher værdien, og bliver rækken udeladt af din WHERE-betingelse?",
                    "/images/characters/raymond/animations/find-row.gif", "/images/characters/raymond/animations/find-row.png", "Raymond holder en lup, undersøger en datatabel og peger på den tomme række.", 512, 512)
            ],
            Reactions = ReactionsFor("raymond"),
            LargeReactionPreviews = true,
            ReactionsDescription = "48 emojis og reaktioner med professoren – fra hverdagsfølelser og opmuntring til nøgler, forespørgsler og sikre databaser. Find et motiv, åbn originalen, eller hent den som PNG.",
            Scenes =
            [
                new("Biblioteket", "/images/characters/raymond/scenes/library.png", "Raymond læser i et hyggeligt træbibliotek med bøger og varmt lampelys.", 1536, 1024),
                new("Kodecaféen", "/images/characters/raymond/scenes/cafe.png", "Raymond arbejder med sin laptop og kaffe ved et cafévindue på en regnfuld aften.", 1536, 1024),
                new("Databaseobservatoriet", "/images/characters/raymond/scenes/observatory.png", "Raymond undersøger stjernehimlen fra et fantasifuldt rumobservatorium med teleskop og datakonstellationer.", 1536, 1024),
                new("Mysteriet om den tabte række", "/images/characters/raymond/scenes/detective-study.png", "Raymond undersøger et ufarligt SQL-mysterium med lup, sagsmappe og et opslag med spor.", 1536, 1024)
            ]
        },
        new("beaver", "Bæver", "Underviser i Sikkerhed og test · Blå team", "Sikkerhed og test", false,
            "Den rolige systembygger på blåt team: sikker kode, adgangskontrol, logs og tests, der holder løsningen robust.",
            "Bæver underviser i Sikkerhed og test fra det blå teams perspektiv. Med tablet og logbog undersøger Bæver, hvordan vi beskytter systemet, opdager fejl og gør rettelser holdbare. Arbejdet er roligt, grundigt og praktisk: forstå problemet, styrk løsningen, og skriv en test, der passer på den fremover.",
            ["Blå team", "Sikker kode", "Regressionstests"],
            [new("Portræt", "/images/characters/beaver/portrait.png", "Bæver, en venlig brun bæver med tydelig flad hale, blå underviserjakke og en tablet med logs")],
            [new("Hvorfor en bæver?", "Bæverens byggeri og vedligeholdelse er en god metafor for robuste systemer. Det handler om at bygge med omtanke, holde øje med svage steder og forstærke løsningen, når forholdene ændrer sig."),
             new("Sikkerhed, der kan efterprøves", "Bæver arbejder med sikre standardindstillinger, adgangskontrol, logging og automatiserede regressionstests. Når Ræv finder en svaghed, undersøger de sammen årsagen og kontrollerer, at rettelsen virker.")],
            [new("Mød Ræv på rødt team", "/characters/fox", false, false)])
        {
            Reactions = ReactionsFor("beaver"),
            LargeReactionPreviews = true,
            ReactionsDescription = "Bævers reaktioner fra det blå team: rolige forklaringer, opmuntring, adgangskontrol, logs og tests. Åbn en original i fuld størrelse, eller hent en transparent PNG."
        },
        new("fox", "Ræv", "Underviser i Sikkerhed og test · Rød team", "Sikkerhed og test", false,
            "Den nysgerrige sikkerhedstester på rødt team: undersøger angrebsflader, uventede input og systemets rettigheder.",
            "Ræv underviser i Sikkerhed og test fra det røde teams perspektiv. Med notesbog og testskitser udfordrer Ræv de antagelser, et system bygger på: Hvad hvis inputtet er anderledes, eller brugeren prøver en anden vej? Testen foregår inden for aftalte rammer, og fundene dokumenteres, så de kan forstås, rettes og testes igen.",
            ["Rød team", "Etisk sikkerhedstest", "Angrebsflader"],
            [new("Portræt", "/images/characters/fox/portrait.png", "Ræv, en venlig rødbrun ræv med busket hale, røde tøjdetaljer og en notesbog med testskitser")],
            [new("Hvorfor en ræv?", "Ræven repræsenterer opfindsomhed og evnen til at undersøge alternative veje. Det er en god metafor for etisk sikkerhedstest: at udfordre antagelser og finde grænsetilfælde, som den første løsning måske har overset."),
             new("Find svagheden, og del forståelsen", "Ræv arbejder med angrebsflader, uventede input og afprøvning af rettigheder. Ræv og Bæver er kolleger: De deler fund, styrker løsningen og skriver tests, der viser, om forbedringen holder.")],
            [new("Mød Bæver på blåt team", "/characters/beaver", false, false)])
        {
            Reactions = ReactionsFor("fox"),
            LargeReactionPreviews = true,
            ReactionsDescription = "Rævs reaktioner fra det røde team: nysgerrige spørgsmål, nye idéer, undersøgende tests og fund, der deles med kollegerne. Åbn en original i fuld størrelse, eller hent en transparent PNG."
        }
    ];

    public static CharacterProfile? Find(string? slug) =>
        All.FirstOrDefault(character => string.Equals(character.Slug, slug, StringComparison.OrdinalIgnoreCase));

    private static CharacterIllustration[] ReactionsFor(string slug) => CharacterEmojiCatalog.All
        .Where(asset => asset.CharacterSlug == slug)
        .Select(asset => new CharacterIllustration(asset.Title, asset.Image, asset.Alt, asset.Width, asset.Height)
        {
            Preview = asset.Preview
        }).ToArray();
}

public sealed record CharacterProfile(string Slug, string Name, string Role, string Home, bool ComingSoon,
    string Summary, string Description, IReadOnlyList<string> Tags, IReadOnlyList<CharacterAppearance> Appearances,
    IReadOnlyList<CharacterFeature> Features, IReadOnlyList<CharacterLink> Links)
{
    public string Path => "/characters/" + Slug;
    public IReadOnlyList<CharacterIllustration> Illustrations { get; init; } = [];
    public IReadOnlyList<CharacterIllustration> Reactions { get; init; } = [];
    public bool LargeReactionPreviews { get; init; }
    public string? ReactionsDescription { get; init; }
    public IReadOnlyList<CharacterIllustration> Scenes { get; init; } = [];
    public IReadOnlyList<CharacterAnimation> Animations { get; init; } = [];
    public IEnumerable<CharacterMaterialGroup> Materials
    {
        get
        {
            if (Reactions.FirstOrDefault() is { } reaction)
                yield return new("emojis", Reactions.Count, "character-reactions-title", reaction.Preview, reaction.Alt);
            if (Scenes.FirstOrDefault() is { } scene)
                yield return new("miljøer", Scenes.Count, "character-scenes-title", scene.Image, scene.Alt);
            if (Illustrations.FirstOrDefault() is { } illustration)
                yield return new("illustrationer", Illustrations.Count, "character-gallery-title", illustration.Image, illustration.Alt);
            if (Animations.FirstOrDefault() is { } animation)
                yield return new("GIF-animationer", Animations.Count, "character-animations-title", animation.Poster, animation.Alt);
        }
    }
}

public sealed record CharacterMaterialGroup(string Label, int Count, string SectionId, string Preview, string Alt);
public sealed record CharacterAppearance(string Name, string Image, string Alt);
public sealed record CharacterFeature(string Title, string Description);
public sealed record CharacterLink(string Label, string Href, bool External, bool RequiresLogin);
public sealed record CharacterIllustration(string Title, string Image, string Alt, int Width, int Height)
{
    public string Preview { get; init; } = Image;
}
public sealed record CharacterAnimation(string Id, string Title, string Topic, string Description, string Image, string Poster, string Alt, int Width, int Height);
