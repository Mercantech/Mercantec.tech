namespace Mercantec.Tech.Web.Characters;

internal static class WhaleMaterials
{
    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        new("Python læser status", "/images/characters/whale/infographics/01-python-laeser-status.png", "Havnekaptajnen ved kajen med et diagram, hvor et Python-script henter HTTPS-status fra en tjeneste og aflæser statuskoden.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/01-python-laeser-status.webp",
            Stage = "DevNet",
            Objectives = [1, 3, 5],
            Summary = "Brug Python til at lave et HTTPS-kald med en tidsgrænse og læse statuskoden.",
            Explanation = ["Start med et lille script, der gør én ting. Her åbner Python en HTTPS-forbindelse, venter højst fem sekunder på blokerende netværksoperationer og udskriver svarets statuskode. Udskift adressen med en rute på jeres egen testtjeneste.", "urllib.request kan rejse HTTPError ved et HTTP-fejlsvar og URLError ved problemer med forbindelsen. Prøv begge situationer i et øvemiljø. Når kontrollen skal bruges i en pipeline, så giv den en tydelig exitkode og undgå at logge tokens."],
            Code = "from urllib.request import urlopen\n\nwith urlopen(\"https://example.com/\", timeout=5) as svar:\n    print(svar.status)",
            Sources = [new("Python · urllib.request", "https://docs.python.org/3/library/urllib.request.html", true, false), new("IETF · HTTP-semantik", "https://www.rfc-editor.org/rfc/rfc9110.html", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Find vej i Linux", "/images/characters/whale/infographics/02-find-vej-i-linux.png", "Havnekaptajnen ved sit værkstedsbord ved siden af et rutediagram, hvor pwd viser mappen, ls oplister den, cd skifter den og tail læser en log.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/02-find-vej-i-linux.webp",
            Stage = "DevNet",
            Objectives = [2],
            Summary = "Orientér dig med pwd, ls og cd, og læs de seneste linjer i en lokal logfil.",
            Explanation = ["pwd viser din aktuelle arbejdsmappe, ls -la viser også skjulte filer, og cd flytter din skal til en ny mappe. I eksemplet er deploy en mappe i projektet. En forkert mappe kan få et ellers korrekt script til at læse en anden konfiguration.", "tail -n 20 viser slutningen af en fil uden at ændre den. Se efter tidsstempler og den første fejl, der kan forklare de næste. Kør eksemplet mod en logfil, du har adgang til, og fjern følsomme værdier før deling."],
            Code = "pwd\nls -la\ncd deploy\ntail -n 20 service.log",
            Sources = [new("GNU · shellværktøjer", "https://www.gnu.org/software/coreutils/manual/html_node/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Fra request til response", "/images/characters/whale/infographics/03-request-og-response.png", "Havnekaptajnen ved siden af et HTTP-diagram med en klient, et GET-kald til /status og et API, der svarer 200 med JSON.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/03-request-og-response.webp",
            Stage = "DevNet",
            Objectives = [3, 4],
            Summary = "Følg et GET-kald gennem HTTP og aflæs både statuskode og svarets JSON.",
            Explanation = ["En HTTP-forespørgsel har en metode og et mål. GET beder serveren om en repræsentation af ressourcen. Ruten /status er en aftale i jeres API. Accept kan fortælle serveren, hvilket format klienten kan bruge.", "Serverens statuskode og svarindhold supplerer hinanden. 200 betyder, at denne forespørgsel lykkedes, mens f.eks. 404 viser, at ressourcen ikke blev fundet. Prøv både en kendt og en ukendt rute og forklar, hvor i forbindelsen hver fejl opstår."],
            Code = "curl -i -H 'Accept: application/json' \\\n  http://localhost:8080/status",
            Sources = [new("IETF · HTTP-semantik", "https://www.rfc-editor.org/rfc/rfc9110.html", true, false), new("curl · kommandoer og flag", "https://curl.se/docs/manpage.html", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("En fælles aftale om API’et", "/images/characters/whale/infographics/04-aftalen-om-api.png", "Havnekaptajnen ved siden af to services, klient og API, som bruger den samme OpenAPI-kontrakt med GET /status og et JSON-svar.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/04-aftalen-om-api.webp",
            Stage = "DevNet",
            Objectives = [3, 4],
            Summary = "Brug OpenAPI til at beskrive en HTTP-rute og teste, at den virkelige tjeneste følger aftalen.",
            Explanation = ["En OpenAPI-beskrivelse gør en HTTP-grænseflade læsbar for både mennesker og værktøjer. Den kan beskrive metoder, ruter, parametre, svarkoder og skemaer for data. Det hjælper f.eks. to grupper, der udvikler klient og server hver for sig.", "En beskrivelse garanterer ikke, at den kørende tjeneste følger kontrakten. Test et faktisk kald og sammenlign statuskode, indholdstype og JSON-felter med aftalen. Versionér ændringer, som eksisterende klienter ikke kan håndtere. I eksemplet er paths tom. Tilføj GET /status med forventet 200-svar som næste skridt."],
            Code = "openapi: 3.1.1\ninfo:\n  title: Kajstatus API\n  version: 1.0.0\npaths: {}",
            Sources = [new("OpenAPI Specification 3.1.1", "https://spec.openapis.org/oas/v3.1.1.html", true, false), new("IETF · HTTP-semantik", "https://www.rfc-editor.org/rfc/rfc9110.html", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Brug HTTPS til API’et", "/images/characters/whale/infographics/05-https-til-api.png", "Havnekaptajnen ved siden af tre trin i et HTTPS-kald, hvor klienten validerer certifikatet før krypteret HTTP-trafik til API’et.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/05-https-til-api.webp",
            Stage = "DevNet",
            Objectives = [4, 5],
            Summary = "Lav et HTTPS-kald og forstå, hvad certifikatkontrol og kryptering hver især gør.",
            Explanation = ["TLS beskytter data under transport og lader klienten validere serverens certifikat. Et navn, der ikke matcher URL’en, eller et certifikat fra en ukendt udsteder skal undersøges. I et lokalt miljø kan en betroet lokal CA være en del af opsætningen.", "HTTPS beskytter transporten, mens API’et selv skal afgøre, hvad en bruger har lov til. curl --fail gør HTTP-status fra 400 til en kommandofejl. Brug jeres eget HTTPS-endpoint og hold adgangstokens ude af delt terminalhistorik."],
            Code = "curl --fail --show-error \\\n  https://api.example.com/status",
            Sources = [new("IETF · TLS 1.3", "https://www.rfc-editor.org/rfc/rfc8446.html", true, false), new("IETF · HTTP-semantik", "https://www.rfc-editor.org/rfc/rfc9110.html", true, false), new("curl · kommandoer og flag", "https://curl.se/docs/manpage.html", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Fra Dockerfile til container", "/images/characters/whale/infographics/06-fra-dockerfile-til-container.png", "Havnekaptajnen ved siden af en grafisk kæde, hvor Dockerfile bygges til et image og startes som en container.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/06-fra-dockerfile-til-container.webp",
            Stage = "Docker",
            Objectives = [7],
            Summary = "Skeln mellem instruktionerne til build, det færdige image og containeren, der kører appen.",
            Explanation = ["Dockerfile er opskriften. En build-kommando læser instruktionerne og skaber et image med filerne og indstillingerne til at starte en proces. Et image kan distribueres og genbruges på værter med en passende platform.", "En container er en instans af imaget med en proces og et skrivbart lag. Flere containere kan starte fra samme image og få hver sin konfiguration. Når containeren erstattes, skal data med længere levetid ligge i et passende volume eller en separat tjeneste."],
            Code = "docker build -t kajstatus:1.4.0 .\ndocker run --rm -p 127.0.0.1:8080:80 kajstatus:1.4.0",
            Sources = [new("Dockerfile-reference", "https://docs.docker.com/reference/dockerfile/", true, false), new("Docker · hvad er en container?", "https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-a-container/", true, false), new("Docker · image build", "https://docs.docker.com/reference/cli/docker/image/build/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Byg og mærk et image", "/images/characters/whale/infographics/07-byg-og-maerk-image.png", "Havnekaptajnen i værkstedet med tre billedlag, et build og mærker for kajstatus:1.4.0 og et uforanderligt digest.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/07-byg-og-maerk-image.webp",
            Stage = "Docker",
            Objectives = [7],
            Summary = "Byg fra den rigtige kontekst, begræns filerne med .dockerignore og brug et versions-tag eller digest.",
            Explanation = ["Punktummet i kommandoen vælger den aktuelle mappe som build-kontekst. .dockerignore kan udelade f.eks. .git, lokale builds og hemmelige filer, inden konteksten sendes til byggeren. Gem credentials til build via egnede secret-mekanismer.", "Et tag som 1.4.0 er et navn, der kan flyttes til et andet image. Et digest identificerer bestemt indhold. Brug tags til læsbare versioner, og gem det deployede digest, når du skal kunne genskabe det præcise build."],
            Code = "docker build -t kajstatus:1.4.0 .\ndocker image inspect kajstatus:1.4.0",
            Sources = [new("Docker · image build", "https://docs.docker.com/reference/cli/docker/image/build/", true, false), new("Docker · image tag", "https://docs.docker.com/reference/cli/docker/image/tag/", true, false), new("Dockerfile-reference", "https://docs.docker.com/reference/dockerfile/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Send image til et registry", "/images/characters/whale/infographics/08-image-til-registry.png", "Havnekaptajnen ved et diagram med buildmaskine, registry og deployserver, hvor image-transporten er markeret med push og pull.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/08-image-til-registry.webp",
            Stage = "Docker",
            Objectives = [7],
            Summary = "Mærk et image med registry-adresse, publicér det fra buildmaskinen og hent samme version på serveren.",
            Explanation = ["Et image-navn kan pege på både registry, namespace, repository og tag. Med tag giver du et eksisterende image endnu et navn. Push publicerer til det angivne repository, når din konto har tilladelse til at skrive.", "Deployserveren kan hente en godkendt pakke med en konto, der kun har den nødvendige læseadgang. Udskift eksempeldomænet med jeres registry og brug sikker login eller credentials fra jeres CI-system. Byg én gang og flyt samme image mellem miljøer."],
            Code = "docker tag kajstatus:1.4.0 registry.example.com/kajstatus:1.4.0\ndocker push registry.example.com/kajstatus:1.4.0",
            Sources = [new("Docker · image tag", "https://docs.docker.com/reference/cli/docker/image/tag/", true, false), new("Docker · image push", "https://docs.docker.com/reference/cli/docker/image/push/", true, false), new("Docker · Compose pull", "https://docs.docker.com/reference/cli/docker/compose/pull/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Én Compose-fil, flere tjenester", "/images/characters/whale/infographics/09-compose-tjenester.png", "Havnekaptajnen med et diagram af compose.yaml, der knytter web, API og database sammen i et projekt.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/09-compose-tjenester.webp",
            Stage = "Compose",
            Objectives = [6, 7],
            Summary = "Brug Compose til at beskrive en flertjeneste-app og kontrollere den færdige konfiguration.",
            Explanation = ["I compose.yaml beskriver services, hvilke processer der skal køre. En service kan pege på et publiceret image eller bygges fra kildekode. Netværk afgør, hvem tjenesterne kan nå, mens volumes giver et sted til data, der skal leve videre.", "docker compose config samler konfigurationen og indsætter variabler. Flaget --quiet validerer uden at udskrive den fulde, opløste konfiguration. Læg konfigurationen under versionsstyring, og hold miljøafhængige værdier og hemmeligheder udenfor."],
            Code = "docker compose config --quiet\ndocker compose up -d\ndocker compose ps",
            Sources = [new("Docker · Compose Quickstart", "https://docs.docker.com/compose/gettingstarted/", true, false), new("Docker · Compose config", "https://docs.docker.com/reference/cli/docker/compose/config/", true, false), new("Docker · Compose up", "https://docs.docker.com/reference/cli/docker/compose/up/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Hvem kan nå hvad?", "/images/characters/whale/infographics/10-hvem-kan-naa-hvad.png", "Havnekaptajnen med et netværkskort, hvor browseren når web, og kun web, API og database deler det interne netværk.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/10-hvem-kan-naa-hvad.webp",
            Stage = "Compose",
            Objectives = [6, 7],
            Summary = "Skeln mellem en publiceret værtsport og forbindelser mellem services på Compose-netværket.",
            Explanation = ["På et delt Compose-netværk kan en service typisk findes med sit servicenavn. En API-container kan f.eks. bruge db:5432 til at forbinde til PostgreSQL. Brug serviceporten på det interne netværk og undgå at binde din app til en midlertidig container-IP. Eksemplet med getent forudsætter, at værktøjet findes i API-imaget.", "En ports-regel publicerer en port på værten. Til lokal øvelse kan en binding til 127.0.0.1 begrænse porten til selve værten. I webeksemplet er databasen intern, og browserens trafik går via en webserver eller proxy."],
            Code = "docker compose ps\ndocker compose exec api getent hosts db",
            Sources = [new("Docker · netværk i Compose", "https://docs.docker.com/compose/how-tos/networking/", true, false), new("Docker · Compose ps", "https://docs.docker.com/reference/cli/docker/compose/ps/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Data der overlever containeren", "/images/characters/whale/infographics/11-data-overlever-container.png", "Havnekaptajnen ved et diagram, hvor en databasecontainer erstattes, og et navngivet volume tilkobles igen med data intakte.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/11-data-overlever-container.webp",
            Stage = "Compose",
            Objectives = [7],
            Summary = "Forstå forskellen på containerens skrivbare lag og et navngivet volume til data.",
            Explanation = ["Data i containerens skrivbare lag følger den container. Et navngivet volume ligger separat og kan monteres igen, når tjenestens container erstattes. Mount-stien afhænger af det konkrete databaseimage og dets version.", "Et volume gør data vedvarende på en vært. Tab af værten, sletning eller forkerte ændringer kan stadig ødelægge dem. Lav en databaseegnet backup, opbevar den separat og prøv at gendanne den i et øvemiljø."],
            Code = "docker volume ls\ndocker volume inspect kajstatus_db",
            Sources = [new("Docker · volumes", "https://docs.docker.com/engine/storage/volumes/", true, false), new("Docker · Compose Quickstart", "https://docs.docker.com/compose/gettingstarted/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Secrets og rettigheder", "/images/characters/whale/infographics/12-secrets-og-rettigheder.png", "Havnekaptajnen med et adgangsdiagram, hvor kun API-tjenesten får en secret til databaseforbindelsen.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/12-secrets-og-rettigheder.webp",
            Stage = "Compose",
            Objectives = [7],
            Summary = "Hold credentials ude af images og Git, og giv kun de relevante services adgang til deres secrets.",
            Explanation = ["Placér ikke credentials i en Dockerfile eller et image-lag. I Compose kan et secret hentes fra en fil eller en understøttet miljøkilde og tildeles den service, som skal læse det. Appen skal derefter kunne læse værdien fra det monterede filsted. Føj udsnittet til en Compose-fil, hvor api allerede har et image eller en build-konfiguration.", "Beskyt secret-kilden på værten. Begræns også databasebrugerens rettigheder, og vælg en appbruger med mindst mulig adgang i containeren. Skriv aldrig secret-værdier i logs, og udskift en credential, hvis den er blevet delt utilsigtet."],
            Code = "services:\n  api:\n    secrets: [db_password]\nsecrets:\n  db_password:\n    file: ./local-secrets/db_password",
            Sources = [new("Docker · secrets i Compose", "https://docs.docker.com/compose/how-tos/use-secrets/", true, false), new("Docker · USER-instruktionen", "https://docs.docker.com/reference/dockerfile/#user", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Sæt flåden i søen", "/images/characters/whale/infographics/13-saet-flaaden-i-soeen.png", "Havnekaptajnen ved et diagram med version, kontrol af Compose, start af tjenester og afsluttende test af den offentlige indgang.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/13-saet-flaaden-i-soeen.webp",
            Stage = "Udrulning",
            Objectives = [7],
            Summary = "Brug en tydelig rækkefølge til at validere, hente, starte og kontrollere en Compose-udrulning.",
            Explanation = ["Vælg et image-tag eller digest, der allerede er bygget og testet. Kontrollér den konfiguration, der bliver brugt på værten, og hent imaget. Compose kan genoprette de services, hvis image eller konfiguration er ændret.", "Med --wait venter Compose på, at services er kørende eller healthy, alt efter deres konfiguration. Det dækker ikke automatisk den rute, brugerne åbner. Kontrollér et lille relevant kald via appens normale indgang, og registrér den valgte version og resultatet."],
            Code = "docker compose config --quiet\ndocker compose pull\ndocker compose up -d --wait\ncurl --fail https://app.example.com/health",
            Sources = [new("Docker · Compose config", "https://docs.docker.com/reference/cli/docker/compose/config/", true, false), new("Docker · Compose pull", "https://docs.docker.com/reference/cli/docker/compose/pull/", true, false), new("Docker · Compose up", "https://docs.docker.com/reference/cli/docker/compose/up/", true, false), new("curl · kommandoer og flag", "https://curl.se/docs/manpage.html", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Afgør om appen er klar", "/images/characters/whale/infographics/14-er-appen-klar.png", "Havnekaptajnen ved et diagram med gul opstart, grøn healthcheck og et separat kald via den offentlige rute.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/14-er-appen-klar.webp",
            Stage = "Udrulning",
            Objectives = [3, 7],
            Summary = "Skeln mellem processens tilstand, en intern healthcheck og en ekstern kontrol af tjenesten.",
            Explanation = ["En container kan være running, selv om appen stadig starter eller ikke kan bruge en vigtig afhængighed. En healthcheck kører en kommando i containeren og bruger exitkoden til at vurdere, om den er healthy. Startperiode og intervaller skal passe til appen. Eksemplet forudsætter, at curl findes i imaget, og at appen lytter på port 8080 inde i containeren.", "I Compose kan depends_on med condition: service_healthy vente på en afhængighed under opstart. Det erstatter ikke appens egen håndtering af kortvarige forbindelsesproblemer. Kontrollér også den rute, brugerne faktisk bruger, fra et andet sted end selve containeren."],
            Code = "healthcheck:\n  test: [\"CMD\", \"curl\", \"-fsS\", \"http://localhost:8080/health\"]\n  interval: 10s\n  timeout: 3s\n  retries: 3",
            Sources = [new("Docker · healthcheck i Compose", "https://docs.docker.com/reference/compose-file/services/#healthcheck", true, false), new("Docker · opstartsrækkefølge i Compose", "https://docs.docker.com/compose/how-tos/startup-order/", true, false), new("Docker · Compose up", "https://docs.docker.com/reference/cli/docker/compose/up/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Følg sporene i loggen", "/images/characters/whale/infographics/15-foelg-sporene-i-loggen.png", "Havnekaptajnen med en logbog og en tidslinje fra normal start over timeout til en fejl i en afhængighed.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/15-foelg-sporene-i-loggen.webp",
            Stage = "Udrulning",
            Objectives = [2, 7],
            Summary = "Brug Compose ps og logs til at afgrænse, hvilken service og hvilket tidspunkt du skal undersøge.",
            Explanation = ["docker compose ps viser de kørende containere og deres status. Tilføj --all, hvis du leder efter en service, der allerede er stoppet. Brug docker compose logs med et tidsvindue og et servicenavn til at indsnævre hændelsen.", "Start med den tidligste relevante fejl og tjek, hvad der skete omkring den. En timeout i web kan f.eks. skyldes en afhængighed længere nede. Brug tidsstempler eller korrelations-id’er til at sammenholde spor og undlad at logge persondata og tokens."],
            Code = "docker compose ps --all\ndocker compose logs --since 10m --tail 50 api",
            Sources = [new("Docker · Compose ps", "https://docs.docker.com/reference/cli/docker/compose/ps/", true, false), new("Docker · Compose logs", "https://docs.docker.com/reference/cli/docker/compose/logs/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        },
        new("Kanarien først", "/images/characters/whale/infographics/16-kanarien-foerst.png", "Havnekaptajnen ser Kanarien sejle med en lille del af trafikken; to måleudfald fører til enten udvidelse eller tilbage til den gamle version.", 1536, 1024)
        {
            Preview = "/images/characters/whale/infographics/16-kanarien-foerst.webp",
            Stage = "Udrulning",
            Objectives = [7],
            Summary = "Afprøv en ny version med en lille del af trafikken. Planlæg rollback til den senest fungerende version.",
            Explanation = ["Ved en canary sender en proxy eller udrulningsplatform en lille del af trafikken til den nye version. Resten bliver på den kendte. Sammenlign f.eks. fejlraten og svartiden med den tidligere version i et aftalt tidsvindue. Docker Compose står for containere, mens trafikfordelingen kræver særskilt routing.", "Øg trafikandelen i små trin, hvis signalerne holder sig inden for grænserne. Ved fejl kan du sende trafikken tilbage til den kendte version. Behold det godkendte image og planlæg bagudkompatible databaseændringer, så den tidligere app stadig kan læse de data, der er skrevet."],
            Code = "Først: 5 % ny version, 95 % kendt version\nMål: 5xx-fejl og svartid for begge versioner\nVed fejl: send trafikken tilbage til kendt version",
            Sources = [new("Google SRE Workbook · canary releases", "https://sre.google/workbook/canarying-releases/", true, false), new("Docker · Compose up", "https://docs.docker.com/reference/cli/docker/compose/up/", true, false), new("DevNet · fag 20738", "https://maalpinde.lup.dk/vuf/#deploy-or-die", true, false)]
        }
    ];

    public static IReadOnlyList<CharacterIllustration> Scenes { get; } =
    [
        new("Tidevandsbogen ved kajen", "/images/characters/whale/scenes/tidevandsbogen.png", "Docker holder sin åbne tidevandsbog ved en gammel kaj. Bag ham ligger containerskibet i morgentågen.", 1254, 1254)
        {
            Preview = "/images/characters/whale/scenes/tidevandsbogen.webp"
        },
        new("Sardindåsen bliver en slæbebåd", "/images/characters/whale/scenes/sardindaasens-slaebebaad.png", "Docker arbejder under en messinglampe på en lille fjernstyret slæbebåd med sardindåse som skrog. Kanarien venter på bordet.", 1254, 1254)
        {
            Preview = "/images/characters/whale/scenes/sardindaasens-slaebebaad.webp"
        },
        new("Kanarien på prøvetur", "/images/characters/whale/scenes/kanariens-proevetur.png", "Kaptajnen styrer en lille sardindåsebåd med den gule gummiand Kanarien gennem badekarret, mens to både ligger fortøjet.", 1254, 1254)
        {
            Preview = "/images/characters/whale/scenes/kanariens-proevetur.webp"
        },
        new("Kommandobroen i blå time", "/images/characters/whale/scenes/kommandobroen.png", "Docker holder finnen over et grønt greb på containerskibets kommandobro. På dækket ligger rækker af containere.", 1536, 1024)
        {
            Preview = "/images/characters/whale/scenes/kommandobroen.webp"
        }
    ];

    public static IReadOnlyList<CharacterAnimation> Animations { get; } =
    [
        new("saml-stakken", "Saml stakken", "Docker Compose", "Tre tjenester får hver sin container. Docker Compose starter dem som en samlet applikation og giver dem et fælles netværk, så tjenesterne kan finde hinanden.", "/images/characters/whale/animations/saml-stakken.gif", "/images/characters/whale/animations/saml-stakken.webp", "Docker følger en havnekran, der stiller tre farvede containere for web, API og database på en fælles kaj og forbinder dem.", 512, 512),
        new("kanarien-forst", "Kanarien sejler først", "Canary deployment", "Send først en lille del af trafikken til den nye version. Hold øje med fejl og svartid, før du sender mere trafik over.", "/images/characters/whale/animations/kanarien-forst.gif", "/images/characters/whale/animations/kanarien-forst.webp", "Kanarien tager sin sardindåsebåd gennem badekarret. To andre både sejler først med, efter at lampen på kajen bliver grøn.", 512, 512),
        new("tilbage-til-sikker-kurs", "Tilbage til sikker kurs", "Rollback", "Når en ny version fejler, kan kaptajnen rulle tilbage til den senest fungerende version. Han tjekker igen, at tjenesten svarer som forventet.", "/images/characters/whale/animations/tilbage-til-sikker-kurs.gif", "/images/characters/whale/animations/tilbage-til-sikker-kurs.webp", "En rød lampe lyser over den nye container. Docker bruger et messinggreb til at skifte til den senest fungerende grønne container.", 512, 512)
    ];
}
