---
type: feature
status: implemented
tags:
  - feature
  - logbog
---

# Min notesbog og logbog

Eleven har én notesbog på tværs af forløb, også mellem hold. Åbn **Min notesbog** i hovednavigationen (`/notebook`). `/journal` åbner logbogsvisningen. Mercantec Auths `sub` er ejernøglen; Lectio-navne og holdmedlemskab giver ikke ejerskab.

## Skriv og forbind

**Ny note** opretter en side i roden. Brug højreklik eller **Sidehandlinger** for at oprette en underside, flytte en side eller slette den og dens undersider. Sider kan også flyttes med træk og slip. **Papirkurv** kan gendanne siden med dens links, kommentarer og delinger i op til 30 dage. Under **Sidens oplysninger** gemmes titel og tags. Brug editorens `@`/sidevælger til stabile ID-links.

Den fælles BlockNote-editor understøtter tekst, tabeller, kode, callouts, Mermaid og medier. Notesbogen viser skriveværktøjer uden sidebyggerens elevliste-, gruppe-, afstemnings-, præsentations- og AI-handlinger. Notesbogsfiler kan være op til 100 MiB; sidebyggerens eksisterende multipart-upload er uændret.

Søgning finder titel og senest gemte noteindhold; filtrér på tags og note/logbog. Backlinks og graf bygger på aktive links, ikke slettet undo-historik. Grafen viser op til 400 tilgængelige sider og angiver, hvis flere findes. Sidelisten kan hente flere resultater.

Indhold gemmes automatisk gennem den eksisterende Yjs/Hocuspocus-proces. **Gemt i skyen** kræver en varig gemmekvittering. Ved forbindelsesbrud pauser redigering, og afventende ændringer bliver i den åbne fane. Navigationsværnet beskytter ugemte ændringer og uploads. Der er ingen vedvarende offline-redigering i denne version.

## Fælles logbogsskabeloner

Undervisere åbner **Logbogsskabeloner** (`/teacher/notebook-templates`). Biblioteket er fælles, og alle aktive skabeloner er tilgængelige for alle elever.

1. Opret en skabelon med titel og rytme: daglig, ugentlig eller fri.
2. Skriv startindholdet med blokeditoren, og gem ændrede oplysninger.
3. Vent på lagring, og vælg **Aktivér skabelon** eller **Aktivér ny version**.
4. **Arkivér** stopper nye indlæg fra skabelonen; eksisterende elevkopier bevares.

Aktivering kontrollerer revision og fingerprint og kopierer kun en færdiggemt kladde til en uforanderlig version. Elevbiblioteket viser den aktive versions titel og rytme; nye lærerkladder vises ikke.

**Skriv logbog** lader eleven vælge skabelon, dato og valgfrit hold. Daglige og ugentlige valg åbner det eksisterende indlæg for perioden. Ligger indlægget i papirkurven, kan det gendannes derfra; når fristen udløber, kan eleven skrive et nyt for samme periode. Uger starter mandag; perioder beregnes på serveren i `Europe/Copenhagen`. Frie skabeloner opretter nye indlæg. Genforsøg er idempotente, og databaseindeks beskytter mod dubletter fra samtidige faner.

Eleven får en frit redigerbar kopi med nye blok-ID'er og egne filbevillinger. Skabelonændringer påvirker aldrig eksisterende indlæg. Biblioteket starter med **Dagens refleksion** og spørgsmål om arbejde, læring og næste skridt.

## Adgang, deling og kommentarer

- Ejeren skriver i sine sider og administrerer ekstra deling.
- **Alle undervisere kan læse alle elevnotesbøger og kommentere**, uanset hold. Rollen afgøres af de eksisterende validerede JWT-roller, ikke person-ID'er eller UI-rolleskift. Undervisere retter ikke i elevens tekst.
- Eleven kan dele præcis én side eller hele notesbogen med navngivne registrerede LUP-brugere. Deling giver kun læseadgang. En hel notesbog omfatter også fremtidige sider; én side omfatter ikke undersider eller linkmål.
- Kommentarsporet er kun til eleven og underviserne. Det ligger uden for Yjs-indholdet og er utilgængeligt for delingsmodtagere og den delte notesøgning.

