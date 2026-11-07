# Raymond-animation: Forbind tabeller

- Genereret 2026-11-07 med indbygget `image_gen.imagegen`.
- Mode: identitetsbevarende billedafledning med lokal reference (`referenced_image_paths`).
- `transparent_background: false`.
- Reference: `C:\Users\Private\Documents\GitHub\SQL-H1\assets\mascot\candidates\01-sql-underviseren.png`, inspiceret med `view_image` før generation.
- Originalfil: `C:\Users\Private\.codex\generated_images\01a1b791-7c21-7d32-bfef-b46886fdc216\exec-c829bc0e-d669-41a5-b476-4fd071fd6843.png`.
- Projektfil: `assets/characters/raymond/animations/join-tables/sheet.png`.
- Originalen er kopieret uændret. Ingen billedredigering eller retries.

## Format og visuel QA

- Faktiske dimensioner: **1536 × 1024 px**, RGB og fuldt opak (ingen alpha-kanal).
- Bredde er delelig med 3 og højde med 2: **6 præcise 512 × 512-celler**.
- Ren 3 × 2-læsning, venstre mod højre og øverst mod nederst. Ingen renderede rammer, gutters, overskrifter eller numre.
- Samme rolige lyseblå malede baggrund i alle seks frames. Hele Raymond og pegepinden er inde i hver celle. Borddiagrammernes kanter krydser ikke cellegrænserne.
- Meget ens figurstørrelse og faste fødder/bundlinje i alle seks celler. Der er små naturlige genereringsforskelle i hovedkontur og øreposition; arkets frames er brugbare til en kort illustreret loop-animation.
- Raymond har den kanoniske blå farve, brune øjne, runde bronzebriller, hårtot, krøllet snabel, cardigan, pegepind og bog. Naturligt lille smil uden overdreven latter.
- To enkle databasetabeller til højre med tilsvarende gyldne nøglesymboler og teal forbindelse. Ingen tekst, logo eller vandmærke.
- Handling: pegepind på venstre nøgle → følger forbindelse → peger på højre nøgle → gyldne nøgler/forbindelse lyser → lille tilfreds nik med sænkede øjenlåg → næsten startpositur. Sidste og første frame passer tæt til loop.
- Filformat/dimensioner blev kontrolleret læsende med Pillow; der er ikke udført frameudskæring eller GIF-samling i denne delopgave.

## Præcis prompt

```text
Create a professional 6-frame 2D animation sprite sheet of the SAME blue elephant professor Raymond in the attached canonical identity reference. This is an identity-preserving illustration edit/derivation, not a redesign. Preserve exactly his warm medium blue skin, gentle warm brown eyes, round bronze glasses, small swept blue hair tuft, broad ears, relaxed curled trunk, cream knitted cardigan over blue shirt, brown trousers and original hand-painted storybook editorial style with soft gouache/pencil texture. His expression stays calm, natural and quietly encouraging with a closed small smile, never exaggerated laughter or an open-mouthed grin. NO 3D, realistic elephant, anime or flat-vector redesign.

CRITICAL SHEET FORMAT: one fully OPAQUE landscape image, exactly 1536 pixels wide by 1024 pixels high. Exactly THREE columns by TWO rows, containing exactly SIX equally sized 512 by 512 square animation cells. No gaps, no gutters, no borders, no grid lines, no outer margins, no captions, no numbering. Cells touch edge-to-edge. A clean virtual cutting grid is at x=512, x=1024 and y=512. No figure, prop or content may cross the virtual boundary of its own cell.

THIS IS ONE FIXED ANIMATION COMPOSITION REPEATED SIX TIMES, not six different scenes or illustrations. In every cell the camera, scale, perspective, framing, lighting, background and object positions are locked identically. Full-body Raymond stands at the left, both feet on the same fixed baseline about 448px down within each cell, holding a slim wooden teaching pointer with his right hand. His entire head, ears, tuft, trunk, hands, cardigan, tail, feet and pointer remain fully inside the cell with breathing room. Same height around 355px in every cell. On the right half of each cell are the SAME two small simple database table diagrams, fixed in place, each composed of four horizontal blank rows inside a neat pale blue rectangular card. The first diagram is a little left of the second, at about the same height. A small GOLD key symbol marks a row in the left diagram, and a matching GOLD key symbol marks a row in the right diagram. Remaining rows use tiny blue/teal circular row symbols. A single curved teal relationship line connects the two key rows. These diagrams must remain unmistakably identical and stationary across all six cells; no readable text or characters in the diagrams. Leave just enough separation between Raymond and the diagrams for his pointer tip to guide attention. The background is the SAME very light icy blue/off-white painted wash in every cell, simple and nearly uniform, with one soft grounding shadow under his fixed feet, no furniture, extra people, decorations or changing scenery.

Animation sequence, reading left to right then top to bottom:
CELL 1 (top left): calm starting pose, pointer gently indicates the GOLD key row of the left database table, Raymond looking toward it.
CELL 2 (top middle): SMALL continuous arm/pointer movement, pointer follows the first half of the teal relationship line; a subtle head turn follows the pointer. Feet/body stay locked.
CELL 3 (top right): pointer indicates the matching GOLD key row in the right table; the head turns only a little further. Feet/body stay locked.
CELL 4 (bottom left): same ending pointer pose while the connection and both matching gold keys glow softly to show the match. Calm natural face, no surprise or laughter.
CELL 5 (bottom middle): Raymond gives a SMALL satisfied downward nod, the glow begins to fade, pointer starts moving gently back toward the left key. Body and feet remain stationary.
CELL 6 (bottom right): Raymond returns calmly almost to CELL 1's starting pose, pointer back near the left key, head almost reset, relationship line ordinary teal again; this final frame should connect smoothly back to frame 1.

Keep all changes small, clear and continuous: only pointer/forearm angle, subtle head angle and the temporary connection glow change. The six full-body characters must be visually consistent clones of the same canonical character, not six new compositions. No text, letters, labels, frame numbers, logos, watermark, speech bubbles, extra fingers, missing glasses, or cropped body parts anywhere. Output the finished opaque 3x2 animation sheet only.
```
