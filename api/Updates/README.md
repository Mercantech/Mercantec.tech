# Nyt i LUP

Undervisernes ændringsoversigt ligger på `/teacher/updates`. Indgangen findes under avatarikonet. Katalog og billeder leveres fra `/api/platform-updates` og kræver en autentificeret underviser eller admin. De private billeder er indlejret i API'et.

## Tilføj et opslag

Tilføj `api/Updates/Notes/<stabilt-id>.json` i samme ændring som den brugerrettede funktion. Brug små bogstaver, tal og bindestreger i filnavnet. Skriv én kort dansk note pr. funktion med fokus på, hvad brugeren kan gøre eller opleve. Ret filen med samme id, når ændringen hører til samme opslag. Rent interne rettelser, tests, refaktorering og builds springes over.

```json
{
  "date": "2026-11-09",
  "title": "Find dine noter på emne",
  "description": "Filtrér notesbogen efter emne for at finde de sider, du skal bruge.",
  "authors": ["Mathias"]
}
```

Brug funktionens leveringsdato i formatet `ÅÅÅÅ-MM-DD`. `authors` er valgfri og kan indeholde `Mathias`, `Kasper` eller begge. Et skærmbillede er valgfrit. Gem et optimeret WebP af den faktiske funktion med fiktivt indhold i `Images/`, og tilføj fx `"after": { "id": "noter-efter-emne", "alt": "Notesbogen med filteret Emne åbent." }`. Billedets id er filnavnet uden `.webp`.

Det eksisterende API-build indlejrer opslag og billeder. Dokploy udruller dem sammen med koden ved push til `main`, og visningen sorterer de nyeste først. Den kuraterede historik bevares i `PlatformUpdatesCatalog.cs` og `coverage.json`. Nye opslag i `Notes/` kræver ingen registrering i de to filer.

```powershell
dotnet test Mercantec.Tech.slnx --filter "FullyQualifiedName~PlatformUpdates"
dotnet publish api/Mercantec.Tech.Api.csproj -c Release
```

Kontrollér på `/teacher/updates` som underviser, at opslaget vises én gang med korrekt dato og tekst. Hvis opslaget har et billede, så kontrollér billedet og zoom på desktop og mobil. Adgangskontrollen dækker også elev og gæst.

## Dækning og billedgrundlag

Den kuraterede historik indeholder 62 opslag fra en gennemgang af 240 commits frem til `7241311` fra 22. oktober til 9. november 2026. Den indlejrede, private `coverage.json` dokumenterer koblingen fra commits til opslag og begrundelser for udeladelser. Automatiske test kontrollerer det kuraterede katalog mod gennemgangen.

`image-sources.json` dokumenterer originalernes relative placering i det lokale arbejdsområde, beskæringer og billedstørrelser. De færdige billeder leveres i `Images/`; originalerne er ikke en build-afhængighed. Skærmbilleder kan være nyere end den ændring, de illustrerer. De tidligere fixture-optagelser viser den faktiske app med fiktive konti, dokumenter og API-fixtures med ekstern trafik blokeret. Editorbilleder bruger den faktiske editor med lokal testkontekst og uden AI-kald. Historiske før/efter-par bruges kun, når begge billeder findes.

- `notification-dropdown`, `library`, `hold-navigation`: tidligere skærmbilleder i arbejdsområdets `output/lup-*.png`.
- `characters`, `notebook`, `roulette`: nye optagelser af den faktiske app fra `origin/main` ved `0f706f7`, med isolerede fiktive konti og API-fixtures. Ekstern trafik var blokeret.
- `published-pages`, `poll-studio`, `newyear`, `avatar`, `apartment`: eksisterende billeder fra `design/`, beskåret omkring funktionen.
- `gift-before` / `gift-after`: den tidligere pakkevisning sammenlignet med den personlige åbnescene.
- `student-before` / `student-after`: elevforsiden før og efter det kompakte Lectio-/VUF-panel blev tilføjet.
- `avatar` / `avatar-collection`: den tidligere editor med udseendevalg sammenlignet med den nye avatarsamling, der følger kontoen. Det tidligere billede genbruges også ved det historiske opslag om den første editor.
- De øvrige opslag: fokuserede optagelser i `tmp/platform-updates-completeness/` og eksisterende, reelle kontrolbilleder i arbejdsområdets `design/` og `output/`. Der er særskilte billeder af bl.a. Lectio, elevvisning, quests, afleveringer, kalender, editor, emoji-værksted, avatarsamling og temavælger.

## Ophav

De 62 kuraterede opslag har en byline med de væsentlige implementeringsbidrag til den
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

## Gennemgang den 8. november 2026

Fem nye opslag dækker private AI-forslag i editoren, LUPPs 48 emojis, karakterbibliotekets gallerier, den skjulbare sidemenu og forbindelser mellem notesbøger. Alle fem er udviklet af Mathias. AI-opslaget samler også faglig kildeudvælgelse og korrekt nummerering i forhåndsvisningen; AI-adgang skal fortsat være konfigureret.

De fem billeder er faktiske skærmbilleder: AI-opslaget bruger den isolerede editor-fixture fra `ec330aa`, og sidemenuen bruger den tidligere integrerede browserkontrol med fiktive SQL-sider. Notesbogsoversigten er optaget fra samme release med nye fiktive teori- og holdnotesbøger i hukommelsen. LUPP og karakterbiblioteket er optaget på de offentlige sider på lup.dk. Originaler, beskæringer og kildehashes er registreret i `image-sources.json`; de nye optimerede WebP-filer er indlejret privat i API'et.

Kaspers komplette Raymond-avatarpakke er føjet til det eksisterende opslag om avatarsamlingen: 28 handlinger, 224 animationsbilleder og 33 lydklip, heraf fem danske replikker, samt forhåndsvisning med genstande og effekter. De oprindelige før/efter-billeder er bevaret og viser kontosamlingen, ikke hele den nye pakke. Opslaget om Nyt i LUP krediterer nu også Mathias for bylines og vedligeholdelse; dets historiske billede viser første udgave.

Gennemgangen til `ec330aa` bevarer alle tidligere opslag og commit-koblinger. De ti nyere commits har hver en forklaring og en kobling til et opslag; `coverage.json` dokumenterer konkrete bidrag fra Mathias og Kasper.

## Gennemgang den 9. november 2026

Otte nye opslag dækker LUP Arena, kamæleonen til Machine Learning, sidernes titel-emojis, kladderedigering med Codex, editorens arbejdsflade og AI-panel, Raymonds SQL-illustrationer og forbindelsesgrafen. Arena-opslaget samler åbningen den 8. november og de særskilte kampevner den 9. november.

Billederne viser faktiske funktioner. Editorens kolonner og grafen er optaget med fiktivt indhold, AI-panelet, Codex-historikken og titel-emojis på demosider, og Arena i en offentlig træningskamp. Kamæleonens profil og Raymonds SQL-galleri er fra de offentlige karaktersider.
