namespace Mercantec.Tech.Web.Demo;

public static class DemoCatalog
{
    public const int Level = 7;
    public const int Xp = 1240;
    public const int Gold = 320;

    public static readonly Course[] Courses =
    [
        new("h3", "H3 · 2026", "Fra sensor og Arduino til API, database og dashboard. Et fælles projekt binder fagene sammen.", "9 uger", "Kasper & Mathias",
        [
            "Arduino og IoT", "Mere Arduino", "Serverside", "Mere serverside", "Projektuge", "Test og sikkerhed", "MVP færdig", "Projektuge", "Eksamen"
        ],
        [
            new("iot", "Arduino & IoT", "Byg med hardware, skriv struktureret C++, og forbind dine enheder."),
            new("server", "Serverside", "Udvikl backend, der modtager data og forbinder resten af løsningen."),
            new("database", "Database", "Giv målingerne en struktur, og gem dem med en ORM."),
            new("dashboard", "Frontend & dashboard", "Gør enhedernes data forståelige og mulige at handle på."),
            new("kvalitet", "Test & sikkerhed", "Afprøv forbindelser, fejltilstande og adgang i hele kæden."),
            new("proces", "Projekt & dokumentation", "Planlæg, afprøv og forklar jeres samlede løsning.")
        ]),
        new("game", "Game design fra bunden", "Byg Rune Run sammen, undersøg designbegreber, og brug erfaringerne i dit eget spil.", "3 uger", "Kasper",
        [
            "Rune Run og fælles teori", "Eget spil og side quests", "Afslutning og aflevering"
        ],
        [
            new("design", "Game Design", "Regler, valg og oplevelse. Syv guider fra fælles spil til egen idé.")
        ])
    ];

    public static readonly Quest[] Quests =
    [
        new("maskine", "Miniprojekt · Fyld maskinen", "Main", "h3", "Vis en afgrænset løsning med Arduino-input og en synlig reaktion."),
        new("controller", "Arduino som spilcontroller", "Main", "h3", "Lad spillerens handling på en Arduino påvirke et spil gennem frontend og backend."),
        new("iot-platform", "Interaktiv IoT-platform", "Main", "h3", "Saml sensor, API, database og dashboard i én løsning."),
        new("h3-mqtt-debug", "Find fejlen i et MQTT-topic", "Side", "h3", "Afprøv et forkert topic, find fejlen, og forklar hvad der afslørede den."),
        new("h3-journal", "Skriv dagens tre linjer", "Daily", "h3", "Hvad lavede du? Hvad lærte du? Hvad er dit næste skridt?"),
        new("rr-gdd", "Rune Run GDD · første udgave", "Main", "game", "Udfyld punkt 1–3 i den fælles Rune Run-skabelon før byggeriet."),
        new("own-gdd", "Indledende GDD til eget spil", "Main", "game", "Aflevér punkt 1–4. Vent på godkendelse, før du starter eget spil."),
        new("prototype", "Spilbar prototype", "Main", "game", "Aflevér en version, der kan prøves fra start til afslutning."),
        new("feedback", "Test en andens spil", "Daily", "game", "Spil selv en andens version. Gem én observation og ét forslag."),
        new("menu", "Lav en main menu", "Side", "game", "Byg start og en vej tilbage fra spillet.")
    ];

    public static readonly string[] RankNames = ["Lille", "Mellem", "Stor", "Mega", "Ultra mega"];
    public static readonly string[] RankCriteria = ["Forklar emnet med dine egne ord.", "Brug emnet i et fælles projekt.", "Brug det i dit eget projekt, og vis det til underviseren.", "Præsentér emnet for klassen.", "Lær emnet fra dig, så andre kan bruge det."];

    public static readonly Badge[] Badges =
    [
        new("loop", "Core Loop", "Game Design", "Forklar, hvad spilleren gør igen og igen.", 2, "Mellem"),
        new("playtest", "Playtester", "Game Design", "Adskil observation fra fortolkning.", 1, "Lille"),
        new("gdd", "Game Designer", "Game Design", "Forbind pitch, scope og et afprøveligt spildesign.", 0, null),
        new("mqtt", "MQTT Explorer", "Arduino & IoT", "Følg en besked fra enhed til broker.", 0, null),
        new("cpp", "C++ Bygger", "Arduino & IoT", "Strukturér Arduino-kode med funktioner og flere filer.", 0, null),
        new("friday", "Fancy Friday", "Fællesskab", "Et særligt fællesskabsbadge, som underviseren aftaler kriterier for.", 0, null),
        new("api", "API Architect", "Serverside", "Design tydelige endpoints og håndtér fejl.", 3, "Stor"),
        new("sql", "Data Designer", "Database", "Modellér relationer og gem dine målinger.", 2, "Mellem"),
        new("git", "Git Together", "Samarbejde", "Brug branches, commits og review i dit team.", 1, "Lille"),
        new("security", "Security Mindset", "Test & sikkerhed", "Beskyt adgang, og undersøg fejltilstande.", 0, null),
        new("ui", "Interface Builder", "Frontend", "Gør data forståelige og mulige at handle på.", 0, null),
        new("docs", "Fortæl med kode", "Dokumentation", "Forklar løsningen, så andre kan arbejde videre.", 0, null),
        new("hawaii", "Hawaii", "Fællesskab", "Åbner Hawaii-skjorten i butikken. Gives ikke automatisk.", 0, null)
    ];