Titler, tags, breadcrumbs, søgeresultater, links, backlinks, graf og medier filtreres på serveren efter aktuel adgang. Links giver aldrig adgang. Filbilletter genkontrollerer deling og aktiv filreference ved hver download/range-request. Den ældre filrute afviser `notebook-private/`.

Læsere får senest servergemte indhold uden udgivelse. Adgang og revision kontrolleres hvert 15. sekund og ved fokus/tilbagevenden til fanen. Uændrede dokumenter sender ikke de binære bytes igen. Opdateringer anvendes på samme læsedokument; tilbagekaldelse lukker visningen og fjerner utilgængelige metadata.

**Elevnotesbøger** (`/teacher/notebooks`) viser elevnavn, hold, seneste logbog og seneste ændring med søgning og holdfilter. Lærerblikket bruger lærerens egen adgang, ikke “Se som elev”. Deling bruger lokale profiler fra validerede loginclaims, uden Auth-adminadgang eller e-mail som nøgle.

## Arkitektur og drift

Det særskilte notesbogsdomæne bruger .NET 10, Blazor, PostgreSQL og privat S3-lagring. `NotebookAccess` er fælles adgangspolitik. `/api/notebooks` håndterer sider, skabeloner, logbog, delinger, kommentarer, søgning og oversigter.

Samarbejdsprocessen håndterer også `notebook:<documentId>`. Serveren bestemmer dokumenttypen: ejeren skriver notesider, undervisere skriver skabelonkladder. Interne API-ruter og kloningsendpoint bruger den eksisterende servicehemmelighed; nginx udleverer ingen interne API-ruter. Dokumentstate, søgetekst, aktive links og filreferencer gemmes i samme transaktion. PostgreSQL bruger et GIN-fuldtekstindeks med `simple`-ordbog til dansk, kode og fagnavne.

Den additive migration køres ved API-start og indlæser kendte profiler fra eksisterende kontakter og koblede medlemskaber. Første opstart opretter standardskabelonen fra et medfølgende, valideret Yjs-snapshot. Der tilføjes ingen tjenester, net eller secrets. Compose angiver `Notebook__CollaborationBaseUrl`; ved manuel hostudvikling sættes den til samarbejdsserverens lokale origin.

Sikkerhedskopiér PostgreSQL og S3 samlet. Skabelonmedier kan dele et uforanderligt storage-objekt gennem forskellige sideejede asset-ID'er; oprydning må ikke slette et objekt, der fortsat refereres fra en elevkopi eller skabelonversion.

## Verifikation

```powershell
npm --prefix editor test
npm --prefix collaboration test
npm --prefix editor run build
dotnet test tests/Mercantec.Tech.Api.Tests
dotnet test tests/Mercantec.Tech.Web.Tests
dotnet build web
dotnet ef migrations has-pending-model-changes --project api
```

Fuld browserfixture med rigtige Blazor-komponenter, API-controllere og samarbejdsserver, men fiktive personer og en isoleret SQLite-database:

```powershell
dotnet publish web -c Release -o .tools/notebook-qa-web
node editor/tests/qa-notebook-harness.mjs
```

Åbn `http://localhost:9098/_qa` i separate elev-/lærerfaner. Fixturen lytter kun på loopback, kræver eksplicit QA-start og er ikke med i produktionsimages. Stop med Ctrl+C.

XP, streaks, quest-aflevering, Markdown-import/eksport, offline-redigering og nye AI-funktioner kommer senere.

## Relateret

- [[Forloebseditor]]
- [[Feature-oversigt]]
- [[Domæne-model]]
