# Sæt rækkerne i orden — Raymond

Dato: 2026-11-07.

## Leverance og værktøj

- Fil: `sheet.png` i denne mappe.
- Indbygget `image_gen.imagegen`, identity-preserve/edit med `referenced_image_paths` og `transparent_background: false`.
- Kanonisk reference, visuelt inspiceret med `view_image` før generering: `C:\Users\Private\Documents\GitHub\SQL-H1\assets\mascot\candidates\01-sql-underviseren.png`.
- Valgt uændret native slutoutput: `C:\Users\Private\.codex\generated_images\01a1b791-c0db-7c91-b9a5-ab55ffb84eca\exec-86b23db3-c502-4c20-b4c7-53402e8ba9bd.png`.
- Faktiske dimensioner: **1536 × 1024 px**, opakt **RGB**.
- Grid: **3 kolonner × 2 rækker**, hver celle **512 × 512 px**. Ingen gutters eller rammer. Læseretning venstre mod højre, øverste række før nederste.
- Filstørrelse: 2082648 bytes. SHA-256: `b16323b72c02d4c58eeab7e6be5acc8de7811d28a54535d4cbfc967e21900e9c`.
- Projektfil og native slutoutput er byte-identiske. Ingen ekstern billedbehandling, beskæring, skalering eller transparensændring.

## Præcis første prompt — første trin

```text
Use case: identity-preserve.
Asset type: a production animation sprite sheet with six consecutive animation cels for a short educational GIF, not six unrelated illustrations.
Reference image: the attached original Raymond elephant professor is the exact character identity and warm painted illustration style to preserve.

Create ONE OPAQUE landscape PNG animation sheet, exactly 1536 pixels wide × 1024 pixels high, divided mathematically into exactly 3 columns × 2 rows of six equal 512 × 512 pixel cells. No gutters, padding between cells, divider lines, frames, headings or captions. Each cell occupies its exact third/half of the sheet. Read animation frames in row-major order: top-left, top-middle, top-right, bottom-left, bottom-middle, bottom-right. Keep a locked-off camera, identical scale, identical environment and identical object geometry in every cell; only the hand/card movement and subtle expression change. Each cell is one animation cel from the SAME shot.

Character invariants: Raymond is the exact kind blue elephant professor from the reference, with warm brown eyes, round bronze spectacles, small blue hair tuft, long relaxed trunk with a modest curl, soft rounded ears, cream knitted cardigan, blue collared shirt, brown trousers and blue feet. Preserve his calm natural friendly face and warm hand-painted editorial/storybook cartoon style with delicate painted texture. Do not redesign him, enlarge his facial features unnaturally, stretch the trunk or exaggerate a laugh. No book or pointer: hands are free for sorting. Show his entire body inside each cell.

Locked composition in EVERY 512 × 512 cell: Raymond stands on the LEFT, his feet on the same stationary floor baseline around y=453. His complete silhouette fits approximately x=35..286 and y=62..454. On the RIGHT is the same small simple wooden teaching table, tabletop around y=365 with steady vertical legs down to the floor. Above the tabletop sit exactly THREE simple wide horizontal database-row cards, approximately x=302..472, each with one large clear centered digit. The cards have consistent sizes, soft blue outlines, flat pale-blue faces for 1 and 2 and muted-gold face for 3. They are clearly separate cards and do not overlap the digits. The only readable marks anywhere on the sheet are the large single digits 1, 2 and 3 on these three cards. Each cell contains exactly one card of each number, with no duplicates and no missing card. Quiet nearly-white very pale-blue background, faint soft grounded shadows. Every cell repeats the same background, table placement, character placement, full framing and lighting.

Exact six-frame action:
1 TOP LEFT: Raymond calmly looks at the unsorted vertical stack; the cards read 3 at the top, 1 in the middle, 2 at the bottom. His near hand approaches the top 3 card.
2 TOP MIDDLE: Raymond clearly grasps and lifts the 3 card with his blue hand slightly UP and RIGHT away from the stack. Cards 1 and 2 remain visible on the table stack; all three digits remain legible. The lifted 3 card is physically held by the hand, no duplicate 3 left behind.
3 TOP RIGHT: his arm and hand move the same 3 card DOWN along the right side, visibly passing to the space below card 2. Cards 1 and 2 are together above it, in that order. This must be a visibly different arm/card position than frame 2.
4 BOTTOM LEFT: the 3 card settles under 2; the three aligned cards now clearly read 1, 2, 3 from top to bottom. Raymond's hand releases card 3 at the lower right.
5 BOTTOM MIDDLE: the cards stay perfectly aligned in the same 1, 2, 3 order. Raymond gives a small satisfied nod and points with his hand toward the sorted stack, a gentle closed-mouth smile.
6 BOTTOM RIGHT: the same sorted 1, 2, 3 stack stays in place; Raymond returns his arm to a relaxed calm ready pose, with the same modest friendly smile as frame 1.

Very important: this must have visible concrete frame-to-frame changes of hand and row-3 card trajectory while the camera, head/body proportions, table, horizon and feet never jump. All of Raymond, table and cards stay comfortably within their own cells. No sprite crosses a cell boundary. No frame numbers, no arrows, no words, no ORDER BY text, no additional characters, logos, watermark or transparency. The image is opaque in all pixels.
```

