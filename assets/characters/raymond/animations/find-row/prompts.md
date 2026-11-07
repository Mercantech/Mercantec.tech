# Find den manglende række · animationsark

Genereret 7. november 2026 med indbygget `image_gen.imagegen` (standard built-in mode), ikke CLI/API-fallback. `transparent_background: false`. Udvalgt original PNG kopieret uændret til `sheet.png`; ingen billedredigering eller efterbehandling uden for imagegen.

## Kanonisk reference

`C:\Users\Private\Documents\GitHub\SQL-H1\assets\mascot\candidates\01-sql-underviseren.png`

Referencen blev inspiceret med `view_image` før generering. Raymonds oprindelige blå elefantdesign med brune øjne, bronzebriller, hårtot, krøllet snabel og malet cardiganstil er identitetskilden.

## Output og dimensioner

- Udvalgt original efter håndrettelse: `C:\Users\Private\.codex\generated_images\01a1b792-0848-7161-af09-0b5cc022ba2d\exec-b4c34a31-90a6-43e7-bb23-53e9dbebe72f.png`
- Tidligere original før håndrettelse: `C:\Users\Private\.codex\generated_images\01a1b792-0848-7161-af09-0b5cc022ba2d\exec-69e8ed01-d53d-4ccc-b7b3-d5461610c466.png`
- Første original, ikke udvalgt: `C:\Users\Private\.codex\generated_images\01a1b792-0848-7161-af09-0b5cc022ba2d\exec-e6e1e1b0-015c-48d3-b477-9b6bf2926c90.png`
- Faktiske dimensioner: **1536 × 1024 px**, RGB PNG, uigennemsigtig.
- Grid: **3 kolonner × 2 rækker**, nominelt **512 × 512 px** pr. frame, læserækkefølge fra øverste venstre.
- Størrelse: 2.044.911 bytes.
- SHA-256: `6fa35d0344f447a4a9af2e37dfaaa00cf1a18acdf0f1eb47d527f75782636988`.

## Visuel QA

Den første generation havde svage separatorlinjer. Én målrettet retry fjernede dem; det udvalgte ark har ingen synlige panelrammer, gutters, overskrifter, tekst eller logoer. Samme lyse baggrund, tabel, kort, kamera og fulde figur gentages i seks celler. Ører, fødder, lup og kort ligger inden for hver celle. Ansigt og grundudtryk bevarer Raymond; udtrykket er venligt og afdæmpet uden voldsom latter. Tabellen har tre rækker og tre kolonner, midterrækken er tom med stiplede felter.

En efterfølgende særskilt bestilt, målrettet imagegen-edit rettede håndskift i de to øverste celler til højre. Visuelt er det reelt løst: luppen holdes i hånden til venstre i billedet i **alle seks 0-baserede frames 0, 1, 2, 3, 4, 5**. Den anden hånd hænger nede i frame 1 og 2. Den nedre midterste pegegestus, tabelfelter, guldkort og øvrige motivplaceringer er visuelt bevaret. Dimensioner og grid er uændrede. De to rettede positurer er nu små lupbevægelser foran kroppen, hvilket er et roligt tjek af den manglende række.

**Begrænsninger, der stadig er synlige:** 0-baseret frame 4 peger mod tabellens tomme række frem for direkte på det lille guldkort; det er bevidst bevaret i håndrettelsen. Opdagelsesblikket i frame 3 er diskret. Figurstørrelse og hovedposition varierer en smule mellem frames. Arket er brugbart til en legende illustreret GIF, men er ikke en pixelregistreret professionel spriteanimation. Der er lavet én oprindelig retry samt den efterfølgende særskilt bestilte lokale håndrettelse.

## Første præcise prompt

`referenced_image_paths` = den kanoniske reference ovenfor.

```text
Make a production animation contact sheet for a looping GIF. One OPAQUE image exactly 1536 pixels wide by 1024 pixels high, landscape 3:2, containing EXACTLY SIX square frames in a strictly uniform 3-column by 2-row grid. Each frame is exactly 512 by 512 pixels. Frame order is left-to-right then top-to-bottom: 1 top-left, 2 top-middle, 3 top-right, 4 bottom-left, 5 bottom-middle, 6 bottom-right. No gutters, no panel borders, no separators, no frame labels, no captions or headings. Every frame is the SAME small scene and same camera, not six different scenes. Keep the same character scale, position, floor baseline, table position, light and background in all six cells. Each 512x512 frame has generous safe margins; no ear, foot, trunk, magnifier or prop may cross a cell edge.

The supplied local reference is the precise canonical full-body character design. Preserve the ORIGINAL Raymond: friendly blue elephant professor with very large warm brown eyes, round bronze wire spectacles, small swept blue hair tuft, broad soft elephant ears, original long upward-curled trunk, small natural warm smile, cream textured cable-knit cardigan over blue collared shirt, brown trousers, blue feet with ivory nails. Preserve original warm hand-painted editorial cartoon style with dark-blue drawn contours, tactile painted skin and fine knit texture. This is the SAME Raymond in all six frames with exact same face and proportions; no 3D, photorealism, redesign, dramatic laughter, squinting or closed eyes. He is a calm competent teacher.

Repeat this fixed composition in each frame: Raymond's entire standing body on the LEFT half of the square, floor baseline at y=465 within its own square, whole character including both ears inside its frame. At the RIGHT is a simple large upright cream card showing a database table with exactly THREE horizontal rows and three columns, top and bottom rows with a few abstract colored shapes, middle row an empty dashed outline. Table sits in the same place in each frame. A small loose gold-edged row-shaped card with three tiny colored shapes sits just beside the table on the right, in exactly the same position in all frames. Raymond has one brass magnifying glass in one hand. Very pale blue-white softly painted background and subtle grounding shadow; no interior furnishings, no words, letters, numbers or logos. This is a friendly harmless database puzzle, no crime, no real people or accusations.

The six exact sequential animation poses are: FRAME 1 Raymond looks calmly at the empty middle row, holding magnifier low near his chest. FRAME 2 he leans slightly forward and raises the magnifier toward the empty middle row. FRAME 3 he moves the magnifier a little across the table rows, still looking thoughtful. FRAME 4 he notices the small gold-edged loose row-card beside the table, gently raising eyebrows and looking toward it. FRAME 5 he points lightly toward that card with his free hand, looking quietly pleased with a modest closed-mouth smile. FRAME 6 he relaxes and returns the magnifier near his chest, face and body pose very close to FRAME 1, so the loop returns naturally. Small concrete changes in hands, gaze and slight lean only; stable character anatomy, stable background and stable table. No camera motion. No duplicated arms or objects. No text, no watermark, no logo.
```

