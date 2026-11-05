---
type: feature
status: active
tags: [feature, themes, newyear, design]
---

# Temaer og nytår

Dette er det fælles overblik over platformens udseender. Nye temaer registreres i `web/Theming/ThemeCatalog.cs`; både profilmenuen og udseendemenuen før login bruger kataloget. Temaer er gratis og kræver hverken level, badges eller guld.

| ID | Navn | Udseende og funktion |
| --- | --- | --- |
| `system` | System | Følger enhedens lyse eller mørke farveskema. |
| `light` | Lyst | Platformens lyse grundtema. |
| `dark` | Mørkt | Platformens mørke grundtema. |
| `christmas` | Jul | Julelys, gaver og interaktiv sne med pause og bevægelsesvalg. |
| `newyear` | Nytår | Mørk nytårshimmel og et frivilligt fyrværkerispil med den lilla LUP-figur og faldende mål. |
| `hacker` | Hacker | Grøn terminalpalet og monospace-overskrifter. |

Rainbow er udgået. Ukendte eller tidligere temaværdier falder tilbage til Lyst. Temavalget gemmes i browseren og giver ikke adgang til indhold.

## Nytårstema

Tilføjet 5. november 2026 efter ønsket om et Space Invaders-inspireret nytårstema. Brugeren vælger Nytår under Udseende og starter derefter fyrværkerispillet med en særskilt knap. Under spillet kan raketter ramme og midlertidigt skjule synlige dele af interfacet. Point og fyrværkeri giver feedback på træffere.

Vælg **Udseende → Nytår**, og tryk **Start fyrværkeri**. Den lilla lerfigur fra brugerens referencebillede holder et fyrværkerirør og skyder op fra røret. Flyt med musen, piletasterne eller A/D, og skyd med klik eller ved at holde mellemrum nede. På touchskærme kan man holde retningsknapper og Skyd nede samtidig. **Start forfra** nulstiller spillet.

Spillet har ingen tidsgrænse og fortsætter, selv når sidens oprindelige mål er ramt. Balloner, stjerner, gaver, discokugler, festhatte og små vinduer falder ned foran interfacet. Niveauet stiger løbende med flere og hurtigere mål, op til en øvre grænse for fart og antal, så spillet fortsat kan spilles på mobil. Nogle mål kræver flere træffere og giver flere point. Et mål, der slipper forbi, afslutter ikke spillet.

Spillet afsluttes med **Afslut og gendan** eller Escape. Ved afslutning, navigation, temaskift eller ændret vinduesstørrelse gendannes interfacet. Baggrundssiden holdes stille under spillet, og spillet pauser, når fanen ikke er aktiv. Reduceret bevægelse slår fyrværkeripartikler fra. Spillet ændrer ikke platformens data, køb, afleveringer eller gemt indhold. Det starter kun efter brugerens egen handling.

Nytår og Jul ændrer ikke footerens padding eller højde. De faste temaknapper flyttes op over footeren, når den er synlig; fælles placering styres af `--lup-footer-clearance` i `lup-ui.js`.

Spilfiguren er `web/wwwroot/images/newyear/lup-fireworks-player.png`; den komplette billedprompt og built-in imagegen-mode er gemt i `design/newyear/player-prompt.txt`.

Kontrollerne ligger i `web/Components/NewYearControls.razor`, paletten i `web/wwwroot/css/lup-newyear.css` og spillet i `web/wwwroot/js/lup-newyear.js`. Regressioner køres med `node --test tests/lup-ui.test.cjs tests/lup-newyear.test.cjs`.

## Når et tema tilføjes

1. Registrér ID, navn, ikon og beskrivelse i `ThemeCatalog`.
2. Tilføj ID til `lup-ui.js` og opstartsscriptet i `index.html`, og tilføj temaets CSS-variabler.
3. Kobl eventuelle temakontroller til livscyklussen, så effekter stopper og rydder op ved navigation, afslutning og temaskift.
4. Opdatér dette overblik og kontrollér temavalg før og efter login, mobilbredde og reduceret bevægelse.

## Relateret

- [[LUP-navigation-og-soegning]]
- [[Julegaver-og-sne]]
- [[Blazor-designgennemgang]]
