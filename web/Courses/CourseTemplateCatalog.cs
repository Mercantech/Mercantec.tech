namespace Mercantec.Tech.Web.Courses;

/// <summary>
/// Fælles kursusskabelon for LUP-undervisningsplaner.
/// Alle forløb (H1–H6 og valgfag) bruger samme struktur.
/// </summary>
public static class CourseTemplateCatalog
{
    public static CourseTemplate[] All =>
    [
        H1, H2, H3, H4, H5, H6, DeployOrDie, GameDesign, MachineLearning
    ];

    public static CourseTemplate? Find(string? slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? null
            : All.FirstOrDefault(c =>
                c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)
                || c.Short.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<CourseTemplate> Hovedforlob =>
        All.Where(c => c.Kind == CourseKind.Hovedforlob);

    public static IEnumerable<CourseTemplate> Valgfag =>
        All.Where(c => c.Kind == CourseKind.Valgfag);

    private static readonly string[] StandardCriteria =
    [
        "Nysgerrighed — stiller relevante spørgsmål og undersøger frem for kun at få det til at virke.",
        "Best practice — følger aftalte arbejdsgange (Git, struktur, dokumentation) og begrunder afvigelser.",
        "Refleksion — kan forklare hvad der gik godt/skidt, hvad der er lært, og næste skridt."
    ];

    /// <summary>Uger uden ekstra emneliste — kun titel.</summary>
    private static CourseWeekPlan[] W(params string[] titles) =>
        titles.Select(t => new CourseWeekPlan(t)).ToArray();

    public static readonly CourseTemplate Blank = new(
        Slug: "template",
        Short: "Skabelon",
        Title: "Kursusskabelon",
        DurationWeeks: null,
        Lead: "Fælles struktur for undervisningsplaner: mål, indhold, evaluering og ugeplan.",
        Kind: CourseKind.Skabelon,
        Project: "Dit projektnavn",
        Goals:
        [
            "Beskriv, hvad lærlingen skal kunne efter forløbet.",
            "Hold målene konkrete og demonstrerbare.",
            "Kobl mål til projekt og ugeplan."
        ],
        Expectations:
        [
            "Det lærlingen typisk kan efter forløbet.",
            "Niveauet ift. tidligere forløb.",
            "Hvad der kræves for at deltage meningsfuldt."
        ],
        Themes:
        [
            new("Tema 1", "Kort beskrivelse af indhold og praksis."),
            new("Tema 2", "Kort beskrivelse af indhold og praksis."),
            new("Tema 3", "Kort beskrivelse af indhold og praksis.")
        ],
        Weeks: W(
            "Uge 1 · Introduktion og fundament",
            "Uge 2 · Opbygning og afprøvning",
            "Uge 3 · Projekt og afslutning"
        ),
        Holistic: "Beskriv, hvordan fagene samles i ét projekt eller én rød tråd.",
        Practice: "Beskriv koblingen til virksomhedspraksis og værktøjer.",
        Differentiation: "Beskriv, hvordan kompleksitet og tempo kan tilpasses.",
        Evaluation: "Løbende dialog, feedback på produkt/proces og afsluttende overhøring.",
        Criteria:
        [
            "Nysgerrighed — undersøger og stiller relevante spørgsmål.",
            "Best practice — følger aftalte arbejdsgange og begrunder afvigelser.",
            "Refleksion — kan forklare læring og næste skridt."
        ]
    );

    private static readonly CourseTemplate H1 = new(
        "h1", "H1", "Hovedforløb 1", 10,
        "Fundament i C#, OOP, SQL, HTTP, Blazor og projektarbejde (SCRUM/GitHub).",
        CourseKind.Hovedforlob, "Niche Markedsplads",
        [
            "Udvikle grundlæggende applikationer i C# med OOP.",
            "Designe og anvende relationelle databaser med SQL.",
            "Forstå netværkskommunikation og HTTP.",
            "Implementere brugergrænseflader med Blazor.",
            "Arbejde i teams med SCRUM og GitHub."
        ],
        [
            "Skrive og fejlfinde mindre C#-programmer med klasser og metoder.",
            "Lave simple SQL-forespørgsler og CRUD.",
            "Bygge en simpel Blazor-frontend.",
            "Arbejde i Git/GitHub med SCRUM-lignende sprints."
        ],
        [
            new("Programmering", "Variabler, kontrolstrukturer, metoder, debugging og grundlæggende OOP i C#."),
            new("Netværk", "Klient/server, OSI og HTTP."),
            new("Database", "Relationelle databaser, SQL, CRUD og ADO.NET."),
            new("Frontend", "Blazor, databinding, komponenter, HTML/CSS."),
            new("Proces", "SCRUM, sprint-planlægning og versionsstyring med GitHub.")
        ],
        W(
            "C# og grundlæggende OOP",
            "SQL og datamodel",
            "HTTP og Blazor",
            "Team og GitHub",
            "Projektstart · Niche Markedsplads",
            "Sprintarbejde",
            "Udvidelse og integration",
            "Test og polish",
            "Dokumentation",
            "Overhøring"
        ),
        "Alle H1-fag integreres i Niche Markedsplads, så teori og praksis hænger sammen.",
        "Visual Studio, GitHub, PostgreSQL og Blazor i et setup, der minder om reelle udviklingsprojekter.",
        "Grupper kan tilpasse kompleksitet. Ekstra udfordringer efter aftale med underviseren.",
        "Dialog, sprintfeedback og afsluttende overhøring med fokus på nysgerrighed, best practice og refleksion.",
        StandardCriteria);

    private static readonly CourseTemplate H2 = new(
        "h2", "H2", "Hovedforløb 2", 10,
        "Fullstack hotelbooking: RESTful API, EF Core, SPA (Blazor), JWT/roller, AD, SignalR-tickets og mail.",
        CourseKind.Hovedforlob, "Hotelbookingsystemet",
        [
            "Udvikle skalerbare fullstack-løsninger med API, database og SPA-frontend.",
            "Modellere data med EF Core (1:1, 1:n, n:m) og PostgreSQL.",
            "Designe REST API’er med DTO’er, validering, fejlhåndtering og OpenAPI-dokumentation.",
            "Implementere autentifikation/autorisation med BCrypt, roller og JWT.",
            "Bygge Blazor WASM-frontend med state management, login og PWA-elementer.",
            "Integrere Active Directory, mail (bookingbekræftelse) og SignalR-ticketflow.",
            "Anvende SCRUM, GitHub-releases og løbende refleksion i gruppeprojektet."
        ],
        [
            "Bygge API + EF Core + Blazor i samme hotelbooking-løsning.",
            "Håndtere relationer, seed-data og forespørgsler meningsfuldt.",
            "Forklare arkitekturvalg (3-lags, DTO, auth, real-time) i projektet.",
            "Arbejde i grupper (typisk 3, max 4; højst én genbrugt H1-makker)."
        ],
        [
            new("EF Core & database", "ORM, migrationer, Neon/Postgres, relationer, Bogus-seed og repository-struktur."),
            new("REST API", "ASP.NET Core, CRUD, DTO’er, XML/OpenAPI-dokumentation, fejlhåndtering og CORS."),
            new("Sikkerhed", "AuthN vs AuthZ, RBAC, BCrypt, JWT, rate limiting og clientside-risici (fx XSS)."),
            new("SPA / Blazor", "API-kald, login, state (memory/localStorage), services og PWA."),
            new("AD & drift", "Active Directory-brugere/grupper/tilladelser og admin-integration."),
            new("Service & real-time", "ITSM/ticket-tænkning, SignalR og mail til bookingbekræftelse."),
            new("Proces", "SCRUM, GitHub-releases med semver, refleksion og overhøring.")
        ],
        [
            new(
                "API-kald, grupper og projektstart",
                "Intro til H2, hotelbooking-case og API-kald fra frontend",
                [
                    "Forløbsintro, grupperammer og starter-repo",
                    "Hotel Booking-case (fx luksushotel, hotelkæde, hostel-netværk)",
                    "API-kald fra frontend og error handling",
                    "Data-serialisering",
                    "GitHub-arbejdsgange og første fremlæggelse/video"
                ]),
            new(
                "API med EF Core, brugere og JWT",
                "Backend-fundament: Web API, Postgres, roller, BCrypt og JWT",
                [
                    "ASP.NET Core Web API og filgennemgang (Controllers, Program.cs)",
                    "EF Core med PostgreSQL (Docker/Neon) og User-model",
                    "Roller, hashing (BCrypt) og DataAnnotations",
                    "Relationer (1:1, 1:n, n:m), DTO’er og claims",
                    "JWT-opsætning, Bearer-auth og /me-endpoint",
                    "Release: API V0.1.0"
                ]),
            new(
                "Hotelsystem-logik, fejlhåndtering og seed",
                "Dokumentation, sikkerhed, repository pattern og testdata",
                [
                    "XML-dokumentation og OpenAPI/Swagger (valgfrit Scalar)",
                    "Fejlhåndtering, logging og HTTP-statuskoder",
                    "Validering, rate limiting og CORS",
                    "Bogus/Faker til realistisk seed-data",
                    "Repository pattern og struktur",
                    "Udvidet EF Core, deployment og release"
                ]),
            new(
                "SPA-frontend på API’et",
                "Blazor WASM (plus React-oplæg), login, state og PWA",
                [
                    "Opstart af Blazor-projekt med API-kald",
                    "State management i Blazor WASM",
                    "Login-flow mod API med JWT",
                    "Skalerbare frontend-services og PWA-elementer",
                    "Release: frontend koblet til API (fx V0.2.0)"
                ]),
            new(
                "Udvidelse, værktøjer og projektstyring",
                "Ekstra emner, API-testværktøjer og projektarbejde",
                [
                    "Projektstyring og backlog",
                    "API-test med Postman/Bruno",
                    "Projektudvidelser efter gruppens plan",
                    "Release med anbefalede krav"
                ]),
            new(
                "Active Directory-integration",
                "AD, roller/tilladelser og admin-login",
                [
                    "Opsætning af Active Directory",
                    "Roller, brugere og tilladelser",
                    "Integration af lokalt AD til admin-side",
                    "Login-system med AD",
                    "Release med AD-integration"
                ]),
            new(
                "Mail-service og projektudvidelse",
                "Bookingbekræftelse, vejledning og fremlæggelse",
                [
                    "Fremlæggelse og feedback på klassen",
                    "Mail-service (SMTP m.m.) til bookingbekræftelse",
                    "Bookingdetaljer i mail/dashboard",
                    "Gruppevejledning og projektarbejde",
                    "Release"
                ]),
            new(
                "Ticket-system med SignalR",
                "Support-tickets og real-time kommunikation",
                [
                    "Design af ticket-system med EF Core",
                    "Introduktion til SignalR (hubs, klient, grupper)",
                    "Real-time opdateringer i SPA",
                    "Mesterdag og projektarbejde",
                    "Release med ticket/real-time (fx V0.4.0)"
                ]),
            new(
                "Færdiggørelse og Aspire",
                "Projektpolish, dokumentation og .NET Aspire-oplæg",
                [
                    "Feature-lukning og polish",
                    "Oplæg om .NET Aspire",
                    "Dokumentation og demo-forberedelse",
                    "Release før overhøring"
                ]),
            new(
                "Eksamensforberedelse og overhøring",
                "Sidste release, AI som værktøj og overhøring",
                [
                    "Eksamensforberedelse og AI som arbejdsværktøj",
                    "Sidste release (API V1.0.0)",
                    "Overhøring / eksamen",
                    "Afslutning"
                ])
        ],
        "Alle H2-fag samles i hotelbookingsystemet: API, database, SPA, auth, AD, tickets og mail i ét erhvervsnært gruppeprojekt.",
        "Visual Studio, GitHub, EF Core, PostgreSQL/Neon, Blazor, Docker, Postman/Bruno og professionelle release-arbejdsgange (semver + refleksion).",
        "Grupper (typisk 3, max 4) tilpasser feature-dybde — ekstra relationer, auth, AD, SignalR eller test — i dialog med underviseren. Online og fysisk må blandes.",
        "Løbende dialog, sprint-/release-feedback, individuel refleksion ved releases og afsluttende overhøring.",
        StandardCriteria,
        TheoryDocumentPath: "content/courses/h2-teori.json");

    private static readonly CourseTemplate H3 = new(
        "h3", "H3", "Hovedforløb 3", 9,
        "IoT med Arduino, .NET API, database, dashboards og sikkerhed.",
        CourseKind.Hovedforlob, "Interaktiv IoT Platform",
        [
            "Integrere IoT-enheder med backend og database.",
            "Designe API’er til sensordata og aktuatorer.",
            "Udvikle dashboards med visualisering og kontrol.",
            "Arbejde med sikkerhed og dataintegritet i IoT-kæden.",
            "Dokumentere og præsentere en helhedsorienteret løsning."
        ],
        [
            "Koble sensor/aktuator til .NET API og database.",
            "Vise og styre data i et simpelt dashboard.",
            "Anvende grundlæggende auth omkring API og data."
        ],
        [
            new("Arduino & IoT", "Sensorer, aktuatorer og struktureret C++."),
            new("Serverside", "REST API i .NET, realtime og adgang."),
            new("Database", "Lagring af målinger med ORM."),
            new("Dashboard", "Fra rå data til forståelig visning."),
            new("Test & sikkerhed", "Fejltilstande, adgang og dokumentation.")
        ],
        W(
            "Arduino og IoT",
            "Mere Arduino",
            "Serverside",
            "Mere serverside",
            "Projektuge",
            "Test og sikkerhed",
            "MVP færdig",
            "Projektuge",
            "Eksamen"
        ),
        "Hardware, API, database og dashboard samles i én kæde fra sensor til brugergrænseflade.",
        "Arduino, .NET, database og frontend i et setup, der spejler reelle IoT-projekter.",
        "Sensoropsætning, API-features og dashboard kan differentieres efter niveau.",
        "Dialog, sprintfeedback og afsluttende overhøring med demonstration af IoT-kæden.",
        StandardCriteria);

    private static readonly CourseTemplate H4 = new(
        "h4", "H4", "Hovedforløb 4", 7,
        "Flutter/Dart, API-integration, offline lagring, test og SCRUM.",
        CourseKind.Hovedforlob, "Mobil kundestyringsapp",
        [
            "Designe og udvikle cross-platform apps med Flutter.",
            "Integrere mobilapp med eksisterende API’er.",
            "Håndtere offline data og synkronisering.",
            "Teste centrale flows i mobilappen.",
            "Arbejde med SCRUM i et mobilprojekt."
        ],
        [
            "Bygge en Flutter-app med navigation og formularer.",
            "Kalde et API og vise resultater meningsfuldt.",
            "Forklare offline-strategi og testvalg."
        ],
        [
            new("Flutter/Dart", "Widgets, state og navigation."),
            new("API-integration", "HTTP, modeller og fejlhåndtering."),
            new("Offline", "Lokal lagring og synk."),
            new("Test", "Widget-/integrationstest af centrale flows."),
            new("Proces", "SCRUM og afleveringsklar app.")
        ],
        W(
            "Flutter-fundament",
            "UI og navigation",
            "API-integration",
            "Offline data",
            "Test",
            "Projektuge",
            "Overhøring"
        ),
        "Mobilapp, API og data samles i én kundestyringscase.",
        "Arbejde med mobiludvikling mod realistiske API- og offlinekrav.",
        "Feature-omfang og teknisk dybde tilpasses i dialog med underviseren.",
        "Løbende feedback og afsluttende overhøring med app-demonstration.",
        StandardCriteria);

    private static readonly CourseTemplate H5 = new(
        "h5", "H5", "Hovedforløb 5", 9,
        "Linux, Docker, embedded/IoT, pipelines og mini-svendeprøve.",
        CourseKind.Hovedforlob, "Mini-svendeprøveprojekt",
        [
            "Administrere Linux-baserede miljøer til drift og udvikling.",
            "Pakke og køre løsninger med Docker.",
            "Bygge dataindsamling og pipelines i en helhedsorienteret opgave.",
            "Forberede og gennemføre en mini-svendeprøve.",
            "Dokumentere drift, dataflow og valg."
        ],
        [
            "Sætte en containeriseret løsning op.",
            "Forklare Linux-grundlag relevant for drift.",
            "Samle infrastruktur og applikation i ét demonstrerbart projekt."
        ],
        [
            new("Linux", "Administration, services og sikkerhed i grove træk."),
            new("Docker", "Images, containers og compose."),
            new("Embedded/IoT", "Dataindsamling og enhedskobling."),
            new("Pipelines", "Fra data til anvendelig applikation."),
            new("Mini-svendeprøve", "Plan, produkt, dokumentation og fremlæggelse.")
        ],
        W(
            "Linux-fundament",
            "Services og drift",
            "Docker",
            "Compose og miljøer",
            "IoT/data",
            "Pipelines",
            "Mini-svendeprøve start",
            "Dokumentation",
            "Fremlæggelse"
        ),
        "Drift, containere, IoT og data samles i mini-svendeprøven.",
        "Spejler erhvervsnær drift og udviklingsopgaver med realistisk infrastruktur.",
        "Dybde i Linux/Docker/data kan differentieres; prøverammen er fælles.",
        "Løbende vejledning plus mini-svendeprøve med produkt og dokumentation.",
        StandardCriteria);

    private static readonly CourseTemplate H6 = new(
        "h6", "H6", "Hovedforløb 6 (Svendeprøve)", 5,
        "Afsluttende projekt, dokumentation og individuel mundtlig fremlæggelse.",
        CourseKind.Hovedforlob, "Svendeprøveprojekt",
        [
            "Gennemføre et afsluttende projekt med krav, løsning og dokumentation.",
            "Anvende tidligere teknologier i én selvstændig opgave.",
            "Planlægge egen arbejdsproces med tydelig dokumentation.",
            "Udarbejde produkt- og procesdokumentation.",
            "Præsentere og forsvare løsningen individuelt."
        ],
        [
            "Drive et afgrænset projekt fra plan til produkt.",
            "Begrunde tekniske og metodiske valg.",
            "Samle kompetencer fra tidligere forløb."
        ],
        [
            new("Projektopgave", "Helhedsorienteret løsning inden for prøvens rammer."),
            new("Dokumentation", "Produktrapport og procesrapport."),
            new("Projektstyring", "Plan, prioritering og opfølgning."),
            new("Prøveforberedelse", "Fremlæggelse og demonstration.")
        ],
        W(
            "Plan og krav",
            "Udvikling",
            "Dokumentation",
            "Polish og forberedelse",
            "Prøve"
        ),
        "H6 samler kompetencer fra tidligere hovedforløb i én afsluttende opgave.",
        "Spejler en professionel aflevering: ansvar, tidspres, dokumentation og mundtligt forsvar.",
        "Teknologivalg tilpasses inden for svendeprøvens formelle krav.",
        "Formel svendeprøve med produkt, dokumentation og individuel fremlæggelse (begge delprøver skal bestås).",
        StandardCriteria);

    private static readonly CourseTemplate DeployOrDie = new(
        "deploy-or-die", "Deploy Or Die", "Deploy Or Die", 3,
        "Fra lokal kode til kørende miljø: containere, pipelines, hosting og observation.",
        CourseKind.Valgfag, "Deploy-lab",
        [
            "Pakke en applikation, så den kan køre uden for udviklingsmaskinen.",
            "Opsætte en simpel CI/CD-pipeline med build og deploy.",
            "Vælge hosting og forklare miljøer (dev/prod) og hemmeligheder.",
            "Observere en kørende tjeneste og reagere på fejl.",
            "Dokumentere deploy-flowet, så andre kan gentage det."
        ],
        [
            "Bygge et containerimage og køre det lokalt.",
            "Få en pipeline til at bygge og udgive en version.",
            "Forklare, hvorfor et deploy fejlede, og hvad der rettede det."
        ],
        [
            new("Containere", "Dockerfile, images og compose til lokal afvikling."),
            new("CI/CD", "Automatisk build, test og udgivelse."),
            new("Hosting", "Miljøer, domæner, secrets og rollback i grove træk."),
            new("Observability", "Logs, healthchecks og simple alarmer."),
            new("Dokumentation", "Runbook: hvordan andre deployer samme løsning.")
        ],
        W(
            "Fra kode til container",
            "Pipeline og første deploy",
            "Drift, fejl og dokumentation"
        ),
        "Udvikling, build, deploy og drift behandles som én kæde — ikke separate øvelser.",
        "Spejler reelle release-arbejdsgange med Docker, GitHub Actions/CI og hosting.",
        "Stack og hostingdybde kan tilpasses; kravet er et gentageligt deploy af egen app.",
        "Løbende lab-feedback og afsluttende demonstration af et grønt deploy med runbook.",
        StandardCriteria);

    private static readonly CourseTemplate GameDesign = new(
        "game-design", "Game Design", "Game design fra bunden", 3,
        "Byg Rune Run sammen, undersøg designbegreber, og brug erfaringerne i dit eget spil.",
        CourseKind.Valgfag, "Rune Run → eget spil",
        [
            "Forklare mål, regler, mekanikker og core loop.",
            "Undersøge valg, risiko, feedback og game feel.",
            "Afgrænse et eget spil med pitch og GDD.",
            "Afprøve, dele og begrunde ændringer i spillet.",
            "Aflevere en spilbar prototype med opdateret GDD."
        ],
        [
            "Tegne og forklare et core loop.",
            "Bygge en Rune Run-variant med meningsfulde valg.",
            "Få et indledende GDD godkendt før eget udviklingsarbejde."
        ],
        [
            new("Core loop", "Handling, feedback og næste valg."),
            new("Regler og risiko", "Mål, begrænsninger og synlige konsekvenser."),
            new("Scope og GDD", "Pitch, afgrænsning og godkendelse."),
            new("Playtest", "Observation før fortolkning."),
            new("Showcase", "Spilbar version fra start til slut.")
        ],
        W(
            "Rune Run og fælles teori",
            "Eget spil og side quests",
            "Afslutning og aflevering"
        ),
        "Fælles Rune Run først, derefter eget godkendte projekt — teori før individuel bygning.",
        "Designvalg afprøves i spilbare builds; GDD er arbejdsdokument, ikke rapport for rapportens skyld.",
        "Spillets kompleksitet tilpasses, så første version kan gøres færdig.",
        "Main quests (GDD, prototype, kode/video) plus daily playtest/logbog. Ingen særskilt mundtlig eksamen.",
        StandardCriteria);

    private static readonly CourseTemplate MachineLearning = new(
        "machine-learning", "Machine Learning", "Machine Learning", 3,
        "Fra data til model: forberedelse, træning, evaluering og en lille anvendelig demo.",
        CourseKind.Valgfag, "ML-mini",
        [
            "Forberede et datasæt og forklare bias, lækage og kvalitet i grove træk.",
            "Træne en simpel model til klassifikation eller regression.",
            "Evaluere med relevante metrikker og holde testdata adskilt.",
            "Forklare begrænsninger og etiske opmærksomhedspunkter.",
            "Pakke en lille demo, hvor andre kan prøve modellens output."
        ],
        [
            "Rense og splitte data meningsfuldt.",
            "Træne og sammenligne mindst to enkle tilgange.",
            "Forklare, hvad modellen kan — og hvad den ikke kan."
        ],
        [
            new("Data", "Indsamling, rensning, features og splits."),
            new("Modeller", "Enkle supervised metoder og baseline."),
            new("Evaluering", "Metrikker, confusion matrix / fejlanalyse."),
            new("Demo", "Lille grænseflade eller notebook til afprøvning."),
            new("Ansvar", "Bias, privatliv og dokumentation af antagelser.")
        ],
        W(
            "Data og problemformulering",
            "Model og evaluering",
            "Demo, begrænsninger og præsentation"
        ),
        "Problem, data, model og demo hænger sammen — ikke tre løsrevne øvelser.",
        "Spejler en lille erhvervsnær ML-opgave: tydeligt formål, målbare resultater og ærlig evaluering.",
        "Datasæt og modelkompleksitet tilpasses; kravet er forståelse og en afprøvbar demo.",
        "Løbende feedback på data/modelvalg og afsluttende demonstration med evaluering.",
        StandardCriteria);
}

public enum CourseKind
{
    Hovedforlob,
    Valgfag,
    Skabelon
}

public sealed record CourseTheme(string Name, string Desc);

/// <summary>Uge i undervisningsplanen — titel plus valgfri emner (uden datoer).</summary>
public sealed record CourseWeekPlan(string Title, string Focus = "", string[]? Topics = null)
{
    public bool HasDetail =>
        !string.IsNullOrWhiteSpace(Focus) || Topics is { Length: > 0 };
}

/// <summary>Teori-/kompetenceblok til pensum (generel template-indhold).</summary>
public sealed record CourseTheoryBlock(string Title, string Summary, string[] Points);

/// <summary>Fuld teoriblok med markdown-brødtekst (fx «Teori for dagen»).</summary>
public sealed record CourseTheoryDocumentBlock(int Number, string Title, string Markdown);

public sealed record CourseTemplate(
    string Slug,
    string Short,
    string Title,
    int? DurationWeeks,
    string Lead,
    CourseKind Kind,
    string Project,
    string[] Goals,
    string[] Expectations,
    CourseTheme[] Themes,
    CourseWeekPlan[] Weeks,
    string Holistic,
    string Practice,
    string Differentiation,
    string Evaluation,
    string[] Criteria,
    CourseTheoryBlock[]? Theory = null,
    string? TheoryDocumentPath = null)
{
    public string DurationLabel => DurationWeeks is int w ? $"{w} uger" : "Fleksibel";
    public string KindLabel => Kind switch
    {
        CourseKind.Hovedforlob => "Hovedforløb",
        CourseKind.Valgfag => "Valgfag",
        _ => "Skabelon"
    };
    public string Path => $"/courses/template/{Slug}";
    public bool HasTheoryDocument => !string.IsNullOrWhiteSpace(TheoryDocumentPath);
    public bool HasTheory => Theory is { Length: > 0 } || HasTheoryDocument;
}
