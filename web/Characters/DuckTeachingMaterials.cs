namespace Mercantec.Tech.Web.Characters;

internal static class DuckTeachingMaterials
{
    private const string ImageFolder = "/images/characters/gummianden/infographics/";
    private const string Reproduce = "1. Genskab";
    private const string Investigate = "2. Undersøg";
    private const string Verify = "3. Ret og verificér";
    private const string Share = "4. Del";

    private static readonly CharacterLink DuckMethod = Source("VATek · Gummianden", "https://gummianden.vatek.dk/info");
    private static readonly CharacterLink Debugger = Source("Microsoft Learn · C# og debuggeren",
        "https://learn.microsoft.com/en-us/visualstudio/get-started/csharp/tutorial-debugger");
    private static readonly CharacterLink Breakpoints = Source("Microsoft Learn · Breakpoints",
        "https://learn.microsoft.com/en-us/visualstudio/debugger/using-breakpoints");
    private static readonly CharacterLink Watch = Source("Microsoft Learn · Watch og QuickWatch",
        "https://learn.microsoft.com/en-us/visualstudio/debugger/watch-and-quickwatch-windows");
    private static readonly CharacterLink Step = Source("Microsoft Learn · Step Into, Over og Out",
        "https://learn.microsoft.com/en-us/visualstudio/debugger/navigating-through-code-with-the-debugger");
    private static readonly CharacterLink CallStack = Source("Microsoft Learn · Call Stack",
        "https://learn.microsoft.com/en-us/visualstudio/debugger/how-to-use-the-call-stack-window");
    private static readonly CharacterLink Exceptions = Source("Microsoft Learn · Exceptions i debuggeren",
        "https://learn.microsoft.com/en-us/visualstudio/debugger/managing-exceptions-with-the-debugger");
    private static readonly CharacterLink Arrays = Source("Microsoft Learn · Arrays i C#",
        "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/arrays");
    private static readonly CharacterLink Bounds = Source("Microsoft Learn · IndexOutOfRangeException",
        "https://learn.microsoft.com/en-us/dotnet/api/system.indexoutofrangeexception");
    private static readonly CharacterLink Logging = Source("Microsoft Learn · Logging i .NET",
        "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/overview");
    private static readonly CharacterLink Nullability = Source("Microsoft Learn · Nullable referencetyper",
        "https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/null-safety/nullable-reference-types");
    private static readonly CharacterLink Tests = Source("Microsoft Learn · Enhedstest med xUnit",
        "https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit");
    private static readonly CharacterLink Issues = Source("GitHub Docs · Opret et issue",
        "https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-an-issue");

    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        Card("01-forklar-fejlen", "Forklar fejlen højt",
            "Gummianden lytter til en forklaring med tre kort for forventet resultat, faktisk resultat og første forskel.",
            Reproduce,
            "Fortæl anden, hvad koden skal gøre, hvad den gør, og hvornår de to ting først er forskellige.",
            [
                "Vælg ét konkret input. Gå gennem koden, som om din lytter aldrig har set den før. Sig, hvilken værdi hver vigtig variabel har, og hvorfor du forventer, at næste linje ændrer den.",
                "Når du hører dig selv sige »den gør jo bare …«, så stop ved »bare«. Det er en antagelse, du kan undersøge med et breakpoint, en loglinje eller et lille testinput.",
                "Prøv det med summen af tallene 1, 2 og 3. Hvis programmet viser 9, hvor kan de tre ekstra point så komme fra? Skriv én ting, du vil måle, før du retter koden."
            ],
            null, DuckMethod),
        Card("02-genskab-fejlen", "Genskab fejlen",
            "Detektivanden følger et spor med samme input, samme trin og samme fejl.",
            Reproduce,
            "Gør en løs fejlbeskrivelse til et forsøg, du selv eller en makker kan gentage.",
            [
                "Notér version og relevant miljø. Start fra en kendt tilstand, brug de samme data, og udfør handlingerne i samme rækkefølge. Beskriv det, du faktisk ser, fx en bestemt undtagelse eller et forkert tal.",
                "Kør forsøget igen. Hvis fejlen kun sker nogle gange, så noter hyppigheden og forskelle mellem forsøgene, fx rækkefølge, tidspunkt eller flere samtidige handlinger.",
                "Prøv det ved at give en makker inputtet og trinene til pointtavlen. Kan de se samme fejl uden at få en mundtlig forklaring fra dig?"
            ],
            null, DuckMethod, Issues),
        Card("03-forventet-og-faktisk", "Forventet og faktisk",
            "Gummianden sammenligner forventet 6 med faktisk 9 for det samme input, tallet 3.",
            Reproduce,
            "Sæt to konkrete resultater ved siden af hinanden. De gør fejlen lettere at efterprøve.",
            [
                "Aftalen er at lægge tallene fra 1 til inputtet sammen. Med input 3 forventer vi 1 + 2 + 3, altså 6. Programmet nedenfor giver faktisk 9. Begge resultater bruger samme input.",
                "Hvert gennemløb lægger n til totalen i stedet for i. Derfor lægger programmet 3 til tre gange. Det første gennemløb giver 3, mens vi forventede 1. Her er den første forskel.",
                "Prøv at erstatte n med i i tildelingen. Kør med 3 igen og bekræft 6. Prøv også n = 1, hvor begge udgaver tilfældigvis giver 1. Det input afslører derfor ikke fejlen."
            ],
            """
            using System;

            int n = 3;
            int total = 0;
            for (int i = 1; i <= n; i++)
                total += n; // Fejl: lægger altid n til.

            Console.WriteLine(total); // 9. Vi forventer 6.
            """, Debugger),
        Card("04-mindste-eksempel", "Det mindste eksempel",
            "Værkstedsanden fjerner dele fra en større maskine, så den samme fejl kan ses i en enkelt kontakt.",
            Reproduce,
            "Fjern én afhængighed ad gangen, og tjek at den oprindelige fejl stadig viser sig.",
            [
                "Lav en kopi eller et lille øveprojekt. Tag kendte input og den mistænkte funktion med. En fast liste med tre tal kan fx erstatte en database, hvis den stadig fører til samme fejl i løkken.",
                "Kør efter hver ændring. Når fejlen forsvinder, kan det senest fjernede stykke være en nødvendig forudsætning. Læg det tilbage, og se om fejlen vender tilbage.",
                "Prøv at genskabe pointtavlens indeksfejl med kun et array og en enkelt læsning. Eksemplet nedenfor kaster samme type undtagelse som konsoløvelsen med breakpoint. Det isolerer adgang uden for arrayet fra resten af appen."
            ],
            """
            using System;

            int[] point = { 2, 3, 1 };
            int i = point.Length;
            Console.WriteLine(point[i]);
            """, Bounds, Arrays),
        Card("05-laes-fejlbeskeden", "Læs fejlbeskeden",
            "Detektivanden undersøger en fejlbesked som tre spor, nemlig typen, beskeden og stedet.",
            Investigate,
            "Find typen af fejl, læs beskeden, og brug stack trace til at finde en relevant linje.",
            [
                "En undtagelsestype siger noget om, hvad der skete. En IndexOutOfRangeException betyder fx, at et arrayindeks er uden for de gyldige grænser. Beskeden giver ofte ekstra detaljer. Et stack trace viser, hvilke kald der førte til undtagelsen.",
                "Find den øverste relevante linje fra din egen kode. Linjen viser, hvor fejlen bliver synlig. Den forkerte værdi kan være opstået tidligere, fx i beregningen af et indeks eller hos en kalder.",
                "Prøv det med konsoløvelsen på næste kort. Find typen og linjen i fejlvisningen. Undersøg derefter, hvor indeks i fik værdien 3."
            ],
            null, Exceptions, Bounds),
        Card("06-saet-et-breakpoint", "Sæt et breakpoint",
            "Gummianden sætter en rød prik i kodemargenen, og en gul pil markerer den linje, hvor koden er sat på pause.",
            Investigate,
            "Sæt programmet på pause lige før et mistænkt arrayopslag, og aflæs tilstanden, mens den findes.",
            [
                "Sæt koden ind i Program.cs i et nyt C#-konsolprojekt. Vi forventer, at den viser summen 6. Koden indeholder en bevidst fejl og kaster i stedet IndexOutOfRangeException, før den når Console.WriteLine.",
                "I Visual Studio kan du klikke i venstre margen ved linjen total += point[i]. Start med F5. Den røde prik markerer dit breakpoint. Når debuggeren rammer det, kan du undersøge værdierne, før linjen udføres.",
                "Prøv at fortsætte med F5 mellem stoppene. Ved i = 3 er total allerede 6, men point.Length er 3, så højeste indeks er 2. Stop kørslen, skift <= til <, og kontrollér at programmet nu udskriver 6."
            ],
            """
            using System;

            int[] point = { 2, 3, 1 };
            int total = 0;

            for (int i = 0; i <= point.Length; i++) // Bevidst fejl
            {
                total += point[i]; // Sæt dit breakpoint her.
            }

            Console.WriteLine(total);
            """, Breakpoints, Debugger),
        Card("07-betinget-breakpoint", "Betinget breakpoint",
            "Gummianden følger loopet fra i = 0 til i = 3, hvor et betinget breakpoint stopper kørslen.",
            Investigate,
            "Spring de normale gennemløb over, og stop kun, når det mistænkte indeks dukker op.",
            [
                "Åbn konsoløvelsen fra Sæt et breakpoint, og behold den bevidste fejl. Højreklik på breakpointet ved total += point[i] i Visual Studio. Åbn indstillingerne, vælg en betingelse, og skriv i == 3 som et boolesk udtryk.",
                "Start igen. De første tre gennemløb fortsætter. Debuggeren stopper før opslaget, når i er 3, hvor total allerede er 6. Udtrykket bruger dobbelt lighedstegn til at sammenligne. Et enkelt lighedstegn bruges til tildeling.",
                "Prøv i == point.Length som betingelse. Skift dernæst løkken til i < point.Length og kør igen. Programmet viser 6 uden at ramme betingelsen."
            ],
            "i == point.Length", Breakpoints),
        Card("08-locals-og-watch", "Locals og Watch",
            "Logvagten ser værdien i = 2 i Locals og følger udtrykket i + 1 med værdien 3 i Watch.",
            Investigate,
            "Brug Locals til værdier omkring den aktuelle kode og Watch til et udtryk, du selv vælger.",
            [
                "Åbn Locals, mens programmet er sat på pause i løkken. Her kan du se lokale variabler i den valgte ramme, fx i, total og point. Udvid arrayet for at undersøge indholdet. Watch kan holde øje med et bestemt udtryk, så længe det kan evalueres i det valgte scope.",
                "Tilføj i + 1, point.Length og i < point.Length i Watch. Når i er 2, er i + 1 lig med 3, mens i < point.Length er sand. Ved næste loopgennemløb er i = 3, og den sidste betingelse bliver falsk.",
                "Prøv at forudsige de næste værdier, før du fortsætter. Sammenlign med Watch. Vælg udtryk, der kun læser tilstanden, så dine observationer ikke påvirker programmet."
            ],
            "i + 1\npoint.Length\ni < point.Length", Watch, Debugger),
        Card("09-step-into-over-out", "Step Into, Over og Out",
            "Grænsetestpiloten følger tre forskellige stier, der går ind i et kald, fortsætter på samme niveau og forlader metoden.",
            Investigate,
            "Vælg, om debuggeren skal vise metoden indefra, gå videre ved kalderen eller køre ud af metoden.",
            [
                "Sæt breakpoint på int total = HentPris(3) i dette særskilte konsolprojekt. I Visual Studio går Step Into (F11) ind i HentPris, så du kan se antal. Step Over (F10) udfører hele kaldet og går til næste linje i kalderen.",
                "Step Out (Shift+F11) udfører resten af den metode, du står i, og vender tilbage til kalderen. Andre breakpoints eller undtagelser kan stadig afbryde. Genstart mellem forsøgene, så du kan sammenligne den gule positionspil.",
                "Prøv først F11 og dernæst Shift+F11. Kig på den gule pil, og læs total, når tildelingen er udført. Kør igen, og brug F10 på kaldet. I begge forsøg bliver total 30."
            ],
            """
            using System;

            int total = HentPris(3); // Sæt dit breakpoint her.
            Console.WriteLine(total);

            static int HentPris(int antal) => antal * 10;
            """, Step),
        Card("10-call-stack", "Læs Call Stack",
            "Spordetektiven læser tre rammer fra toppen, hvor HentPris kører efter kald fra BeregnTotal og Main.",
            Investigate,
            "Følg de aktive kald tilbage, og se, hvor den aktuelle metode fik sine data fra.",
            [
                "Sæt breakpoint i HentPris, kør appen, og åbn Call Stack. Den øverste relevante ramme viser, hvor du står. Nedenunder kan du se, at BeregnTotal kaldte HentPris, og at Main kaldte BeregnTotal.",
                "Vælg en anden ramme for at se kalderens kontekst og variabler, mens udførelsen stadig er sat på pause. Det hjælper, når en metode kan kaldes fra flere steder, og du vil kende det aktuelle forløb.",
                "Prøv at klikke mellem HentPris og BeregnTotal i Call Stack. Hvor kan du se antal? Hvor beregnes prisen? Bekræft til sidst, at koden udskriver 35."
            ],
            """
            using System;

            static class Program
            {
                static void Main() => Console.WriteLine(BeregnTotal(3));
                static int BeregnTotal(int antal) => HentPris(antal) + 5;
                static int HentPris(int antal) => antal * 10; // Breakpoint
            }
            """, CallStack),
        Card("11-foelg-loggen", "Følg loggen",
            "Logvagten lyser på en tidslinje med tre hændelser, start, input og fejl.",
            Investigate,
            "Følg den samme kørsel gennem dens hændelser, også når du ikke kan holde en debugger åben.",
            [
                "Skriv få hændelser med nok kontekst til at kunne adskille to kørsler. For et HTTP-kald kan det fx være et fælles anmodnings-id, starttidspunkt og relevante tal. ILogger kan gemme værdierne fra navngivne pladsholdere som særskilte felter.",
                "Læs forløbet fra start frem mod den første relevante fejl. Hold fx persondata og tokens ude af loggen. Eksemplet kan bruges i en .NET-app, der allerede har en ILogger ved navn logger.",
                "Prøv at skrive én loghændelse med indeks og længde lige før det mistænkte opslag. Når i = 3 og længden er 3, kan du se problemet selv efter kørslen er stoppet."
            ],
            """
            logger.LogInformation(
                "Kørsel {RunId} læser indeks {Index} af {Length}",
                runId, i, point.Length);
            """, Logging),
        Card("12-off-by-one", "Én plads for langt",
            "Værkstedsanden tæller tre gyldige arrayindeks 0, 1 og 2 og undersøger den overstregede plads 3.",
            Verify,
            "Ret grænsen fra <= til <, når i starter ved 0 og bruges som arrayindeks.",
            [
                "Et C#-array med tre elementer har gyldige indeks 0, 1 og 2. Length er antal elementer. Hvis løkken bruger <=, prøver den også at læse plads 3. Med < stopper løkken, når i er lig med Length.",
                "Hvis du kun skal læse værdierne, kan foreach gøre koden kortere og fjerne den manuelle indeksgrænse. Behold for, hvis opgaven også skal bruge den aktuelle position.",
                "Prøv rettelsen på tre arrays. For { 2, 3, 1 } skal resultatet være 6. For { 2 } skal det være 2. For et tomt array skal det være 0, og kroppen i løkken må ikke køre."
            ],
            """
            using System;

            int[] point = { 2, 3, 1 };
            int total = 0;
            for (int i = 0; i < point.Length; i++)
                total += point[i];

            Console.WriteLine(total);
            """, Arrays, Bounds),
        Card("13-null-og-graenser", "Prøv grænserne",
            "Grænsetestpiloten sammenligner tre input med et fraværende felt, en tom streng og én værdi.",
            Verify,
            "Aftal, hvordan koden skal reagere på null, tom streng og et input med ét tegn.",
            [
                "En null-reference peger ikke på en streng. En tom streng findes og har længden 0. I dette eksempel skal en manglende eller tom værdi vises som Ukendt. Når en værdi findes, bruger vi den uændret.",
                "Den nullable type string? fortæller, at vi forventer, at input kan være null. string.IsNullOrEmpty undersøger de to tilfælde, hvor vi vil give samme svar. Blanktegn er tilladt i denne aftale. Vælg IsNullOrWhiteSpace, hvis blanktegn også skal udløse standardnavnet.",
                "Prøv at køre eksemplet og forudsige resultatet for hver linje. De første to giver Ukendt, og den sidste giver A. Tilføj derefter en streng med ét mellemrum, og forklar resultatet."
            ],
            """
            using System;

            foreach (string? navn in new string?[] { null, "", "A" })
                Console.WriteLine(VisNavn(navn));

            static string VisNavn(string? navn) =>
                string.IsNullOrEmpty(navn) ? "Ukendt" : navn;
            """, Nullability),
        Card("14-hypotese-og-test", "Test én hypotese",
            "Værkstedsanden drejer én af tre knapper og sammenligner sin hypotese med det observerede resultat.",
            Verify,
            "Skriv, hvad du tror, og hvilken observation der kan afgøre, om det passer.",
            [
                "Formulér en forklaring, der kan modbevises. Fx »Løkken fejler, fordi den besøger indeks 3 i et array med længde 3«. Hvis det passer, bør et breakpoint før opslaget vise begge værdier.",
                "Hold programversion, input og trin ens. Prøv først at observere uden at ændre koden. Lav derefter den mindste rettelse, og gentag det samme forsøg. Så kan du forbinde forskellen i resultat med en bestemt ændring.",
                "Prøv at sætte betingelsen fra <= til < og behold alle tre tal. Gentag derefter det oprindelige forsøg med samme data og samme breakpoint. Koden skal stadig lægge alle tre tal sammen, og den må ikke ramme indeks 3."
            ],
            null, Breakpoints, Arrays),
        Card("15-regressionstest", "Lås rettelsen med en test",
            "Værkstedsanden følger den samme test gennem tre faser, hvor den fejler, koden rettes, og testen består.",
            Verify,
            "Skriv en regressionstest, der fejler før rettelsen og består bagefter.",
            [
                "Flyt sumløkken ind i en metode i dit øveprojekt, så du kan kalde den med et kendt input. Opret en xUnit-test, der kalder metoden fra projektet med et tomt array. Her er det forventede resultat 0. Den gamle løkke forsøger at læse indeks 0 og kaster en undtagelse.",
                "Læg Pointtavle.cs i projektet og PointTests.cs i testprojektet, der refererer til det. Kør dotnet test, og se testen fejle af den forventede grund. Ret løkkens grænse i Sum, og kør nøjagtigt samme test igen.",
                "Prøv at tilføje en test med ét element og en med tre elementer. Hvis nogen senere genindfører <=, skal testpakken afsløre fejlen."
            ],
            """
            // Pointtavle.cs
            public static class Pointtavle
            {
                public static int Sum(int[] point)
                {
                    int total = 0;
                    for (int i = 0; i <= point.Length; i++) // Ret til <.
                        total += point[i];
                    return total;
                }
            }

            // PointTests.cs
            using System;
            using Xunit;

            public class PointTests
            {
                [Fact]
                public void SumAfTomtArrayErNul() =>
                    Assert.Equal(0, Pointtavle.Sum(Array.Empty<int>()));
            }
            """, Tests),
        Card("16-skriv-en-fejlrapport", "Skriv en fejlrapport",
            "Spordetektiven udfylder en fejlrapport med miljø, trin, forventet resultat og faktisk resultat.",
            Share,
            "Giv din makker det, de skal bruge for at se samme fejl og efterprøve rettelsen.",
            [
                "Skriv en konkret titel. Notér version, input og de korteste trin, der genskaber fejlen. Beskriv forventet og faktisk resultat hver for sig. Tilføj fejltype og relevant kode eller en kort del af loggen, når det hjælper.",
                "Adskil observationen fra forklaringen. »i var 3 før opslaget« er en observation. »Løkkens grænse er forkert« er hypotesen, indtil du har prøvet den mindste rettelse med samme input.",
                "Prøv at bytte rapport med en makker. De skal kunne få den samme IndexOutOfRangeException, følge din rettelse og kontrollere, at programmet derefter viser 6. Brug kun fiktive data i eksemplet."
            ],
            """
            Titel: Pointtavlen fejler med tre værdier
            Miljø: Lokal C#-konsolapp, Debug-build
            Input: [2, 3, 1]
            Trin: Kør Program.cs med arrayet ovenfor.
            Forventet: Konsollen viser 6.
            Faktisk: IndexOutOfRangeException ved point[i].
            Observation: i = 3 og point.Length = 3.
            """, Issues, Bounds)
    ];

    private static CharacterIllustration Card(string file, string title, string alt, string stage, string summary,
        IReadOnlyList<string> explanation, string? code, params CharacterLink[] sources) =>
        new(title, ImageFolder + file + ".png", alt, 1536, 1024)
        {
            Preview = ImageFolder + file + ".webp",
            Stage = stage,
            Summary = summary,
            Explanation = explanation,
            Code = code,
            Sources = sources
        };

    private static CharacterLink Source(string label, string href) => new(label, href, true, false);
}
