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
             new("Åbn Game Design i Notion", "https://app.notion.com/p/Game-Design-3c6123f464aa8158906ae7c7a3643328", true, false),
             new("Åbn Notion-editoren", "/notion", false, true), new("Vælg nytårstema", "/settings", false, true)])
        {
            Reactions = ReactionsFor("lupp"),
            LargeReactionPreviews = true,
            ReactionsDescription = "LUPPs reaktioner fra Game Design: fra den første spilidé og skitse til playtest, feedback og et nyt forsøg. Find et motiv, åbn originalen, eller hent den som transparent PNG.",
            Scenes = OutfitScenes("lupp",
                "LUPP i mørk smoking og lilla butterfly ved premieren på et spil.",
                "LUPP i turkis blomsterskjorte tegner en spilbane i sandet med sin gule blyant på en veranda ved havet."),
            IllustrationsDescription = "Illustrationer fra Game Design-forløbet og Rune Run, hvor den lilla figur afprøver spillets regler. Åbn et billede i fuld størrelse, eller hent originalen som PNG.",
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
                new("Præsentation og feedback", "/images/characters/lupp/10-praesentation-og-feedback.png", "LUPP viser sin idé, lytter til feedback og retter den til før næste afprøvning.", 1536, 1024),
                RuneRun("spillets-byggesten", "Spillets byggesten", "Rune Runs lilla figur viser forskellen på spillets mål, regler, mekanikker, spilrum, tilstand og visning."),
                RuneRun("core-loop", "Rune Runs gameplay loop", "Den lilla figur finder en rune, bevæger sig hen til den, samler den op og vælger den næste."),
                RuneRun("valg", "To veje – ét reelt valg?", "To baner viser forskellen på en farveændring og et valg mellem en kort, risikabel rute og en længere, sikker vej."),
                RuneRun("double-half", "Fordobl eller halvér", "Den lilla figur løber den samme strækning med halv fart, normal fart og dobbelt fart."),
                RuneRun("skill", "Færdighed og præcision", "Den lilla figur planlægger en rute på kortet og udfører den gennem en smal passage."),
                RuneRun("puzzle-door", "Find nøglen, åbn vejen", "Den lilla figur ser en låst dør, følger sporet til nøglen og får adgang til en ny vej."),
                RuneRun("information", "Kendt, skjult eller tilfældigt?", "Et skakbræt, den lilla figur bag et sænke slagskibe-bræt og et terningkast viser tre typer information."),
                RuneRun("signals", "Mulighed, signal og feedback", "Den lilla figur opdager en åben passage. En pil viser vejen, og runetælleren bekræfter, at runen er samlet op."),
                RuneRun("puzzle", "Sådan løser spilleren en gåde", "Den lilla figur undersøger en mur, prøver en vej rundt og når portalen."),
                RuneRun("06-resource-loop", "Ét boost – hvornår bruger du det?", "Den lilla figur har én opladning, bruger den til at løbe hurtigt og må derefter vente til næste runde.", 1774, 887),
                RuneRun("time-score", "Hvad er omvejen værd?", "Den lilla figur vælger mellem en direkte vej til portalen og en omvej, hvor der kan samles point."),
                RuneRun("random-output", "Samme gennemsnit – ny oplevelse", "Den lilla figur bruger boost. Fast udfald giver to, mens terningen kan give ét eller tre."),
                RuneRun("strategy", "Plan, tilpasning og timing", "Den lilla figur lægger en plan, tilpasser den ved en rullende sten og sætter farten op, da vejen åbner sig."),
                RuneRun("flow", "Udfordringen skal passe til spilleren", "Tre baner viser den lilla figur på en let bane, en passende udfordring og en bane med mange farer."),
                RuneRun("design-wheel", "Tre designgreb – frit tema", "Den lilla lerfigur peger på et hjul med skjult information, tidspres samt risiko og belønning.")
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
             new("Find materialer på Videnscenterportalen", "https://videnscenterportalen.dk/vat/", true, false),
             new("Mød Gummianden", "/characters/gummianden", false, false)])
        {
            Reactions = VatsonEmojiCatalog.All,
            LargeReactionPreviews = true,
            ReactionsDescription = "Alle Vatsons emojis i stor visning – fra vinken og kaffepause til PLC, VR og værkstedsbesøg. Åbn en original i fuld størrelse, eller hent den som PNG.",
            Scenes = OutfitScenes("vatson",
                "Vatson svæver ved en oplyst disk i marineblå smokingvest og turkis butterfly og løfter den ene griber til hilsen.",
                "Vatson svæver i en koralfarvet blomsterskjorte på en veranda med havudsigt. Hans turkise ringbind står på en stol.")
        },
        new("gummianden", "Gummianden", "Talende undervisningsmakker · Debugging", "VATek · Fejlfinding", false,
            "Den gule gummiand, der lytter, når koden driller, og hjælper dig med at finde, hvor den går galt.",
            "Gummianden har én fast vane: Den beder dig forklare, hvad du forventede, før den spørger, hvad der faktisk skete. På LUP kan du se den skifte mellem striktrøje, detektivfrakke, forklæde, flyverhjelm og nattevagtens læsebriller, alt efter hvad fejlen kræver. Vil du forklare din egen kode højt, kan du tale med anden i AI-værktøjet hos Videnscenter for Automation og Teknologi (VATek).",
            ["Rubber duck debugging", "Breakpoints", "Fejlfinding", "VATek"],
            [new("Lyttemakkeren", "/images/characters/gummianden/lyttemakkeren.webp", "En rund gul gummiand med orange næb, terrakottafarvet cardigan og en vinge løftet til hovedet for at lytte"),
             new("Spordetektiven", "/images/characters/gummianden/spordetektiven.webp", "Gummianden i detektivfrakke med en lup, en notesbog og tre spor lagt ud foran sig"),
             new("Værkstedsanden", "/images/characters/gummianden/vaerkstedsanden.webp", "Gummianden med beskyttelsesbriller i panden og en skruetrækker ved en lille turkis testboks"),
             new("Testpiloten", "/images/characters/gummianden/graensetestpiloten.webp", "Gummianden i flyverhjelm og rødt halstørklæde afprøver et håndtag med tre stillinger"),
             new("Logvagten", "/images/characters/gummianden/logvagten.webp", "Gummianden på nattevagt med læsebriller, en lille lampe og en udskrift af loggen"),
             new("VATek-ikon", "/images/characters/gummianden/portrait.png", "VATeks originale gule gummiand med orange næb og en lille tot på turkis baggrund")],
            [new("Forklar det højt", "Fortæl anden, hvilket input du gav programmet, hvad du forventede, og hvad der skete. Gå gennem én linje ad gangen. Hvor er den første linje, hvor programmets opførsel afviger fra din forklaring?"),
             new("Stop, kig og tag næste skridt", "Sæt et breakpoint på en linje lige før den mistænkte ændring, og kør programmet med en debugger. Når programmet stopper, kan du sammenligne variablernes værdier med din forventning. Kør én linje videre, og se, hvilken værdi der ændrer sig."),
             new("Find en fejl, du kan vise frem", "Værkstedsanden fjerner alt, der ikke skal til for at udløse fejlen. Testpiloten prøver tomt input og de værdier, der ligger lige på grænsen. Når fejlen er rettet, bruger de det samme lille eksempel til at skrive en test."),
             new("Tal med anden hos VATek", "I VATeks værktøj stiller den talende and opfølgende spørgsmål og giver små ledetråde. Underviseren kan vælge, hvad eleverne skal øve, tilføje egne materialer og dele anden via link eller pinkode.")],
            [new("Se undervisningsbillederne", "/characters/gummianden#character-gallery-title", false, false),
             new("Se animationerne", "/characters/gummianden#character-animations-title", false, false),
             new("Åbn Gummianden hos VATek", "https://gummianden.vatek.dk/", true, false),
             new("Sådan virker Gummianden", "https://gummianden.vatek.dk/info", true, false),
             new("Besøg VATek.dk", "https://vatek.dk/", true, false),
             new("Mød Vatson", "/characters/vatson", false, false)])
        {
            Illustrations = DuckTeachingMaterials.Illustrations,
            IllustrationsLabel = "infografikker",
            IllustrationsDescription = "Følg en fejl fra det første symptom til en test, der kan fange den næste gang. Find en infografik om fx breakpoints, variabler eller logs, og fold forklaringen ud for at prøve eksemplet i C#.",
            AnimationSubject = "debugging",
            Animations = DuckMotionMaterials.Animations,
            Reactions = ReactionsFor("gummianden"),
            LargeReactionPreviews = true,
            ReactionsDescription = "Gummiandens reaktioner, fra det første »hmm« til testen, der endelig går grøn. Søg efter et motiv, åbn originalen, eller hent den som transparent PNG. Du finder også emojierne i LUPs editor.",
            ScenesDescription = "Besøg lyttekammeratens studie, detektivens sporvæg, testpilotens prøvebane og logvagtens natbord. Åbn et billede i fuld størrelse, eller hent det som PNG.",
            Scenes = [..DuckMotionMaterials.Scenes, ..OutfitScenes("gummianden",
                "Gummianden i turkis fløjlsjakke og butterfly lytter med en vinge ved kinden i en lampelyst bogstue.",
                "Gummianden i koralfarvet hawaiiskjorte sidder i en fletstol på en veranda med udsigt over havet.")]
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
            Illustrations = InfographicsFor("raymond"),
            IllustrationsLabel = "infografikker",
            IllustrationsDescription = "16 infografikker med Raymond om Databaseprogrammering, fag 16474. Serien introducerer alle 24 målpinde på H1, H2 og H3. Find et emne, læs forklaringen, eller hent en PNG til undervisningen. Eksemplerne bruger PostgreSQL og SQL-H1's cafédata; ORM og dokumentdatabaser udvider emnerne til H2 og H3.",
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
                new("Mysteriet om den tabte række", "/images/characters/raymond/scenes/detective-study.png", "Raymond undersøger et ufarligt SQL-mysterium med lup, sagsmappe og et opslag med spor.", 1536, 1024),
                ..OutfitScenes("raymond",
                    "Raymond i mørk smoking og bronzebriller løfter en espressokop i et varmt oplyst bibliotek.",
                    "Raymond i bladtrykt skjorte og bronzebriller holder sin stråhat på en veranda ved havet.")
            ]
        },
        new("chameleon", "Kamæleonen", "Ekspert og underviser i Machine Learning", "Machine Learning · Data og modeller", false,
            "Den grønne violinbygger, der lærer dig at finde mønstre, afprøve modeller og stille gode spørgsmål til data.",
            "Kamæleonen er LUPs ekspert i Machine Learning. Han forklarer data, træning og forudsigelser med små, konkrete eksempler og hjælper dig med at undersøge, om en model også virker på nye data. Han er violinbygger oplært i Italien, men spiller selv banjo. I værkstedet og undervisningen arbejder han på samme måde: observer, justér lidt, afprøv, og lyt efter det, der stadig skurrer.",
            ["Machine Learning", "Nysgerrig håndværker", "Violinbygger · Banjo"],
            [new("Violinbygger", "/images/characters/chameleon/portrait.webp", "Kamæleonen med grønne skæl, orange kam, brunt læderforklæde, ufærdig violin og stemmegaffel"),
             new("Underviser", "/images/characters/chameleon/teacher.webp", "Kamæleonen underviser med enkle eksempelkort og et diagram"),
             new("Banjo", "/images/characters/chameleon/banjo.webp", "Kamæleonen med sin banjo og sit velkendte læderforklæde"),
             new("På feltarbejde", "/images/characters/chameleon/fieldwork.webp", "Kamæleonen undersøger sine omgivelser med en feltnotesbog")],
            [new("Hvorfor en kamæleon?", "Tilpasning er hans metafor for at være nysgerrig på nye data. En trænet model ændrer sig dog ikke automatisk, når den ser et nyt eksempel. Han undersøger først data og kvalitet og beslutter derefter, om der er brug for ændringer eller ny træning."),
             new("Lær mønstret, ikke facitlisten", "Hans motto er: En god model tilpasser sig. En dårlig model husker bare træningsdata. Han bruger det til at forklare generalisering og overfitting: En model skal vurderes på eksempler, der ikke blev brugt til at træne eller vælge den."),
             new("Italiensk håndværk, bluegrass i pausen", "Han lærte at bygge violiner i Italien og kan bruge en eftermiddag på en enkelt trækurve. Når limen tørrer, finder han banjoen frem. Han samler på stemmegafler, nynner bluegrass og kalder en god forklaring for noget, der klinger rigtigt."),
             new("Rolig, præcis og villig til at ombestemme sig", "Han spørger gerne: Hvad så modellen, og hvad har vi endnu ikke vist den? Fejl er spor, der skal undersøges. Han hjælper med klassifikation, regression, klynger, datalækage, metrikker, skævheder og ansvarlig brug af modeller.")],
            [new("Se billeder og materialer i biblioteket", "/library?category=characters", false, false),
             new("Mød Raymond og databaserne", "/characters/raymond", false, false)])
        {
            Illustrations = InfographicsFor("chameleon"),
            IllustrationsLabel = "infografikker",
            IllustrationsDescription = "16 infografikker om Machine Learning: fra data, features og træning til generalisering, evaluering, datadrift og ansvarlig brug. Søg efter et emne, læs forklaringen med faglige kilder, eller hent den originale PNG.",
            AnimationSubject = "Machine Learning",
            Animations =
            [
                new("learn-pattern", "Find mønstret", "Klassifikation · Nye eksempler", "Kamæleonen grupperer eksempler og prøver derefter et nyt blad. Modellen skal kunne bruge et mønster på nye data.",
                    "/images/characters/chameleon/animations/learn-pattern.gif", "/images/characters/chameleon/animations/learn-pattern.png", "Kamæleonen sorterer bladkort efter form og undersøger et nyt eksempel.", 512, 512),
                new("improve-model", "Små skridt mod en bedre model", "Træning · Fejl og justering", "Kamæleonen justerer en model og sammenligner dens forudsigelser med eksemplerne. En pæn kurve alene viser ikke, om modellen virker på nye data.",
                    "/images/characters/chameleon/animations/improve-model.gif", "/images/characters/chameleon/animations/improve-model.png", "Kamæleonen peger på en linje, som justeres tættere på fire datapunkter.", 512, 512),
                new("detect-drift", "Når nye data ændrer sig", "Datadrift · Undersøg før du retter", "Nye data kan se anderledes ud end træningsdata. Undersøg ændringen og mål kvaliteten, før du beslutter, om modellen skal trænes igen.",
                    "/images/characters/chameleon/animations/detect-drift.gif", "/images/characters/chameleon/animations/detect-drift.png", "Kamæleonen opdager orange datapunkter uden for en kendt grøn gruppe og undersøger dem.", 512, 512)
            ],
            Reactions = ReactionsFor("chameleon"),
            LargeReactionPreviews = true,
            ReactionsDescription = "48 transparente emojis og reaktioner med Kamæleonen: varme hverdagsudtryk, opmuntring, Machine Learning og små glimt fra violin- og banjoværkstedet. Søg efter et motiv, åbn originalen, eller hent en PNG.",
            ScenesDescription = "Kom med i det italienske violinværksted, til banjo i aftensolen, på datajagt i drivhuset og ind i undervisningsstudiet. Åbn billederne i stor størrelse, eller hent dem som PNG.",
            Scenes =
            [
                new("Violinbyggerens værksted", "/images/characters/chameleon/scenes/workshop.png", "Kamæleonen bygger en violin i et varmt italiensk håndværkerværksted.", 1536, 1024) { Preview = "/images/characters/chameleon/scenes/workshop.webp" },
                new("Banjo i aftensolen", "/images/characters/chameleon/scenes/banjo-evening.png", "Kamæleonen spiller banjo i en rolig stund ved solnedgang.", 1536, 1024) { Preview = "/images/characters/chameleon/scenes/banjo-evening.webp" },
                new("Data i drivhuset", "/images/characters/chameleon/scenes/data-garden.png", "Kamæleonen undersøger planter og eksempler med sin notesbog i et lyst drivhus.", 1536, 1024) { Preview = "/images/characters/chameleon/scenes/data-garden.webp" },
                new("Læringsværkstedet", "/images/characters/chameleon/scenes/teaching-studio.png", "Kamæleonen forklarer grupper og mønstre med enkle farvede datapunkter i et lyst undervisningsstudie.", 1536, 1024) { Preview = "/images/characters/chameleon/scenes/teaching-studio.webp" },
                ..OutfitScenes("chameleon",
                    "Kamæleonen i flaskegrøn fløjlsjakke og kobberfarvet butterfly stemmer sin banjo i en lille koncertsal.",
                    "Kamæleonen i en koralfarvet blomsterskjorte spiller banjo på en træterrasse ved havet.")
            ]
        },
        new("whale", "Docker", "Havnekaptajn · Underviser i DevNet og deployment", "Deploy or die · DevNet", false,
            "Den gamle pukkelhval, der lærer dig at bygge, udrulle og holde øje med containerbaserede applikationer.",
            "Docker har sejlet containerskibe i al slags vejr. Nu underviser den gamle havnekaptajn i Linux, API’er, netværk og udrulning med Docker Compose. Han fører stadig logbog i hånden. Hvis nogen siger »den er nok klar«, lægger han kaffen fra sig og beder om at se målingerne.",
            ["Docker og Compose", "Deploy og drift", "Havnekaptajnen"],
            [new("Havnekaptajnen", "/images/characters/whale/portrait.webp", "Den gamle pukkelhval Docker med sennepsgul hue, blå striktrøje, radio og tidevandsbog"),
             new("På broen", "/images/characters/whale/broen.webp", "Docker holder udkig med en messingkikkert i sin striktrøje og sennepsgule hue"),
             new("Med Kanarien", "/images/characters/whale/vaerksted.webp", "Docker viser sin hjemmelavede slæbebåd med den gule gummiand Kanarien"),
             new("På nattevagt", "/images/characters/whale/natvagt.webp", "Docker holder øje med radioen med runde læsebriller og sin sennepsgule hue")],
            [new("Lasten skal passe til skibet", "På kajen viser kaptajnen, hvordan en Dockerfile bliver til et image, og hvordan det samme image kan køres som en container flere steder. Med Compose samler han de tjenester, netværk og volumes, som applikationen skal bruge."),
             new("Kanarien sejler først", "Han bygger fjernstyrede slæbebåde af sardindåser. Den gule gummiand Kanarien får den nye container først. Kaptajnen holder øje med, om båden holder kursen, før han sender resten af flåden af sted. Det er hans model for en canary-udrulning, hvor en lille del af trafikken prøver den nye version først."),
             new("Ingen sejlads på mavefornemmelse", "Den grønne lampe er kun begyndelsen. Kaptajnen tjekker, om appen svarer som forventet, læser logs og holder øje med fejl og svartider. Hvis noget går galt, ved han, hvilken version der sidst virkede."),
             new("En gammel sejler med faste vaner", "Han samler på tidevandstabeller, drikker kaffen af et bulet emaljekrus og har altid et stykke snor til en fortøjning. Hver ændring får en linje i logbogen, så den næste på vagt kan se, hvad der skete.")],
            [new("Se Deploy or Die-forløbet", "/courses/template/deploy-or-die", false, false),
             new("Se DevNet på Målpinde", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false),
             new("Læs om Docker Compose", "https://docs.docker.com/compose/", true, false),
             new("Mød Raymond og databaserne", "/characters/raymond", false, false)])
        {
            Illustrations = WhaleMaterials.Illustrations,
            IllustrationsLabel = "infografikker",
            IllustrationsDescription = "16 infografikker fra Linux og API-kald til containere, Compose og sikker udrulning. Se koblingen til DevNets syv målpinde, åbn forklaringerne, eller hent billederne som PNG.",
            AnimationSubject = "Docker og deployment",
            Animations = WhaleMaterials.Animations,
            Reactions = ReactionsFor("whale"),
            LargeReactionPreviews = true,
            ReactionsDescription = "48 transparente emojis med kaptajnen til hverdagen, en travl deploy og en tur på kajen. Søg efter et motiv, åbn originalen, eller hent en PNG.",
            ScenesDescription = "Følg kaptajnen fra tidevandsbogen ved kajen til værkstedsbordet, badekarrets testhavn og en rigtig containerudrulning. Åbn billederne i fuld størrelse, eller hent dem som PNG.",
            Scenes = [..WhaleMaterials.Scenes, ..OutfitScenes("whale",
                "Docker i mørk jakke, bordeaux butterfly og gul hue løfter et glas i skibets salon med havnen bag koøjet.",
                "Docker i blå hawaiiskjorte med orange blomster og gul hue skubber et miniaturefragtskib gennem det lave vand ved en træbro.")]
        },
        new("bee", "Cyberbien", "Underviser i cybersikkerhed", "Cybersikkerhed · Fra forebyggelse til respons", false,
            "Cyberbien viser, hvordan du beskytter konti, kode og netværk, og hvad du gør, når en alarm går.",
            "Cyberbien underviser i cybersikkerhed fra den første risikovurdering til arbejdet efter en hændelse. Han bygger miniaturebistader af gamle netværksswitche, og i hans øvelsesnet står der altid et glas lokkehonning. Rører nogen det, vil han vide, hvad der skete, før han beslutter, hvad han gør.",
            ["Cybersikkerhed", "Honeypots", "Sikker software", "Netværk og cloud", "Hændelser og respons"],
            [new("Cyberbien", "/images/characters/bee/portrait.webp", "En gul bi i marineblå hættetrøje med gule snore, cyanglødende kodebriller og en tommel i vejret"),
             new("Lokkehonningen", "/images/characters/bee/honeypot.webp", "Cyberbien bærer et glas lokkehonning i en gennemsigtig boks med statuslys"),
             new("På vagt", "/images/characters/bee/logvagt.webp", "Cyberbien holder en tablet og gransker en alarm"),
             new("På sporet", "/images/characters/bee/feltarbejde.webp", "Cyberbien læner sig frem med en lille lup for at undersøge et spor"),
             new("Klar til øvelse", "/images/characters/bee/sikkerhedsoevning.webp", "Cyberbien står med et gennemsigtigt cyanblåt skjold med hængelås")],
            [new("Et glas lokkehonning", "En honeypot efterligner noget, der kan tiltrække uønsket aktivitet. I biens isolerede øvelsesnet er det en lokketjeneste uden rigtige data. Når nogen rører den, kan eleverne undersøge, hvad loggen viser, og afgøre, om der er noget at reagere på."),
             new("Byg flere lag af forsvar", "Begynd med at beslutte, hvad du vil beskytte. Giv hver konto de rettigheder, den skal bruge, adskil dele af netværket, og brug logs og alarmer til at se, om forsvaret virker. Øv også, hvordan du gendanner systemet, hvis noget går galt."),
             new("Når noget summer forkert", "En alarm er anledning til at undersøge. Cyberbien samler spor, afgrænser hændelsen og vælger sammen med holdet, hvad de skal isolere, rette og gendanne. Bagefter gennemgår de, hvad de vil ændre, før det sker igen."),
             new("Tre forskellige roller", "Bæver bygger og tester det blå forsvar. Ræv undersøger svagheder med aftalt tilladelse. Cyberbien samler arbejdet med risiko, forebyggelse, overvågning og håndtering af hændelser i samme øvelse.")],
            [new("Se undervisningsbillederne", "/characters/bee#character-gallery-title", false, false),
             new("Se animationerne", "/characters/bee#character-animations-title", false, false),
             new("Se fagene på Målpinde", "https://maalpinde.lup.dk/", true, false),
             new("Sikkerhed og test", "https://testing.mercantec.tech/", true, false),
             new("Informationssikkerhed og awareness", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false),
             new("Mød Bæver", "/characters/beaver", false, false),
             new("Mød Ræv", "/characters/fox", false, false)])
        {
            Illustrations = BeeTeachingMaterials.Illustrations,
            IllustrationsLabel = "infografikker",
            IllustrationsDescription = "Fra phishing, adgang og sikker kode til netværk, cloud, honeypots og hændelseshåndtering. Vælg et emne, læs forklaringen og prøv øvelsen med dit hold.",
            AnimationSubject = "cybersikkerhed",
            Animations = BeeMotionMaterials.Animations,
            Reactions = ReactionsFor("bee"),
            LargeReactionPreviews = true,
            ReactionsDescription = "Cyberbien tjekker en mistænkelig mail, sætter en honeypot ud og fejrer, når testen går grøn. Søg efter et motiv, hent en transparent PNG, eller brug emojierne i LUPs editor.",
            ScenesDescription = "Se Cyberbien ved lokkeserveren, på nattevagt, i testlaboratoriet og under en hændelsesøvelse. Åbn et billede i fuld størrelse, eller hent det som PNG.",
            Scenes = [..BeeMotionMaterials.Scenes, ..OutfitScenes("bee",
                "Cyberbien i marineblå smoking med gylden butterfly præsenterer sit miniaturebistade på en aftenudstilling.",
                "Cyberbien i turkis blomsterskjorte lader en gul drage flyve på en solrig kyststi.")]
        },
        new("bat", "Næsebladet", "Underviser i IoT og embedded", "IoT · Fra signal til handling", false,
            "Næsebladet sender et signal ud og kræver bevis for, at det rigtige svar kom tilbage. Følg målingen fra sensor og firmware til netværk og Linux.",
            "Næsebladet er en teatralsk lyddesigner med et hak i øret, brede skuldre og meget lidt tålmodighed med upræcise målinger. Han går ned i tomme parkeringskældre for at optage ekkoer. Hans første album består af lyden af den samme teske, der rammer gulvet forskellige steder. I laboratoriet spørger han med samme myndighed: «Jeg hørte et svar. Hvordan ved du, hvad der svarede?»",
            ["IoT", "Sensorer og ekkolokalisering", "Firmware", "MQTT", "Embedded Linux", "Fejlfinding"],
            [new("Næsebladet", "/images/characters/bat/portrait.webp", "Oprejst rødbrun flagermus med brede skuldre, kantede violette vingefolder, bredt næseblad og et strengt blik"),
             new("Lyddesigneren", "/images/characters/bat/lyddesigner.webp", "Næsebladet lytter efter ekkoer med sit optageudstyr"),
             new("Kalibrering", "/images/characters/bat/kalibrering.webp", "Næsebladet undersøger en sensor ved målebænken"),
             new("Dirigenten", "/images/characters/bat/dirigenten.webp", "Næsebladet dirigerer med sin teske og holder øje med forsøget"),
             new("Natoptagelse", "/images/characters/bat/natoptagelse.webp", "Næsebladet holder en tændt lommelygte, mens han gør klar til natoptagelsen")],
            [new("Den samme ske, tre forskellige rum", "Gentag forsøget med den samme ske og sammenlign optagelserne. Vægge, afstand og baggrundsstøj ændrer det, mikrofonen opfanger. Brug forskellene til at diskutere, hvilke dele af målingen du kan stole på."),
             new("Mål turen frem og tilbage", "En ultralydssensor sender en puls og måler tiden frem til ekkoet. Afstanden er lydens hastighed ganget med tiden og delt med to, fordi pulsen rejser begge veje. Hvis ekkoet udebliver, registrerer programmet et timeout, så manglende svar ikke bliver læst som nul centimeter."),
             new("Fra sensor til handling", "Vælg sensor og måleinterval, læs signalet med en mikrocontroller, og lad resten af programmet arbejde videre, mens du venter. Send værdien med enhed og tidspunkt via MQTT, eller reagér lokalt, hvis netværket forsvinder."),
             new("Find fejlen ét sted ad gangen", "Tjek forsyning, forbindelser og signal, før du følger målingen ind i firmwaren, hen til MQTT-brokeren og videre til Linux. Brug logs til at se, hvor værdien forsvinder, og test, hvordan enheden opfører sig, når en sensor eller forbindelsen fejler.")],
            [new("Se undervisningsbillederne", "/characters/bat#character-gallery-title", false, false),
             new("Se animationerne", "/characters/bat#character-animations-title", false, false),
             new("IoT og embedded på Målpinde", "https://maalpinde.lup.dk/sites/#iot", true, false),
             new("Arduino Oplà-guide på Mercantec Games", "https://games.mercantec.tech/guide", true, false),
             new("Mød Cyberbien", "/characters/bee", false, false)])
        {
            Illustrations = BatTeachingMaterials.Illustrations,
            IllustrationsLabel = "infografikker",
            IllustrationsDescription = "Illustrationerne og øvelserne er knyttet til 24 målpinde i IoT og Embeddede systemer (17682) og otte i IoT og Embeddede Linux systemer (17683). Vælg et emne, og prøv øvelsen med dit hold.",
            AnimationSubject = "IoT og embedded",
            Animations = BatMotionMaterials.Animations,
            Reactions = ReactionsFor("bat"),
            LargeReactionPreviews = true,
            ReactionsDescription = "Næsebladet lytter, måler, finder støj og sender beskeder med MQTT. Hent emojierne som transparente PNG’er, eller brug dem i LUPs editor.",
            ScenesDescription = "Besøg parkeringskælderen, målebænken, drivhuset og scenen, hvor Næsebladet præsenterer sit teskealbum. Åbn billederne i fuld størrelse, eller hent dem som PNG.",
            Scenes = [..BatMotionMaterials.Scenes, ..OutfitScenes("bat",
                "Næsebladet i brokadevest og butterfly står på et dirigentpodie i et tomt teater og løfter en teske som taktstok.",
                "Næsebladet i grøn hawaiiskjorte med orange blomster holder øje med en optager, en teske og et glas lemonade på en terrasse ved havet.")]
        },
        new("beaver", "Bæver", "Underviser i Sikkerhed og test · Blå team", "Sikkerhed og test", false,
            "Den rolige systembygger på blåt team: sikker kode, adgangskontrol, logs og tests, der holder løsningen robust.",
            "Bæver underviser i Sikkerhed og test fra det blå teams perspektiv. Med tablet og logbog undersøger Bæver, hvordan vi beskytter systemet, opdager fejl og gør rettelser holdbare. Arbejdet er roligt, grundigt og praktisk: forstå problemet, styrk løsningen, og skriv en test, der passer på den fremover.",
            ["Blå team", "Sikker kode", "Regressionstests"],
            [new("Portræt", "/images/characters/beaver/portrait.png", "Bæver, en venlig brun bæver med tydelig flad hale, blå underviserjakke og en tablet med logs")],
            [new("Hvorfor en bæver?", "Bæverens byggeri og vedligeholdelse er en god metafor for robuste systemer. Det handler om at bygge med omtanke, holde øje med svage steder og forstærke løsningen, når forholdene ændrer sig."),
             new("Sikkerhed, der kan efterprøves", "Bæver arbejder med sikre standardindstillinger, adgangskontrol, logging og automatiserede regressionstests. Når Ræv finder en svaghed, undersøger de sammen årsagen og kontrollerer, at rettelsen virker.")],
            [new("Mød Ræv på rødt team", "/characters/fox", false, false),
             new("Se Cyberbiens undervisning", "/characters/bee", false, false)])
        {
            Reactions = ReactionsFor("beaver"),
            LargeReactionPreviews = true,
            ReactionsDescription = "Bævers reaktioner fra det blå team: rolige forklaringer, opmuntring, adgangskontrol, logs og tests. Åbn en original i fuld størrelse, eller hent en transparent PNG.",
            Scenes = OutfitScenes("beaver",
                "Bæver retter sin gyldne butterfly ved et klaver. Han har blå fløjlssmoking og briller, og den brede, flade hale ligger på gulvet.",
                "Bæver har en blå blomsterskjorte på og holder fast i en flettet parasol på en træbro ved havet. Hans flade hale hviler på broen.")
        },
        new("fox", "Ræv", "Underviser i Sikkerhed og test · Rød team", "Sikkerhed og test", false,
            "Den nysgerrige sikkerhedstester på rødt team: undersøger angrebsflader, uventede input og systemets rettigheder.",
            "Ræv underviser i Sikkerhed og test fra det røde teams perspektiv. Med notesbog og testskitser udfordrer Ræv de antagelser, et system bygger på: Hvad hvis inputtet er anderledes, eller brugeren prøver en anden vej? Testen foregår inden for aftalte rammer, og fundene dokumenteres, så de kan forstås, rettes og testes igen.",
            ["Rød team", "Etisk sikkerhedstest", "Angrebsflader"],
            [new("Portræt", "/images/characters/fox/portrait.png", "Ræv, en venlig rødbrun ræv med busket hale, røde tøjdetaljer og en notesbog med testskitser")],
            [new("Hvorfor en ræv?", "Ræven repræsenterer opfindsomhed og evnen til at undersøge alternative veje. Det er en god metafor for etisk sikkerhedstest: at udfordre antagelser og finde grænsetilfælde, som den første løsning måske har overset."),
             new("Find svagheden, og del forståelsen", "Ræv arbejder med angrebsflader, uventede input og afprøvning af rettigheder. Ræv og Bæver er kolleger: De deler fund, styrker løsningen og skriver tests, der viser, om forbedringen holder.")],
            [new("Mød Bæver på blåt team", "/characters/beaver", false, false),
             new("Se Cyberbiens undervisning", "/characters/bee", false, false)])
        {
            Reactions = ReactionsFor("fox"),
            LargeReactionPreviews = true,
            ReactionsDescription = "Rævs reaktioner fra det røde team: nysgerrige spørgsmål, nye idéer, undersøgende tests og fund, der deles med kollegerne. Åbn en original i fuld størrelse, eller hent en transparent PNG.",
            Scenes = OutfitScenes("fox",
                "Ræv står i en teaterfoyer i bordeaux fløjlsjakke og sort butterfly med notesbogen åben og den buskede hale synlig.",
                "Ræv står i rød blomsterskjorte på en træbro ved et vandfald og holder notesbogen åben ved rækværket.")
        }
    ];

    public static CharacterProfile? Find(string? slug) =>
        All.FirstOrDefault(character => string.Equals(character.Slug, slug, StringComparison.OrdinalIgnoreCase));

    private static CharacterIllustration[] OutfitScenes(string slug, string fancyFridayAlt, string hawaiiAlt) =>
    [
        OutfitScene(slug, "fancy-friday", "Fancy Friday", fancyFridayAlt),
        OutfitScene(slug, "hawaii-onsdag", "Hawaii-onsdag", hawaiiAlt)
    ];

    private static CharacterIllustration OutfitScene(string slug, string file, string title, string alt)
    {
        var path = $"/images/characters/{slug}/scenes/{file}";
        return new(title, path + ".png", alt, 1536, 1024) { Preview = path + ".webp" };
    }

    private static CharacterIllustration RuneRun(string file, string title, string alt, int width = 1536, int height = 1024) =>
        new(title, $"/images/characters/lupp/rune-run/{file}.png", alt, width, height)
        {
            Preview = $"/images/characters/lupp/rune-run/{file}.webp"
        };

    private static CharacterIllustration[] ReactionsFor(string slug) => CharacterEmojiCatalog.All
        .Where(asset => asset.CharacterSlug == slug)
        .Select(asset => new CharacterIllustration(asset.Title, asset.Image, asset.Alt, asset.Width, asset.Height)
        {
            Preview = asset.Preview
        }).ToArray();

    private static CharacterIllustration[] InfographicsFor(string slug) => CharacterInfographicCatalog.All.Concat(ChameleonInfographicCatalog.All)
        .Where(asset => asset.CharacterSlug == slug)
        .Select(asset => new CharacterIllustration(asset.Title, asset.Image, asset.Alt, asset.Width, asset.Height)
        {
            Preview = asset.Preview,
            Stage = asset.Stage,
            Objectives = asset.Objectives,
            Summary = asset.Summary,
            Explanation = asset.Explanation,
            Code = asset.Sql,
            Sources = asset.Sources.Select(source => new CharacterLink(source.Label, source.Href, true, false)).ToArray()
        }).ToArray();
}

