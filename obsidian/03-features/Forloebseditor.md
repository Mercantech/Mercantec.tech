# Forløbseditor – selvhostet testversion

Testområdet er `/notion`. Det bruger eksisterende Mercantec-login og er kun åbent for undervisere, der kan administrere det konkrete hold. Det vises i underviserens menu under Læring → Forløbseditor · Test.

## Første version

- Dokumenter og undersider pr. eksisterende hold. Opret sider og redigér titler i venstre dokumentnavigation.
- BlockNote med rich text, standardblokke, Markdown-genveje, slash-menu, simple tabeller og drag & drop.
- `/elever`: holdets aktuelle elevliste. Relationer bruger MembershipId, også for Lectio-elever uden brugerkonto.
- `/afstemning`: vælg en eksisterende afstemning fra samme hold. Blokken henviser til den eksisterende afstemningsside.
- `/slide`: slidegrænse i samme dokument. Præsenter bruger en separat læsevisning og udelader elevlister.
- `/læringsmål`: en redigerbar blok til aktivitetens mål.
- Samtidig redigering med Yjs og Hocuspocus. Dokumenttilstanden gemmes binært i Postgres.
- Gemmestatus skelner mellem forbindelse, ændringer der afventer lagring og bekræftet lagring. Hold dokumentet åbent ved forbindelsesfejl.

Piloten omfatter ikke elevredigering, udgivelse til elever, generiske relationelle databasetabeller, versionshistorik, kolonner eller dokumentvedhæftninger. Standardblokke til medier kan indsættes med URL; det eksisterende filsystems autorisation skal udvides, før det kobles til dokumentupload.

## Arkitektur

`web/Pages/Notion` er Blazor-værten. `web/Notion/NotionClient.cs` taler med API'et. Editorens React/TypeScript-kode ligger i `editor/` og indlæses kun på dokumentruten. Blazor ejer editorens ydre container, mens React/BlockNote ejer indholdet.

`api/Controllers/NotionController.cs` og `shared/Notion.cs` definerer holdbaserede dokumenter, kontekst og adgang. Brugeren skal have `CourseAccess.CanManageAsync` for hver dokumentoperation og samarbejdsforbindelse. Der anvendes ingen adgangsgivende demo-token i produktion.

`collaboration/` er en separat Node 22-service. Den validerer brugerens token og dokumentadgang gennem API'et. Browseren forbindes til samme origins `/collaboration`; nginx håndterer WebSocket-proxyen. Serveren henter og gemmer Yjs-data gennem interne API-endpoints med en særskilt servicehemmelighed. Det interne endpoint er blokeret i web-nginx.

`NotionPage.YjsState` er dokumentets autoritative indhold. Metadata og holdrelationer ligger i Postgres. Elevnavne og afstemninger hentes fra holdet; private besvarelser indgår ikke i det delte Yjs-dokument. Der bruges ingen localStorage/IndexedDB til dokumenter.

Kør kun én samarbejdsinstans i piloten. Flere instanser kræver fælles koordination og lagerstrategi, før de aktiveres.

## Lokal udvikling

Krav: .NET 10, Node 22 og en eksisterende Postgres-konfiguration. Fra repoets rod:

```powershell
npm --prefix editor ci
npm --prefix editor run build
npm --prefix collaboration ci
dotnet build web
dotnet test tests/Mercantec.Tech.Api.Tests
npm --prefix collaboration test
```

Docker Compose opretter automatisk en tilfældig servicehemmelighed i volumen `mercantec_notion_key`. Kun init-containeren skriver filen; API og samarbejdsserver monterer den skrivebeskyttet. Der findes ingen produktionsstandardnøgle. `NOTION_SERVICE_SECRET` kan bruges som et eksplicit override med mindst 32 tilfældige tegn i en git-ignoreret `.env`.

```powershell
docker compose -f docker-compose.yml -f docker-compose.local.yml up --build
```

Åbn `http://localhost:3000/notion`, og brug det normale login. API'et skal have rigtige hold og scoped underviseradgang. Docker bygger editoren automatisk med den committede lockfil. Lokalt genbygges editorbundle efter ændringer i `editor/src/`.

Ved kørsel uden Compose sættes `Notion__ServiceSecret` på API'et og `NOTION_SERVICE_SECRET`, `API_BASE_URL`, `ALLOWED_ORIGINS` og `PORT` på samarbejdsserveren. Browserens origin skal gå gennem en reverse proxy, der sender `/api/` til API'et og `/collaboration` til samarbejdsserveren; Blazors egen devserver laver ikke denne WebSocket-proxy automatisk.

## Deployment på mercantec.tech

1. Brug repoets opdaterede Compose-konfiguration. Den opretter automatisk en tilfældig servicehemmelighed i en vedvarende volumen. Et eventuelt eksisterende `NOTION_SERVICE_SECRET` skal være mindst 32 tegn; lad variablen være tom for automatisk opsætning.
2. Deploy den opdaterede kildekode med repoets eksisterende Compose-konfiguration. API'et kører EF-migrationen ved opstart.
3. Kontrollér, at `collaboration` kører og er healthy. Den bruger aliaset `mercantec-tech-collaboration` på det eksisterende netværk.
4. Åbn `https://mercantec.tech/notion`, vælg et hold og opret en side.
5. Åbn samme dokument i en anden undervisersession med adgang til holdet. Skriv fra begge sessioner, kontrollér gemmestatus og genindlæs.
6. Afprøv `/elever`, `/afstemning`, `/slide` og Præsenter. Kontrollér, at andre holds dokumenter ikke kan åbnes.

Følg den eksisterende [Dokploy-drift](../02-ai-workflows/Dokploy-drift.md) for netværk, domæne og deployment-adgang. API'et og samarbejdsserveren skal benytte HTTPS via webproxyen i produktion. Sikkerhedskopiér Postgres sammen med platformens øvrige data; dokumentlagring alene er ikke en versionshistorik.

## Videreudvikling og licenser

BlockNotes kerne er MPL-2.0; Yjs og Hocuspocus er MIT. Der anvendes ingen BlockNote XL-pakker eller leverandørhostet samarbejdstjeneste. Bevar tredjepartslicenser og kildekodehenvisning for den leverede browserkode. Nye bloktyper registreres i editorens schema og slash-menu og skal testes med samtidige klienter og lagring/genindlæsning.

Ved ændringer af blokformatet skal eksisterende dokumenter migreres, og kompatibiliteten med allerede åbne browserfaner vurderes. Nye elevdata og handlinger skal autoriseres på serveren; skjult brugerflade er ikke en adgangskontrol.
