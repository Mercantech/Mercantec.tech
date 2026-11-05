# Forløbseditor: kladde, udgivelse, grupper og medier

Åbn `/notion` med Mercantec-login. Undervisere med adgang til at administrere holdet kan redigere; holdets elever kan læse de udgivne sider. Navigationen findes under Læring, og forløbets holdside linker til indhold og grupper.

## Udgivelse og læsning

1. Vælg et hold, opret en side eller underside, og skriv i kladden.
2. Vent på **Gemt i skyen**. Gem også en ændret titel.
3. Vælg **Udgiv til holdet**. Ved senere ændringer bruges **Opdatér udgivelsen**.
4. **Se udgivet** viser samme faste dokumentversion som eleverne. Kladden kan fortsætte sideløbende.
5. **Træk udgivelse tilbage** lukker elevadgangen til siden og dens vedhæftninger. Undersider udgives og trækkes tilbage hver for sig.

Elever bruger samme sidelinks og ser kun den udgivne titel og det udgivne indhold. Læsevisningen forbinder ikke til kladdens WebSocket-rum. Nye udgivelser hentes med **Opdatér siden** eller genindlæsning.

Udgivelse kopierer serverens gemte Yjs-tilstand og titel. API'et kontrollerer den præcise revision og et fingerprint, så usavede eller samtidige ændringer ikke udgives ved en fejl. Snapshot og filrettigheder opdateres atomisk. Slettet undo-indhold GC-renses af serveren; private metadata skal fortsat opbevares uden for det delte dokument.

## Links og kommandoer

- `@`: søg efter en side, underside eller elev fra holdet.
- `/side`: indsæt et link til en side. Et elevlink åbner en minimal elevside med navn og relevante grupper.
- `/elever`: indsæt holdets elevliste, vælg deltagere og opret en gemt gruppe.
- `/gruppe`: vis en gemt gruppe i dokumentet.
- `/afstemning`: vælg en eksisterende afstemning fra holdet.
- `/video`, `/lyd`, `/billede`, `/fil`: upload medier og vedhæftninger.
- `/slide`: indsæt en slidegrænse; **Præsenter** viser samme indhold som slides.
- `/læringsmål`: fremhæv aktivitetens mål.
- `/diagram` (eller `/mermaid`): indsæt et Mermaid-diagram (flowchart, sequence m.m.). Diagrammet vises uden ramme; dobbeltklik for at redigere koden.
- `/callout`: indsæt en fremhævet note (info, tip, advarsel eller vigtigt).
- Blokmenu (⋮⋮ til venstre for blokken): **Omdan til** (overskrift, citat, liste, kode, callout, læringsmål) og **Slet**. Markér tekst for at ændre bloktype i værktøjslinjen.

Sidelinks og elevrelationer bruger stabile ID'er. Elevfelter bruger MembershipId, også når en Lectio-elev endnu ikke har login. Links giver ikke i sig selv ekstra adgang. Grupper og andre tilknyttede holddata vises fra den aktuelle, autoriserede holdkontekst; selve dokumentteksten fastholdes i udgivelsen.

## Holdgrupper

Grupper gemmes på holdet i learning_groups og learning_group_members. De kan oprettes og redigeres under **Forløb og hold → holdets elever og grupper** samt fra valgte elever i editoren. Markér elever, giv gruppen et navn og vælg **Gem gruppe**. En elev kan være med i flere grupper.

Undervisere administrerer egne hold. Elever kan se deres egne grupper; elevsider deler kun fælles gruppedata. Alle ændringer kontrolleres på serveren, og en versionskonflikt kræver opdatering før et nyt gemmeforsøg.

## Video og filer

Uploadgrænsen er 100 MiB pr. fil. MP4 og WebM kan afspilles i siden; lyd, rasterbilleder, PDF og udvalgte dokumenttyper understøttes også. Andre dokumentformater vises som download. Der er ingen automatisk videokonvertering i denne version.

Filer ligger privat i den eksisterende S3-lagring. Dokumentet gemmer kun fil- og side-ID'er. En elev får først adgang til en vedhæftning, når den indgår i sidens udgivelse. Filen skal uploades til den side, hvor den skal udgives; tværgående genbrug kan tilføjes senere.

Video streames med byte ranges, så afspilning kan søge uden at hente hele filen. Korte mediebilletter fornyes automatisk. Hver forespørgsel kontrollerer stadig medlemskab, udgivelse og filreference. Tilbagetrækning og ny udgivelse ugyldiggør gamle elevbilletter. Den ældre /api/files-rute afviser det private dokumentområde.

## Arkitektur og udvikling

- web/Pages/Notion og web/Notion/NotionClient.cs: Blazor-vært, læsning og udgivelseshandlinger.
- web/Components/HoldGroups.razor: fælles elev- og gruppeværktøj.
- editor/: BlockNote/React/TypeScript, egne blokke, henvisninger og medievisning.
- collaboration/: selvhostet Hocuspocus/Yjs; binær dokumentlagring gennem interne API-endpoints.
- api/Controllers/Notion*: scoped dokument-, gruppe- og medie-API.
- shared/Notion*.cs: fælles kontrakter.

Krav: .NET 10, Node 22 og den eksisterende Postgres/S3-konfiguration.

~~~powershell
npm --prefix editor ci
npm --prefix editor test
npm --prefix editor run build
npm --prefix collaboration ci
npm --prefix collaboration test
dotnet test tests/Mercantec.Tech.Api.Tests
dotnet test tests/Mercantec.Tech.Web.Tests
dotnet build web
~~~

Docker bygger editoren automatisk med lockfilen. Lokalt skal editorbundle genbygges efter ændringer i editor/src. Der bruges ingen localStorage eller IndexedDB til dokumentlagring. En loopback-only QA-fixture findes i editor/tests; den er ikke en produktions-loginvej.

## Selvhosting og opgradering

Deploy repoets Compose-fil fra main som normalt. EF-migrationen køres ved API-start. Notion-init opretter automatisk en tilfældig servicehemmelighed i den vedvarende mercantec_notion_key-volumen. API og samarbejdsserver monterer den skrivebeskyttet. NOTION_SERVICE_SECRET kan bruges som eksplicit override, mindst 32 tilfældige tegn.

Web-værten venter på en sund samarbejdsserver. Nye editorer bruger schemaVersion=4 i WebSocket-forbindelsen; ældre faner bliver bedt om at genindlæse, så de ikke redigerer ukendte bloktyper (fx Mermaid eller callout). Kør én samarbejdsinstans ad gangen. Flere samtidige instanser kræver fælles koordination af dokumenttilstanden.

Sikkerhedskopiér Postgres og S3 sammen. Udgivne snapshots er en fast læseversion, ikke en komplet versionshistorik. Følg [Dokploy-drift](../02-ai-workflows/Dokploy-drift.md) for netværk, domæner og drift.

BlockNote-kernen er MPL-2.0; Yjs og Hocuspocus er MIT. Der bruges ingen XL-pakker eller leverandørhostet samarbejdstjeneste. Licenser og kildehenvisninger leveres med browserbundle.
