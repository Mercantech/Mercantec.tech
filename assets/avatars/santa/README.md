# Julemanden · komplet karakterpakke

Julemanden er en selvstændig, oplåselig karakter med samme eksportomfang som BIP-01 og Raymond: **28 separate animationsark, 224 tegnede frames, 28 lydeffekter og fem danske stemmereplikker**.

## Brug pakken

Den komplette masterpakke ligger i workspace under `design/santa-avatar-v1/`. Åbn dens `index.html` med resten af mapperne ved siden af. Previewet kan vise alle otte frames, afspille/pause, kæde ultimate-opladning og udløsning sammen, skifte genstand/projektil og afspille alle replikker. Lyden starter kun efter et klik.

- `sheets/`: 28 transparente PNG-masterark, 2048 × 1024 px, otte 512 × 512 px-frames i 4 × 2.
- `sheets/256/`: 28 runtimeark, 1024 × 512 px, otte 256 × 256 px-frames.
- `previews/`: 28 animerede WebP-filer med de samme otte frames.
- `icons/512/`: ansigtsikon og fire emojis (glad, grin, tænk og jubel), alle 512 × 512 px med ægte alfa.
- `audio/`: 28 originale SFX, fem danske tale-WAV-filer, manifest, validering og en samlet lydprøve.
- `items/`, `projectiles/`, `effects/`: uafhængige sprites.
- `manifest.json`, `actions.json`, `attachment-events.json` og `external-assets.json`: dimensioner, timing, loops, lyd-events, greb/startpunkter og world-layers.

## Animationer

10 sociale: idle, blink, wave, think, talk, point, celebrate, encourage, surprised, unlock.
11 bevægelser og handlinger: walk, run, jump, land, sit, sleep, pickup, interact, hit, knockout, respawn.
5 evner: **Stjerneskud**, **Gavegreb**, **Julefred**, **Kanesus** og **Julevarme**. Ultimate **Den store gavefest** har separate charge- og release-forløb.

Hver handling er tegnet med otte forskellige helkropspositurer. Der bruges ingen gentagne stillbilleder, CSS-vip eller genereret interpolation til at fremstille frames. Hver sekvens har én fælles skalering og ét fælles origo; hop, knæbøjning og fald bevares.

## Separate sprites

Karakterarkene viser kun julemanden med tomme hænder. De otte generiske eksempelgenstande genbruges fra den fælles robotpakke: energikube, bog, nøgle, tandhjul, ædelsten, mønt, kasse og skriftrulle. To nye projektiler, **gylden julestjerne** og **snebold**, har fire frames hver. Otte nye juleeffekter dækker skjold, greb, dash, healing, ultimate-opladning/udløsning, oplåsning og træf.

Pickup tilkobler en valgfri genstand ved frame4 og følger den samme screen-right vante gennem alle otte frames. Stjerneskud spawner ved frame4 fra den udstrakte vante. Genstanden kan udskiftes, og projektil/effekter fortsætter under spillets egne ure og levetider. Masterkoordinater er512 px med origo(256,448); runtimekoordinater er256 px med origo(128,224). Atlasrects beskriver de512 px-teksturer; verdensskala og teksturskala er angivet særskilt.

## Fem replikker

1. Ho ho ho! Er du klar til lidt julemagi?
2. Selv den største opgave løses ét skridt ad gangen.
3. Godt klaret! Det fortjener en plads på den gode liste.
4. En lille pause, og så prøver vi igen.
5. Sammen får vi magien til at virke!

Lydeffekterne er originale og bruger bløde støvletrin, stof, små klokker, træ/luft og julemagi. Talen er syntetiseret lokalt med den installerede danske WinRT-stemme Microsoft Helle og bearbejdet med lavere tonehøjde og varm EQ. Der er ikke brugt en ekstern stemme-API eller en påstået mandlig originalstemme. Alle33WAV-filer er kontrolleret for PCM, duration, signal, clipping, stilhed og dubletter samt afkodet og afspillet i Chrome. Værktøjet understøtter ikke subjektiv lyttekontrol; sådan en kontrol påstås ikke. Se lydens egne manifester og rapporter for præcise værdier.

I dette repository ligger runtimefiler i `web/wwwroot/assets/avatars/santa/`, Arena-effekter i `web/wwwroot/assets/arena/v3/effects/santa-*.webp`, eksakte prompter i denne mappes `prompts/` og lydscripts i `scripts/audio/`.

## Kilder og genskabelse

Alle præcise billedprompter er samlet i [promptpakken](PROMPTS.json).

Grafikken er lavet med **den indbyggede ImageGen-funktion**, uden API-nøgler eller CLI-fallback. Kanonisk karakterreference findes i `source/references/`, de præcise karakterprompter og kilder i `source/generated/`, ikonkilder/prompter i `source/icons/` og separate effekt-/projektilkilder i `source/externals/`. Forkastede varianter bevares lokalt i rejected-mapper og udelades fra ZIP.

Scripts i `scripts/` genskaber normalisering, WebP-preview, separate sprites, ikoner, runtime-eksport, lyd og validering. `build-externals.cjs` skriver `external-assets.generated.json`; denne godkendte version kopieres til `external-assets.json` før endelig eksport. `build-sheets.cjs` bygger28ark og kræver224 forskellige kilde-/master-/runtimeframes, sikre kanter og alle referencedelyde. `export-runtime.cjs` skriver platformens assets og10Arena-WebP-effekter til det isolerede release-worktree.

## Platform og kontrol

Avatar-id er `santa`, navn **Julemanden**, oplåsning **level 4 / 150 guld samt gaver sendt i juletemaet til mindst tre forskellige brugere**. Kravet beregnes på serveren fra gemte gaver; gentagne modtagere tæller én gang, og ældre gaver uden gemt tema tæller ikke. Ejerskab og valg gemmes på brugerkontoen i databasen, og allerede oplåste figurer bevares. Nye karakterer kan ikke importeres som gratis gamle browseroplåsninger. Arena har en selvstændig serverstyret julemandskampopsætning.

`validation.json` dokumenterer28/224 uden mangler, fejl eller advarsler. `qa/preview-report.json` dokumenterer alle224 canvasframes,33lydfiler, ultimate og de uafhængige lag. Sociale/bevægelsesrapporter findes under `qa/social/`, kampkildernes rapport i `combat-source-validation.json`, og platformens rapporter under `qa/platform/`. Ikonernes alfa kan inspiceres med `scripts/check-icons.cjs`; skjulte RGB-farver i fuldt transparente pixels er ikke en baggrund.

Den samlede `santa-avatar-v1.zip` pakkes først efter godkendte billed- og lydrapporter og CRC-kontrolleres. ZIP og masterkilder holdes uden for Git; runtimefiler, prompter, lydscripts og dokumentation følger platformkoden.