## Målrettet retry — mellemoutput med stabilt bord

Der blev udført præcis én målrettet native retry for at mindske bordets lodrette bevægelse. Første sheet var første reference/edit-target, den kanoniske Raymond-fil var anden reference. Retry-output: `C:\Users\Private\.codex\generated_images\01a1b791-c0db-7c91-b9a5-ab55ffb84eca\exec-d3424a5f-c546-4a3d-a860-7c82ec9fe6fa.png`.

```text
Use case: identity-preserve, precise-object-edit.
Image 1 is the animation sheet to correct. Image 2 is the original canonical Raymond identity reference.

Make ONE precise correction pass to image 1. Keep its excellent 3-column × 2-row layout, warm painted Raymond identity, subtle facial expressions, soft pale-blue background, exactly three cards per frame with readable digits 1, 2 and 3, and its six logical sorting steps. Preserve the six cels as a single 1536 × 1024 OPAQUE sprite sheet with exact 512 × 512 cells, no gutters, border or labels.

CRITICAL CORRECTION: the table changes height and position between cels in image 1. Lock the SAME small wooden table to EXACTLY THE SAME local cell pixel coordinates in ALL SIX cells: table top surface approximately local y=365, left x=302, right x=491; table legs end at local y=480. Do not move or resize the table to meet the cards. Lock Raymond's head, body, proportions, feet and floor to the SAME local coordinates across ALL SIX cels, except for the deliberately animated right arm and the tiny nod in cel 5. His complete figure stays fully inside each cell, same size. Keep floor baseline fixed near y=486. Camera is locked.

The three 94 × 50 cards form a tabletop stack with three fixed slot centers near local (405,240), (405,290), (405,340); the BOTTOM card rests on the stationary table near y=365. The slot heights and table must stay unchanged in all six frames. At first the fixed slots read 3,1,2 (top-to-bottom); later the SAME fixed slots read 1,2,3. When card3 is lifted away, cards1 and2 smoothly move one slot UP to create the lower empty slot; card3 travels in Raymond's hand via the right-hand side into that bottom slot. The table NEVER moves up or down. Keep all digits clear, exactly one each of1,2,3 in every frame.

Exact preserved sequence, left-to-right top-to-bottom:
Frame1: three fixed stack slots read3,1,2; hand approaches3.
Frame2: hand has lifted3 clearly above/right;1/2 begin shifting up in the stack over the unchanged tabletop.
Frame3: hand brings3 down along right edge toward the bottom slot;1/2 now occupy top/middle slots.
Frame4: same table and slots now show1,2,3; hand releases3 at bottom.
Frame5: same1,2,3 stack, same table, gentle satisfied tiny nod and pointing hand.
Frame6: same1,2,3 stack, same table, calm ready hand at his side.

No new words, captions, arrows, logos, lines between cells, dramatic expression, extra cards, duplicated digits, new props, speech bubbles or transparent pixels. Preserve identity and other successful elements. Six consecutive animation cels of ONE locked shot.
```

