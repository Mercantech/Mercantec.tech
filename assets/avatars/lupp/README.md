# LUPP · komplet spilkarakter

LUPP er den eksisterende lilla lerfigur fra Game Design. Hans runde firkantede krop er også hans hoved; store cremefarvede øjne, korte lilla arme og ben, runde fødder og et lille smil bevares. Den gule blyant er en selvstændig genstand. Kanonisk kilde: https://lup.dk/characters/lupp, læst fra den offentlige side den 10. november 2026 og bevaret som `source/references/public-page.txt`.

## Leverance

**28 animationsark, 224 tegnede frames, 28 originale lydeffekter og fem danske stemmereplikker.** Hver handling er et separat 4 × 2-ark med otte forskellige komplette kropspositurer. LUPPs farve, lerstruktur og grundform følger den eksisterende figur.

- `sheets/`:28 gennemsigtige PNG-masterark,2048×1024px med512×512px-frames.
- `sheets/256/`:28 runtimeark,1024×512px med256×256px-frames.
- `previews/`:28 animerede WebP-filer med de samme otte frames.
- `icons/512/`: ansigtsikon og glad/grin/tænk/jubel som fem transparente512px-PNG.
- `items/`: otte generiske genstande genbrugt fra den fælles robotpakke samt LUPPs separat genererede gule blyant.
- `projectiles/`: Idégnist og Prototypekube, fire frames hver.
- `effects/`: otte separate lilla/cyan/gule effekter.
- `audio/`:28 originale clay/plop/elastic-SFX, fem danske tale-WAV-filer, lydeksempel og validering.

Den komplette masterpakke ligger i workspace under `design/lupp-avatar-v1/`. Åbn dens `index.html` med mapperne ved siden af. Biblioteket kan vise og afspille alle frames, pause, genafspille, kæde ultimate sammen, skifte genstand/projektil og spille replikker efter et klik.

## Handlinger og evner

10 sociale: idle, blink, wave, think, talk, point, celebrate, encourage, surprised, unlock.
11 handlinger/bevægelser: walk, run, jump, land, sit, sleep, pickup, interact, hit, knockout, respawn.
5 evner: **Idépuls, Feedbackgreb, Prototypeværn, Iterationsryk og Playtestpause**. Ultimate **Game Jam** har egne opladnings- og udløsningsforløb.

Alle kropsark viser tomme hænder uden blyant, controller, projektil eller effekt. Pickup tilkobler en valgfri genstand ved frame4 og følger den samme screen-right hånd gennem otte kalibrerede positioner. Idépuls udløser sit uafhængige projektil ved frame4 fra den udstrakte hånd. Spillet ejer genstandens identitet, projektilernes bevægelse og effekternes levetider. Masterens koordinater er512px/origo(256,448); runtime er256px/origo(128,224). Teksturatlasrects forbliver512px og er skilt fra verdensskalaen.

## Fem replikker

1. Jeg er LUPP. Hvad skal spilleren prøve først?
2. En lille prototype kan gøre en stor idé tydelig.
3. Prøv det af, lyt til feedback, og ret til.
4. En fejl er en invitation til et nyt forsøg.
5. Dit spil er ude i verden. Lad os se, hvad spillerne gør!

Platformens fem replikker er genereret lokalt med dansk Røst Chatterbox og LUPPs egen lyse, legende stemmereference. Lyden afspilles efter et klik. Den oprindelige masterpakke indeholder den arkiverede Microsoft Helle-indspilning. Se [den fælles stemmeproduktion](../voices/README.md) og [LUPPs lydpakke](AUDIO.md).

## Oplåsning og platform

Avatar-id er `lupp`. Det eneste aktivitetskrav er mindst ét gennemført spilupload i LUP; figuren kan derefter låses op gratis (level1,0guld). Uploadet må gerne være privat. Serveren kræver en gemt registrering af en vellykket valideret spilbygning, der tilhører kontoen; et link, en vilkårlig fil, en mislykket upload eller andres spil giver ingen adgang. Ejerskab, valg og serverberegnet fremdrift følger brugerkontoen mellem computere.

LUPP har sin egen kampopsætning og Idékraft i Arena. Grafik, projektiler, effekter og kamplyde bruger LUPPs egne filer. Arena aktiverer lyd efter brugerens første tryk og respekterer lydknappen; samlingens lydprøver kræver et klik. Den eksisterende offentlige karakterprofil og dens undervisningsmaterialer bevares.

## Kilder og genskabelse

Billederne er lavet med **den indbyggede ImageGen-funktion**, uden CLI-fallback eller billed-API-nøgler. Præcise prompter ligger sammen med kilderne i `source/generated/`, `source/icons/`, `source/externals/` og `source/references/`. Den samlede [promptpakke](PROMPTS.json) følger også med. Afviste originaler bevares i rejected-mapper og udelades fra ZIP.

Scripts i `scripts/` genskaber ark,512/256eksporter, WebP-preview, separate sprites, lyd, runtime-eksport og kvalitetstjek. Én fælles skalering og ét origo pr. sekvens bevarer hop og fald; frames tilpasses ikke individuelt.

`validation.json` kontrollerer alle28/224 med otte unikke kilde-/master-/runtimeframes pr. handling og sikre transparente kanter. `qa/preview-report.json` kontrollerer alle224 faktiske canvasframes,33 native lydafspilninger, ultimate og de separate lag. `qa/runtime-art-validation.json` binder runtimefiler til de godkendte mastereksporter med hashes. Sociale/kampkilder og den faktiske platform har særskilte rapporter. ZIP pakkes først efter godkendte billed-, lyd- og browserrapporter og CRC-kontrolleres.

Runtimefiler: `web/wwwroot/assets/avatars/lupp/`. Arena-effekter: `web/wwwroot/assets/arena/v3/effects/lupp-*.webp`. Kildesæt og master-ZIP ligger uden for Git; runtime, prompter og lydscripts følger platformkoden.
