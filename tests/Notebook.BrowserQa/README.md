# Notesbog: browser-QA

Isoleret loopback-fixture med den rigtige Blazor-app, de rigtige API-controllere,
SQLite i hukommelsen, privat hukommelseslager til filer og den rigtige Yjs-server.
Personer, hold, tokens og servicenøgle er eksplicit fiktive. Ingen produktionsdata,
Auth-adminadgang eller eksisterende database anvendes.

Fra repoets rod, med .NET 10, Node 22+ og de normale editor-/collaboration-afhængigheder:

```powershell
npm --prefix editor run build
dotnet publish web -c Release -o .tools/notebook-qa-web
node editor/tests/qa-notebook-harness.mjs
```

Åbn **http://localhost:9098/_qa**. Log ind som fiktiv elev, anden elev eller underviser
i separate faner. Fanerne har hver deres sessionStorage. Fixture starter API på
127.0.0.1:8088 og samarbejde på 127.0.0.1:9099 og afviser opstart, hvis en port er optaget.
Stop med Ctrl+C; alle testdata og uploadede filer forsvinder.

Kontrollér oprettelse/redigering og genindlæsning, skabelonkopier, kommentarernes
synlighed, sidedeling kontra hel-notesbogdeling, tilbagekaldelse, backlinks/graf,
læseopdatering efter 15 sekunder, mobilbredde og bevaret skrive-/scrollkontekst.

Dette testprojekt er ikke del af produktionsappen, løsningen eller Docker-builds.
Den særlige auth-handler accepterer kun fixture-processens tre præcise fiktive tokens.
Ingen auth-omgåelse eller QA-rute tilføjes til den rigtige API.