Dette mellemoutput låste bordets placering, men introducerede en fjerde tom række i første frame. Den præcise ekstra edit nedenfor fjernede denne række.

## Ekstra præcis edit — valgt slutoutput

Efter aftale blev der udført én ekstra præcis native edit af mellemoutputtet med det stabile bord. Dette mellemoutput blev inspiceret med `view_image` og brugt som eneste `referenced_image_paths`-target: `C:\Users\Private\.codex\generated_images\01a1b791-c0db-7c91-b9a5-ab55ffb84eca\exec-d3424a5f-c546-4a3d-a860-7c82ec9fe6fa.png`. Toolmode var indbygget `image_gen.imagegen`, `precise-object-edit`, `transparent_background: false`.

Præcis slutprompt:

```text
Use case: precise-object-edit.
Edit the attached animation sheet with ONE surgical local change only. The image is a 1536 × 1024 opaque animation sprite sheet, exactly 3 columns × 2 rows, six 512 × 512 cells.

In the TOP LEFT CELL ONLY, there is an unwanted FOURTH, EMPTY pale-blue rounded rectangular row card directly BELOW the numbered 2 card and directly ABOVE the wooden tabletop. It is approximately inside global x=353..455, y=324..370. DELETE ONLY this one EMPTY rounded rectangle, including its blue outline, and restore the same very pale blue background behind it.

Keep the THREE existing numbered cards 3, 1 and 2 in that top-left cell unchanged, in their current positions. Do not move or resize them. Do not change their digits. Do not add a replacement card. Do not shift the table or any card to fill the empty space. It is intentional for this stack to have a small gap above the tabletop.

Everything else in the ENTIRE image must remain visually identical to the input: all six Raymond figures and hand poses, all eyes, glasses, ears, trunks, cardigan textures, all numbered cards in all six cells, all six wooden tables at their current stable locked positions, table legs, shadows, background, framing, grid geometry and cell boundaries. The other FIVE CELLS must remain untouched.

Result must contain exactly three cards per cell, with digits 1, 2 and 3 each present once. Preserve original canvas size 1536 × 1024, opaque background, no gutters or frames. No new text, objects, labels, logos, borders or changes elsewhere. The ONLY requested change is removal of the ONE blank card in the top-left cel.
```

Slutoutput: `C:\Users\Private\.codex\generated_images\01a1b791-c0db-7c91-b9a5-ab55ffb84eca\exec-86b23db3-c502-4c20-b4c7-53402e8ba9bd.png`.

## Visuel slutkontrol

Den tomme fjerde række er fjernet i første celle. Der er præcis tre læselige kort i hver af alle seks celler, med ét af hvert ciffer 1, 2 og 3. Bordplade og ben har nu samme placering i alle frames, omkring lokal y=366 ved bordfladen, så det tidligere hop på cirka 38 px er fjernet. Ingen nye figur-, ciffer- eller gridfejl blev observeret. De nummererede kort er med vilje hævet over bordet i de første frames.

Figuren, hoved-/kropsproportioner og føddernes bundlinje er stabile; hele Raymond og kortene ligger inden for hver 512 × 512 celle. De seks tydelige hånd-/kortpositurer viser 3,1,2; løft 3; flyt 3 under 2; 1,2,3; tilfreds nik/pegen; rolig klar positur. Raymonds blå hud, brune øjne, bronzebriller, hårtot, rolige snabel, creme-cardigan, blå skjorte, brune bukser og malede stil er bevaret. Ingen ord, logoer eller ekstra figurer.

Native image editing genberegner tekstur en smule også uden for det lokale editområde; de andre fem celler er visuelt uændrede i motiv og placering, men ikke pixelidentiske med mellemoutputtet. Læsende Pillow-sammenligning gav en gennemsnitlig absolut kanalforskel på cirka 3,6–4,1 ud af 255 i disse celler. Projektfilen er derimod byte-identisk med det valgte native slutoutput og er ikke efterbehandlet.
