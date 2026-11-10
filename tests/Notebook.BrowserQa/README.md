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
Den særlige auth-handler accepterer kun fixture-processens fire præcise fiktive tokens.
Ingen auth-omgåelse eller QA-rute tilføjes til den rigtige API.

`node editor/tests/qa-references-browser.mjs` bruger samme fixture og fjerde fiktive bruger
uden hold til at teste oprettelse af teori via oversigten, rigtige globale mentions,
udgivelse, private linkmåls synlighed, personnotifikationer og graf på tværs af
undervisningssider og personlige notesbøger. Rapport og screenshots gemmes i
`.tools/references-browser-qa/`. Scriptet kontakter kun loopback og starter ikke hosten.

`node editor/tests/qa-notebook-tree-browser.mjs` afprøver sidemenuen, flytning med træk og tastatur,
sletning og gendannelse med samme ID i personlige notesbøger og teorinotesbøger.

## Tegninger: automatiseret browseraccept

Fixture registrerer de rigtige `DrawingAccess`/`DrawingService`-tjenester, alle40 indlejrede
standardemoji-grafikker og ét custom klistermærke i hukommelseslageret. Ingen AI-provider
registreres. Testen skriver kun til denne fixtures SQLite/S3-erstatning i hukommelsen.

Når ovenstående launcher melder klar, køres acceptscriptet i en anden terminal:

```powershell
node editor/tests/qa-drawing-browser.mjs
```

Scriptet bruger Playwright-installationen i `%TEMP%/mercantec-graph-qa/node_modules/playwright`.
Sæt `PLAYWRIGHT_MODULE` til en anden lokal `playwright/index.mjs` ved behov, eller
`DRAWING_QA_HEADED=1` for et synligt browserforløb. Browseren må kun kontakte loopback-hosts;
opstart kræver fixtures specifikke healthmarkør, så produktions-URL'er afvises.

Reelle UI-forløb dækker:

- Oprettelse via `/tegning`, Excalidraw-figurer, luk/genåbn og bevaret editorfokus.
- Standard/custom emojis som billeder, native PNG-upload og PNG/SVG/redigerbar Excalidraw-eksport.
- Udgivet live-tavle med elevskriver, anden elev som læser, samtidige sceneændringer og tilbagekaldelse.
- Forældredokumentet forbliver skrivebeskyttet for elevskriveren; udgivet snapshot ændres ikke af kladden.
- Systemtema med både lys/mørk præference plus alle fem eksplicitte temaer; mobil390/320px med touchmål.
- Tegninger i en aktiveret logbogsskabelon kopieres til to selvstændige indlæg uden delte drawing-ID'er.

Rapport, screenshots og de tre faktiske eksporter gemmes i `.tools/drawing-browser-qa/`.
`DRAWING_QA_OUTPUT` kan vælge en anden lokal rapportmappe. Scriptet bygger eller starter
ingen host og stopper kun sin egen Chromium-instans; launcheren ejer fixture-processerne.

`node editor/tests/qa-drawing-clipboard.mjs` tester rigtige Ctrl+C/X/V-hændelser og Chromiums
native Clipboard API: figurer, tekstfelter og emoji-billeder må indsættes i tegningen,
mens en markeret baggrundsnote forbliver uændret. Scriptet bruger ingen SDK-clipboardstub.
En Windows STA-proces bevarer de oprindelige OS-clipboardformater kun i RAM. Den gendanner
kun, hvis clipboard stadig indeholder denne kørsels unikke QA-markør eller SDK-element-ID;
uændret clipboard og brugerens eventuelle nye kopiering lades urørt. Ukendte formater
afbryder testen før clipboardskrivninger. Rapporten indeholder kun fiktiv notedata,
scene-ID'er og MIME-metadata, aldrig brugerens oprindelige clipboardindhold.

`node editor/tests/qa-drawing-concurrency.mjs` bruger samme fixture og egne nye
testnoter til to rigtige SDK-klienter. Testen sammenligner native sceneindhold med
den gemte scene efter samtidige ændringer, lagrækkefølge, lokal fortrydelse,
flytning mod sletning og genforbindelse. Den kontrollerer også synlige deltagere
og markører. Clipboard-aflæsning er afgrænset til browserkonteksten og ændrer ikke
brugerens udklipsholder. Rapport gemmes i `.tools/drawing-concurrency-qa/`.
