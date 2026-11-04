---
type: feature
status: active
tags: [feature, design, blazor, lup]
---

# Blazor-designgennemgang

Gennemgået 4. november 2026 mod den oprindelige reference i design/lup. Udgangspunkt: 246ae1a. Kaspers nye valg: Rainbow udgår; jul og hacker bliver frie grundtemaer sammen med system, lyst og mørkt.

## Designretning

Standardfladen bevarer DM Sans til tekst og Space Grotesk til overskrifter, én kompakt header, foldbare kategorier og faste brødkrummer i forløbet.

- Standard: læringsblå #2452e8, blæk #17243b, papir #ffffff, baggrund #f3f6fb og grøn #216747.
- Jul: grangrøn #081d18, flade #14342d, guld #f2cd83, tekst #f6f4e9 og afdæmpet mint. Sne og lys er dekoration i baggrunden; indhold og statusfarver bevares.
- Hacker: baggrund #07110e, flade #10231c, terminalgrøn #8aefaa, tekst #ddf3e6 og dæmpet grøn #9bb9a8. Monospace bruges til overskrifter og små data; brødtekst bevarer læsbarheden.

Layout: kompakt header → sidetitel → hovedindhold med kontekstafhængig sidekolonne. Forløb har dokument og indholdsfortegnelse; kalender har fem dage og tidsskala; butik og galleri bruger illustrationer. Der tilføjes ikke en ekstra global menurække.

## Fundne afvigelser og rettelser

| Område | Afvigelse i Blazor-porteringen | Rettelse |
| --- | --- | --- |
| Grundlayout | Bootstrap påvirkede designets egne row-elementer og børn | Afgrænset grid-reset, ens spacing, aktiv navigation og mobilregler |
| Forside | Kun medlemsliste og forklaringer om eksempeldata | Forløbsforside med cover, genveje, udvikling og avatar; API-medlemskaber bevares |
| Forløb | H3 var en liste; Game Design kun cover og beskrivelse | Dokumentvisning med udfoldelige uger, dagsider, fag, emner og forløbsnavigation |
| Navigation | Flere indgange endte i lister; genbrugte komponenter kunne vise forrige sides indhold | Selvstændige detaljesider, direkte links og reaktion på navigation |
| Kalender | Fem ens kort uden tidsskala | Ugevisning med timer, aktivitetsblokke og korrekt grøn/blå/gul fremmødeform; mobil bliver dagsliste |
| Badges | Tekstbokse, syv badges, ingen medaljer | 13 badges, fem medaljeniveauer, filtre, ejet/mangler og egne kriteriesider |
| Butik | Tekstliste uden varebilleder | Tøj- og møbelillustrationer, kategorier, level-/badgekrav og visuel oplåsningsplan |
| Avatar | Ingen figur | 2D-figur, udseendevalg, garderobe og genbrug i Rune Run Mini |
| Lejlighed | Små deaktiverede knapper med tekst | 8×6-rum, møbelgrafik, placering/flytning/fjernelse og inventar |
| Projekter | Gentagne tekstpaneler | Billedkort med ejer, deling, reaktioner og kommentarer |
| Søgning | Kun kataloglister; resultater åbnede oversigter | Fag og emner medtages; quests og badges åbner egne sider; private projekter udelades fra fælles indeks |
| Afstemninger | Uens tekst- og knapvisninger | Samlet aktivitetsindgang, egne spørgsmålssider, stemningsvalg og visuelle resultatbjælker hos underviser |
| Temaer | Rainbow, manglende sammenhæng i Bootstrap-flader | System/lys/mørk/jul/hacker; gamle værdier normaliseres til system |

## Juletema

Sne renderes på ét canvas bag indholdet. Mus eller finger påvirker vinden, og snefnug skubbes til side omkring markøren. Der er højst 90 fnug og begrænset pixelratio. Animationen stopper ved skjult fane og ved skift væk fra jul. Ved reduceret bevægelse vises et stillestående snelag. Brugeren kan også pause sneen, og valget huskes.

En lyskæde ligger ved headeren. Gaven åbner en lille julepause, kan lukkes og lukker ved navigation. Den uddeler ikke XP, guld eller varer. Nye SVG-ikoner er almindelige UI-illustrationer, ikke nye brandinglogoer.

## Implementeringsgrænse

Dette arbejde genskaber designet i den eksisterende Blazor-app. Der er ikke indført en ny frontendstack, et iframe med den gamle app eller et ekstra login. Auth, controllers, hold, medlemskaber og Lectio beholdes.

Avatarens udseende og møblernes placering gemmes lokalt med en brugerspecifik nøgle. Det giver ikke servervalideret ejerskab. Badges, saldo, aktivitetsantal, kalenderaktiviteter, afstemningsresultater og indhold i designkataloget er fortsat fixturedata. Nye emnesider er korte indholdsskabeloner; de er ikke en fuld import af Notion eller formelle målpinde.

Resterende leverancer er stadig [[LUP-design-og-leveranceplan]] og issues #11, #12, #24–#34: vedvarende quests/afleveringer, lærerens kalendereditor/træk-og-slip, rigtige afstemninger på tværs af klienter, adgangsfiltreret søgning, automatisk status, økonomi og køb. Disse funktioner markeres ikke færdige af designrettelserne; køb og aflevering er fortsat deaktiveret. Ingen issues lukkes eller sprintstatus ændres her.

Undervisningsplanerne under /lup/index.html bevares som særskilt eksisterende indhold; denne gennemgang vedrører Blazor-appens LUP-flade.

## Kontrol

- Debug-build og Release-build: 0 fejl og 0 advarsler. Release-publish gennemført; SDK’et anbefaler wasm-tools til yderligere optimering. Der er ikke installeret nye workloads.
- 41 repræsentative sider med elevvisning samt syv indgange hos underviser er kontrolleret med isolerede API- og Auth-fixtures.
- 153 interne indholdsadresser gennemgået: ingen døde sider og ingen registrerede JavaScript-/Blazor-fejl. Direkte emnelink og genindlæsning virker.
- Bredder 320, 390, 768, 1024 og 1440 px kontrolleret; ingen vandrette overløb på de udvalgte hovedsider.
- Søgning på MQTT giver direkte emne-, quest- og badge-links. Badgefiltre, lokale avatarvalg og møbelplacering efter reload er afprøvet.
- H3, Game Design og intet aktivt forløb giver korrekt UI-kontekst. Temavalg før login er afprøvet på mobil.
- Jule-sne bevæger sig, reagerer på markøren, stopper ved pause og respekterer reduceret bevægelse. Gave og lys vises kun i juletemaet; kontrollerne dækker ikke footer-links.
- Git diff-kontrol uden whitespace-fejl.

Browserdata er isolerede testfixtures. Der er ikke skrevet til rigtige hold eller elevdata. API-integration med den rigtige server og database er ikke testet i denne designgennemgang.
