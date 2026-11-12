# Raymond · komplet avatarpakke

Raymond har samme omfang og eksportformat som BIP-01: **28 handlinger, 224 tegnede frames, 28 lydeffekter og fem indtalte danske replikker**.

## Grafik og animation

Hver handling er tegnet som sit eget gennemsigtige ark med otte kronologiske fuldkropspositurer i 4 × 2. Der bruges ikke dublerede stillbilleder, CSS-vip af figuren eller interpolation til at fremstille manglende frames. Kilderne er visuelt gennemgået; uønsket baggrundståge blev fjernet med ImageGen før eksport.

- Sociale: idle, blink, wave, think, talk, point, celebrate, encourage, surprised, unlock.
- Bevægelse/handlinger: walk, run, jump, land, sit, sleep, pickup, interact, hit, knockout, respawn.
- Evner: Snabelpuls, Relationsgreb, Tryghedsskjold, Elefantstorm, Gendannelse.
- Ultimate: Den store JOIN, med separate charge- og release-sekvenser.

Kanonisk ansigt og tøj kommer fra de eksisterende Raymond-billeder i `web/wwwroot/images/characters/raymond/`. De præcise prompter til den indbyggede ImageGen-funktion ligger i `prompts/characters/` og `prompts/externals/`. Originale og rensede billeder er bevaret i workspace-pakken med SHA256-proveniens. Der er ikke brugt en ekstern billed-API eller hemmelige nøgler.

## Runtimefiler

`web/wwwroot/assets/avatars/raymond/` indeholder:

- `sprites/`: 28 PNG-ark, hver 1024 × 512 med 256 × 256-frames.
- `face.png` og fire `emoji-*.png`: ensartede transparente 512-px-PNG-eksporter af den kanoniske grafik. Det eksisterende neutrale384 px-ansigt opskaleres kun til et ensartet eksportformat; det får ikke påstået ny detaljegrad.
- `audio/`: 28 særskilte SFX og fem rigtige danske tale-WAV-filer med manifest.
- `items/`, `projectiles/`, `effects/`: uafhængige sprites. De otte generiske eksempelgenstande genbruges fra den fælles robotpakke. De to projektiltyper har fire frames hver; de otte blå/guld-effekter er tegnet særskilt til Raymond.
- `manifest.json`, `attachment-events.json` og `external-assets.json`: formater, FPS, loop/sluttilstande, lyd-events og kalibrerede greb/startpunkter.

Runtimekontraktens verdenskoordinater er256 px. Atlasrectangles beskriver de uafhængige512 px-teksturer og ændres ikke af verdensskalaen; `coordinateSystem` og `textureFrameSize` gør forskellen eksplicit. 512 px-masterpakken bruger tilsvarende512 px-verdenskoordinater. Hver kildesekvens har én fælles skalering og ét fælles origo for alle otte frames, så hop og fald bevares.

Karakterarkene indeholder kun Raymond. Pickup tilkobler en valgfri genstand ved frame4 til en målt håndkurve. Snabelpuls spawner et selvstændigt projektil ved frame4, som derefter bevæger sig under sit eget verdensur, også efter figurens one-shot er slut. Effekter er separate layers med selvstændig levetid. Previewet kan pause, genafspille, skifte genstand/projektil og rydder alle entities ved figurskift, navigation og reduceret bevægelse.

## Lyd

Lydene er originale, varme og organiske med bløde, tunge trin, tøj/luft, træresonans og snabelhorn. Platformens fem replikker er genereret lokalt med dansk Røst Chatterbox og Raymonds egen dybe, rolige stemmereference. Den oprindelige masterpakke indeholder den arkiverede Microsoft Helle-indspilning. Se [den fælles stemmeproduktion](../voices/README.md) og [Raymonds lydpakke](AUDIO.md).

Browseren afspiller først lyd efter et eksplicit klik. Begge avatarers præcise33filnavne er allowlistet; fjernadresser, traversal, ukendte navne og ekstra queryparametre afvises. Stop, figurskift og navigation afbryder lyden uden sene falske fejlbeskeder. Genskabsscripts findes i `scripts/audio/`.

## Komplet pakke og kontrol

Workspace-pakken `design/raymond-avatar-v2/` indeholder også512 px-masterark (2048 × 1024),28 animeredeWebP'er, individuelle JSON-filer, kilder/prompter, en samlet browser-preview, lydprøve og valideringsrapporter. `design/raymond-avatar-v2.zip` er den komplette CRC-kontrollerede pakke og holdes uden for Git på grund af størrelsen.

`validation.json` og `qa/preview-report.json` dokumenterer28/224 uden mangler, otte forskellige kilde-/runtime-/canvasframes pr. handling, transparente kanter,33 afkodbare lyde, kalibrerede hænder/snabel samt uafhængige genstande/projektiler. Platformens egne tests dækker fortsat databaseejerskab, level/guld, kontoskift og engangsmigrering. Raymond har samme id som før og kræver ingen ny oplåsning eller schema-migrering for eksisterende ejere.

Store avatarark/lyde/effekter installeres ikke som en samlet PWA-download. De hentes og caches, når de faktisk bruges. Brugerens avatar-API-data caches fortsat aldrig af serviceworkeren.