## Målrettet retry · præcis prompt

`referenced_image_paths` = første genererede ark, derefter den kanoniske reference.

```text
Targeted correction of the supplied animation sheet (reference 1). Preserve its exact 1536x1024 dimensions, EXACTLY 3 columns by 2 rows of 512x512 square frames, same pale blue-white painted background, same table and row card positions, same character scale, same feet baseline, same canonical Raymond identity and warm painted style. Reference 2 is the original canonical Raymond identity design; preserve his blue elephant face, warm brown eyes, round bronze spectacles, hair tuft, upward-curled trunk, cream cardigan and blue shirt exactly.

Make ONLY these corrections: REMOVE every panel separator line, outer frame line and border so there are NO lines at x=512, x=1024 or y=512: frames touch edge-to-edge seamlessly, no gutters. Keep ALL content in each 512x512 cell. The magnifying glass must remain in the SAME hand across all six frames, namely the hand on viewer-left (Raymond's right hand), never swap to the other hand. The viewer-right hand is his free pointing hand. Poses must be small sequential changes: frame1 (top-left) calmly looking at table with magnifier at chest; frame2 (top-middle) slight lean and lift that SAME magnifier hand toward empty row; frame3 (top-right) move SAME magnifier a little along the rows; frame4 (bottom-left) glance slightly toward the small golden row-card to the right of table, gently intrigued; frame5 (bottom-middle) point his free viewer-right hand in the direction of that golden card and look quietly pleased, no open mouth or exaggerated grin; frame6 (bottom-right) return close to frame1 with magnifier near chest. Do not change the table: exactly 3 rows with middle row empty dashed field, exactly 3 columns and simple colored geometric shapes in the other rows. No words, numbers, text or logos. No extra arms, people or objects. Whole figure and magnifier entirely within each cell. Stable camera and composition, opaque pale painted background. This remains a harmless friendly database puzzle.
```

## Særskilt bestilt lokal håndrettelse · præcis prompt

Før edit blev det eksisterende `sheet.png` inspiceret med `view_image`. `referenced_image_paths` havde kun det valgte ark som edit target: `C:\Users\Private\Documents\GitHub\Mercantec.tech\assets\characters\raymond\animations\find-row\sheet.png`. Det er udelukkende erstattet, fordi visuel inspektion bekræftede, at håndproblemet reelt blev løst.

```text
Edit this existing 1536x1024 animation sprite sheet with a VERY LOCAL targeted correction. Keep the image exactly 1536x1024 and its exact borderless grid: 3 columns by 2 rows, 512x512 pixels per cell. DO NOT redraw, recompose or move the rest of the sheet. Preserve all faces, ears, proportions, feet, backgrounds, tables, colored shapes, small gold row cards, lights, and every detail outside the two specified arm/magnifier corrections. Especially preserve the entire bottom row unchanged, including the bottom-middle pointing gesture, and preserve the entire top-left cell unchanged.

ONLY edit the TWO TOP cells at column2 (pixel x512..1023, y0..511) and column3 (x1024..1535,y0..511). In these two cells the magnifying glass is incorrectly held by the hand on the RIGHT SIDE OF THE PICTURE. Transfer the SAME magnifying glass to the hand on the LEFT SIDE OF THE PICTURE, matching how it is held in top-left and all bottom frames. This means Raymond's right hand, on the viewer's left, should have its arm bent inward across the front of his torso and visibly grasp the brass magnifier handle. His other arm, on the viewer's right, must hang calmly DOWN, with an EMPTY hand near his hip. There must be only ONE magnifying glass in each cell. Avoid any raised empty hand on the right or a lens floating on the right.

Precise positions: in the TOP-MIDDLE cell, the lens should be around local x180,y235 (global x692,y235) in front of his cardigan, held by the hand on local x140,y285; his viewer-right hand rests empty around local x228,y340. In the TOP-RIGHT cell, lens around local x185,y230 (global x1209,y230), held by the hand on local x145,y285; his viewer-right hand rests empty around local x229,y340. These are slight natural variations of the top-left hand-and-magnifier pose; do not move the character head or feet. The viewer-left arm has a clear continuous connection from shoulder to elbow to hand to magnifier. Keep the gentle canonical blue elephant Raymond with warm brown eyes, bronze spectacles, blue hair tuft, curled trunk, cream knitted cardigan and subtle natural smile in exactly the existing hand-painted style. No new panels, borders, text, logos, captions, objects or extra limbs.
```
