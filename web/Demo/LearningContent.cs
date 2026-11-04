namespace Mercantec.Tech.Web.Demo;

public static class LearningContent
{
    public static readonly Lesson[] Lessons =
    [
        new("iot", "arduino", "Kom i gang med Arduino", "Forbind en sensor og aflæs den første værdi.", "Start med én sensor og én tydelig reaktion. Undersøg inputtet i Serial Monitor, før du kobler flere dele på.", "Vis, at din kode kan læse og forklare en måling."),
        new("iot", "cpp", "Strukturér din C++-kode", "Funktioner, ansvar og flere filer.", "Del aflæsning, behandling og output op i funktioner. Giv dem navne, der fortæller, hvad de gør.", "Forklar, hvordan opdelingen gør programmet lettere at ændre."),
        new("iot", "mqtt", "Fra sensor til MQTT", "Publish, subscribe og topics.", "Send én måling til en broker. Lad en anden klient abonnere på samme topic, og afprøv, hvad der sker, når topic eller forbindelse er forkert.", "Følg en besked hele vejen fra enheden til abonnenten."),
        new("server", "api", "Et API til dine målinger", "Endpoints, requests og svar.", "Beskriv først de data, klienten sender. Lav derefter et endpoint, der validerer input og giver et svar, klienten kan handle på.", "Demonstrér både et korrekt request og et forkert request."),
        new("server", "realtime", "Live data med SignalR", "Opdatér visningen, når noget ændrer sig.", "Lad serveren give besked til tilsluttede klienter. Afprøv genforbindelse og vis tydeligt, om data er aktuelle.", "Vis en ny måling uden at genindlæse siden."),
        new("database", "model", "Giv data en struktur", "Relationer og Entity Framework.", "Tegn sammenhængen mellem enhed og måling. Vælg felter, typer og relationer, før du gemmer data med en ORM.", "Gem og hent målinger for en bestemt enhed."),
        new("dashboard", "dashboard", "Byg et forståeligt dashboard", "Fra rå JSON til beslutninger.", "Vis værdi, enhed og tidspunkt. Vælg derefter en graf, der gør ændringer synlige, og håndtér tomme data og fejl.", "Lad en anden elev bruge visningen uden en forklaring."),
        new("kvalitet", "security", "Test hele forbindelsen", "Fejl, adgang og dokumentation.", "Undersøg, hvad der sker, når sensoren, netværket eller API’et fejler. Test også, at en bruger kun kan se det, vedkommende må.", "Dokumentér en fejltilstand og din løsning."),
        new("proces", "project", "Fra idé til afprøvelig løsning", "Scope, opgaver og en tydelig MVP.", "Vælg den mindste løsning, der viser hele kæden. Fordel opgaver, prøv den tidligt, og brug feedback til at vælge næste skridt.", "Forklar jeres vigtigste prioritering og næste forbedring."),
        new("design", "core-loop", "Core loop", "Hvad gør spilleren igen og igen?", "Beskriv handling, feedback og næste valg i Rune Run. Byg en lille loop, der kan afprøves, før du tilføjer flere systemer.", "Tegn dit spils loop og afprøv, om det er forståeligt."),
        new("design", "rules", "Regler og mål", "Giv spilleren noget tydeligt at arbejde hen imod.", "Vælg et mål, en begrænsning og en konsekvens. Undersøg, hvordan de ændrer spillerens valg i Rune Run.", "Forklar, hvad spilleren kan gøre, og hvordan spillet slutter."),
        new("design", "risk", "Risiko og belønning", "En sikker omvej eller en hurtig, farlig rute.", "Byg to ruter mod samme mål. Gør risikoen synlig, så spilleren kan tage et informeret valg.", "Sammenlign de to ruter med en anden spiller."),
        new("design", "feedback", "Feedback og game feel", "Vis, at spillerens handling betyder noget.", "Brug bevægelse, lyd eller farve til at gøre handlinger tydelige. Hver reaktion skal hjælpe spilleren med at forstå tilstanden.", "Afprøv, om en ny spiller forstår feedbacken."),
        new("design", "scope", "Scope og GDD", "Fra stor idé til et spil, du kan gøre færdigt.", "Beskriv pitch, loop, regler og afgrænsning i din GDD. Eget spils GDD godkendes, før udviklingen begynder.", "Aflevér punkt 1–4 i GDD’en før dit eget spil."),
        new("design", "playtest", "Playtest", "Se, hvad spilleren faktisk gør.", "Lad en anden prøve uden at hjælpe. Skriv observationer først og din fortolkning bagefter. Vælg én ændring, du vil afprøve.", "Gem én observation og ét konkret næste skridt."),
        new("design", "showcase", "Vis dit spil", "En version, der kan prøves fra start til slut.", "Saml spil, GDD og logbog. Fortæl kort om dine vigtigste valg, og lad andre spille løsningen.", "Gør det let at finde spillets start, mål og afslutning.")
    ];
    public static string Cover(string course) => course == "game" ? "game-cover.jpg" : "h3-cover.jpg";
    public static DateOnly Start(string course) => course == "game" ? new(2026, 11, 2) : new(2026, 10, 19);
    public static string SubjectIcon(string id) => id switch { "iot" => "chip", "server" => "code", "database" => "database", "dashboard" => "chart", "kvalitet" => "shield", "design" => "game", _ => "book" };
    public sealed record Lesson(string Subject, string Id, string Name, string Lead, string Body, string Goal);
}