public sealed record CharacterProfile(string Slug, string Name, string Role, string Home, bool ComingSoon,
    string Summary, string Description, IReadOnlyList<string> Tags, IReadOnlyList<CharacterAppearance> Appearances,
    IReadOnlyList<CharacterFeature> Features, IReadOnlyList<CharacterLink> Links)
{
    public string Path => "/characters/" + Slug;
    public IReadOnlyList<CharacterIllustration> Illustrations { get; init; } = [];
    public string IllustrationsLabel { get; init; } = "illustrationer";
    public string IllustrationsDescription { get; init; } = "Åbn en illustration i fuld størrelse, eller hent originalen som PNG.";
    public IReadOnlyList<CharacterIllustration> Reactions { get; init; } = [];
    public bool LargeReactionPreviews { get; init; }
    public string? ReactionsDescription { get; init; }
    public IReadOnlyList<CharacterIllustration> Scenes { get; init; } = [];
    public string ScenesDescription { get; init; } = "Små fortællinger med karakteren. Åbn et billede i fuld størrelse, eller hent en PNG.";
    public IReadOnlyList<CharacterAnimation> Animations { get; init; } = [];
    public string AnimationSubject { get; init; } = "databaser";
    public IEnumerable<CharacterMaterialGroup> Materials
    {
        get
        {
            if (Reactions.FirstOrDefault() is { } reaction)
                yield return new("emojis", Reactions.Count, "character-reactions-title", reaction.Preview, reaction.Alt);
            if (Scenes.FirstOrDefault() is { } scene)
                yield return new("miljøer", Scenes.Count, "character-scenes-title", scene.Preview, scene.Alt);
            if (Illustrations.FirstOrDefault() is { } illustration)
                yield return new(IllustrationsLabel, Illustrations.Count, "character-gallery-title", illustration.Preview, illustration.Alt);
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
    public string? Stage { get; init; }
    public IReadOnlyList<int> Objectives { get; init; } = [];
    public string? Summary { get; init; }
    public IReadOnlyList<string> Explanation { get; init; } = [];
    public string? Code { get; init; }
    public IReadOnlyList<CharacterLink> Sources { get; init; } = [];
}
public sealed record CharacterAnimation(string Id, string Title, string Topic, string Description, string Image, string Poster, string Alt, int Width, int Height);
