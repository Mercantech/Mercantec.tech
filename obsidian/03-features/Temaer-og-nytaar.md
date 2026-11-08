---
type: feature
status: active
tags: [feature, themes, newyear, design]
---

# Temaer og nytår

Dette er det fælles overblik over platformens udseender. Nye temaer registreres i `web/Theming/ThemeCatalog.cs`; knappen **Tema** i topbaren bruger kataloget både før og efter login. Temaer er gratis og kræver hverken level, badges eller guld.

| ID | Navn | Udseende og funktion |
| --- | --- | --- |
| `system` | System | Følger enhedens lyse eller mørke farveskema. |
| `light` | Lyst | Platformens lyse grundtema. |
| `dark` | Mørkt | Platformens mørke grundtema. |
| `christmas` | Jul | Julelys, gaver og interaktiv sne med pause og bevægelsesvalg. |
| `newyear` | Nytår | Nytår med midnatsblå, champagneguld, fyrværkerimotiver og et frivilligt programmeringsspil med boosts. |
| `hacker` | Hacker | Grøn terminalpalet og monospace-overskrifter. |

Rainbow er udgået. Ukendte eller tidligere temaværdier falder tilbage til Lyst. Temavalget gemmes i browseren og giver ikke adgang til indhold.

## Temavælger i topbaren

Knappen **Tema** er synlig for gæster og indloggede brugere. Menuen viser alle seks valg med beskrivelse, tydeligt flueben og markeret valgt tilstand. Valget gemmes i browseren. Menuen kan lukkes med Escape eller et klik udenfor og tilpasser sig mobilbredden. `ThemePicker.razor` deler visning og tilgængelighedsmarkering mellem de to topbarer.

## Nytårstema

Tilføjet 5. november 2026 efter ønsket om et Space Invaders-inspireret nytårstema. Brugeren vælger Nytår under Tema og starter derefter fyrværkerispillet med en særskilt knap. Under spillet kan raketter ramme og midlertidigt skjule synlige dele af interfacet. Point og fyrværkeri giver feedback på træffere.

Vælg **Tema → Nytår**, og tryk **Start fyrværkeri**. Den lilla lerfigur fra brugerens referencebillede holder et fyrværkerirør og skyder op fra røret. Flyt med musen, piletasterne eller A/D, og skyd med klik eller ved at holde mellemrum nede. På touchskærme kan man holde retningsknapper og Skyd nede samtidig. **Start forfra** nulstiller spillet.

Spillet har ingen tidsgrænse og fortsætter, selv når sidens oprindelige mål er ramt. Programmeringsfejl falder ned foran interfacet: bugs, syntaxfejl, null-fejl, mergekonflikter, fejlede tests og større build-fejl. Røde tests bliver til PASS, og build-fejl afsluttes med Build succeeded. Niveauet stiger løbende med flere og hurtigere mål, op til en øvre grænse for fart og antal, så spillet fortsat kan spilles på mobil. Nogle mål kræver flere træffere og giver flere point. Et mål, der slipper forbi, afslutter ikke spillet.

Ved træffere springer fyrværkeriet som buketter med stråler, glitrende spor, faldende gnister og farver, der langsomt slukkes. Større mål giver større buketter. Reduceret bevægelse viser en kort, rolig radial markering og tekstfeedback.

### Boosts

Lysende boost-kapsler kan fanges med figuren eller skydes. Den første kommer efter det første nedskudte mål; derefter kommer de regelmæssigt, og de fem typer roterer, så de alle kan opnås. Boosts virker i 12 sekunders aktiv spilletid. Samme boost fornyer varigheden, og forskellige typer kan kombineres. Aktive effekter vises med en bjælke i spillets header; spillet har fortsat ingen tidsgrænse.

| Boost | Effekt |
| --- | --- |
| Multiskud | Tre skud i en vifte. |
| Megaskud | Større skud med ekstra skade. |
| Hurtig ild | Kortere pause mellem skuddene. |
| Kometskud | Skud med spor, der går gennem flere mål. |
| Turbo | Hurtigere figur og hurtigere skud. |

Spillet afsluttes med **Afslut og gendan** eller Escape. Ved afslutning, navigation, temaskift eller ændret vinduesstørrelse gendannes interfacet. Baggrundssiden holdes stille under spillet, og spillet pauser, når fanen ikke er aktiv. Reduceret bevægelse begrænser bevægelige gnister og bevarer rolig træfferfeedback. Spillet ændrer ikke platformens data, køb, afleveringer eller gemt indhold. Det starter kun efter brugerens egen handling.

Nytår og Jul ændrer ikke footerens padding eller højde. De faste temaknapper flyttes op over footeren, når den er synlig; fælles placering styres af `--lup-footer-clearance` i `lup-ui.js`.

Spilfiguren er `web/wwwroot/images/newyear/lup-fireworks-player.png`; den komplette billedprompt og built-in imagegen-mode er gemt i `design/newyear/player-prompt.txt`.

Kontrollerne ligger i `web/Components/NewYearControls.razor`, spillets UI i `web/wwwroot/css/lup-newyear.css`, nytårsudseendet på hele platformen i `web/wwwroot/css/lup-newyear-theme.css` og spillet i `web/wwwroot/js/lup-newyear.js`. Regressioner køres med `node --test tests/lup-ui.test.cjs tests/lup-newyear.test.cjs`.

## Når et tema tilføjes

1. Registrér ID, navn, ikon og beskrivelse i `ThemeCatalog`.
2. Tilføj ID til `lup-ui.js` og opstartsscriptet i `index.html`, og tilføj temaets CSS-variabler.
3. Kobl eventuelle temakontroller til livscyklussen, så effekter stopper og rydder op ved navigation, afslutning og temaskift.
4. Opdatér dette overblik og kontrollér temavalg før og efter login, mobilbredde og reduceret bevægelse.

## Relateret

- [[LUP-navigation-og-soegning]]
- [[Julegaver-og-sne]]
- [[Blazor-designgennemgang]]