    public static readonly GiftItem Gift = new("Opslagstavle", 4, "Gives direkte, når level nås. Den står ikke til salg.");

    public static readonly Unlock[] Unlocks =
    [
        new("Titel", "Systembygger", 2, true),
        new("Tema", "Nordlys", 3, true),
        new("Møbel", "Opslagstavle", 4, true),
        new("Titel", "Mentor", 9, false),
        new("Tema", "Kobber", 9, false)
    ];

    public static readonly Project[] Projects =
    [
        new("rune", "Rune Run · To veje", "Et kort spil med en sikker omvej og en hurtig rute med risiko.", "Game Design", "Alex Jensen", "Fælles", 6,
        [
            new("Maja Holm", "Den hurtige rute føles fair, fordi farefelterne er synlige, før man vælger.")
        ]),
        new("iot", "Sensor til dashboard", "En lille prototype, der forbinder Arduino, backend og en tydelig visning.", "H3 · IoT", "Alex Jensen", "Privat", 1, [])
    ];

    public static readonly Poll[] Polls =
    [
        new("H3 · 2026", "Hvad skal vi bruge mere tid på?", "Valg", ["API og requests", "Database og målinger", "Realtime med SignalR"], [4, 7, 3]),
        new("Game Design", "Hvordan er du landet i dag?", "Stemning", ["Glad", "Klar", "Midt imellem", "Træt", "Presset"], [3, 6, 4, 2, 1])
    ];

    public static readonly Score[] Highscores =
    [
        new("Alex Jensen", "Rune Run · To veje", 1840),
        new("Maja Holm", "Rune Run · To veje", 1710),
        new("Noah Berg", "Rune Run · To veje", 1495)
    ];

    public static readonly string[] FarmerQuests =
    [
        "Gentag en handling, til marken er høstet.",
        "Brug en betingelse, så dronen kun høster modne afgrøder.",
        "Stop løkken, når lageret er fuldt."
    ];

    public static readonly EducationStep[] Education =
    [
        new("H1 · Fundamentet", "10 uger", "C#, OOP, SQL, HTTP, Blazor og projektarbejde."),
        new("H2 · Fullstack", "10 uger", "EF Core, REST API, Blazor og avancerede relationer."),
        new("H3 · IoT & dashboard", "9 uger", "Arduino, .NET API, database, dashboards og sikkerhed."),
        new("H4 · Mobiludvikling", "7 uger", "Flutter/Dart, API-integration, offline data og test."),
        new("H5 · Drift & data", "9 uger", "Linux, Docker, embedded, pipelines og mini-svendeprøve."),
        new("H6 · Svendeprøve", "5 uger", "Afsluttende projekt, dokumentation og individuel fremlæggelse.")
    ];

    public static readonly string[] Journal =
    [
        "Jeg fik en måling fra Arduino frem til API’et, men dashboardet viste rå JSON.",
        "Næste gang vil jeg vise enhed og tidspunkt, før jeg tilføjer en graf."
    ];

    public sealed record Course(string Id, string Name, string Desc, string Duration, string Teacher, string[] Weeks, Subject[] Subjects);
    public sealed record Subject(string Id, string Name, string Desc);
    public sealed record Quest(string Id, string Name, string Type, string Course, string Desc);
    public sealed record Badge(string Id, string Name, string Topic, string Desc, int Rank, string? RankName)
    {
        public bool Special => Id is "hawaii" or "friday";
        public string Icon => Id switch { "loop" => "loop", "playtest" => "test", "gdd" => "game", "mqtt" => "wifi", "cpp" => "chip", "api" => "code", "sql" => "database", "security" => "shield", "hawaii" => "flower", "friday" => "shirt", "git" => "loop", "ui" => "chart", _ => "book" };
    }
    public sealed record GiftItem(string Name, int Level, string Desc);
    public sealed record Unlock(string Type, string Name, int Level, bool Open);
    public sealed record Project(string Id, string Name, string Desc, string Tag, string Owner, string Visibility, int Likes, Comment[] Comments);
    public sealed record Comment(string Author, string Text);
    public sealed record Poll(string Course, string Question, string Type, string[] Options, int[] Counts);
    public sealed record Score(string Name, string Game, int Points);
    public sealed record EducationStep(string Name, string Duration, string Desc);
}
