# Nyt i LUP

Undervisernes billedbaserede ændringsoversigt ligger på `/teacher/updates`. Indgangen findes under avatarikonet. Katalog og billeder leveres fra `/api/platform-updates` og kræver en autentificeret underviser/admin. De private billeder er indlejrede ressourcer i API'et og ligger ikke i web-appens offentlige filer.

## Tilføj en ændring

1. Tag et skærmbillede af den faktiske funktion med testdata, og beskær det omkring ændringen. Gem et optimeret WebP-billede i `Images/`. Brug gerne 1.000-1.300 px i bredden, når originalen har opløsning til det.
2. Tilføj en post i `PlatformUpdatesCatalog.cs` med stabilt ID, dato, kort titel og 1-2 sætninger. `After` er obligatorisk; `Before` tilføjes kun, når der findes et ægte tidligere billede af samme funktion. Billed-ID er filnavnet uden `.webp`.
3. Skriv en præcis alternativ tekst. Hold testpersoner og rigtige elevdata adskilt; billederne må ikke indeholde faktiske private elevoplysninger. Angiv `Authors` som `Mathias`, `Kasper` eller begge, så ophavet vises ved opslagets dato.
4. Registrér de tilhørende commits i `coverage.json`. Knyt synlige ændringer til et eller flere opslag; begrund interne ændringer og dublerende merges, som ikke giver et selvstændigt opslag. Flyt baseline frem, når hele historikken frem til den er gennemgået. Datoen er funktionens leveringsdato, ikke datoen for et nyere skærmbillede.
5. Byg og opdatér både API og web. Nye billeder kommer automatisk med som indlejrede ressourcer. Datoerne sorteres nyeste først i visningen. Kør `PlatformUpdates`-testene og kontrollér billeder, zoom, mobilvisning og adgang som underviser, elev og gæst.

## Dækning og billedgrundlag

Oversigten indeholder 49 opslag efter en gennemgang af hele historikken fra 22. oktober til 8. november 2026: 205 commits frem til `d1fe6ea`. Beslægtede ændringer er samlet, så interne rettelser og gentagne merges ikke fylder med dubletter. Den indlejrede, private `coverage.json` dokumenterer koblingen fra commits til opslag og begrundelser for udeladelser. Automatiske test kontrollerer katalogets og gennemgangens indbyrdes konsistens.

`image-sources.json` dokumenterer originalernes relative placering i det lokale arbejdsområde, beskæringer og billedstørrelser. De færdige billeder leveres i `Images/`; originalerne er ikke en build-afhængighed. Skærmbilleder kan være nyere end den ændring, de illustrerer. De nye optagelser viser den faktiske app med fiktive konti, dokumenter og API-fixtures; ekstern trafik var blokeret. Editorbilleder bruger den faktiske udgivne editor med lokal testkontekst og uden AI-kald. Historiske før/efter-par bruges kun, når begge billeder findes.

- `notification-dropdown`, `library`, `hold-navigation`: tidligere skærmbilleder i arbejdsområdets `output/lup-*.png`.
- `characters`, `notebook`, `roulette`: nye optagelser af den faktiske app fra `origin/main` ved `0f706f7`, med isolerede fiktive konti og API-fixtures. Ekstern trafik var blokeret.
- `published-pages`, `poll-studio`, `newyear`, `avatar`, `apartment`: eksisterende billeder fra `design/`, beskåret omkring funktionen.
- `gift-before` / `gift-after`: den tidligere pakkevisning sammenlignet med den personlige åbnescene.
- `student-before` / `student-after`: elevforsiden før og efter det kompakte Lectio-/VUF-panel blev tilføjet.
- `avatar` / `avatar-collection`: den tidligere editor med udseendevalg sammenlignet med den nye avatarsamling, der følger kontoen. Det tidligere billede genbruges også ved det historiske opslag om den første editor.
- De øvrige opslag: fokuserede optagelser i `tmp/platform-updates-completeness/` og eksisterende, reelle kontrolbilleder i arbejdsområdets `design/` og `output/`. Der er særskilte billeder af bl.a. Lectio, elevvisning, quests, afleveringer, kalender, editor, emoji-værksted, avatarsamling og temavælger.

Oversigten vedligeholdes som en del af koden. Den publicerer ikke automatisk nye poster ved hver ændring.

## Ophav

Alle 49 opslag har en byline med de væsentlige implementeringsbidrag til den
beskrevne feature. `MAGS`/`MAGS-GH` og dokumenterede menneskelige Mathias-trailere
henføres til **Mathias**; `KasperHBC` henføres til **Kasper**. En AI-forfatter
henføres kun til et menneske, når Git-historikken dokumenterer det. Små
promptrettelser og rene merge- eller buildændringer giver ikke automatisk fælles
kreditering.

`coverage.json` indeholder de gennemgåede navne i `authors` og konkrete
commit-referencer pr. person i `authorEvidence`. API-testen kontrollerer, at
katalogets navne og dokumentationen stemmer overens. Den samlede historik
bevares fra seneste GitHub-katalog; manglende opslag tilføjes som features,
uden at samme funktion gentages som et nyt kort for hver rettelse.

Opslaget om hurtigere editorindlæsning genbruger det eksisterende editorbillede.
Ændringen beskrives i teksten; der fremstilles ikke et visuelt før/efter-par
eller en måling af indlæsningstid.
