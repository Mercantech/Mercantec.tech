# Raymond som avatar

De to kildeark er genereret med den indbyggede ImageGen-funktion. Identitetsreferencerne er de uændrede eksisterende filer:

- `web/wwwroot/images/characters/raymond/professor.webp`
- `web/wwwroot/images/characters/raymond/neutral.webp`

De eksisterende billeder blev visuelt gennemgået før genereringen. Raymonds blå hud, store ører, brune øjne, runde bronzebriller, blå hårtot, krøllede snabel, cremefarvede cardigan, blå skjorte, brune bukser og elefantfødder blev bevaret.

## Fælles promptspecifikation

Use case: identity-preserve. Asset type: production 2D animation sprite sheet. Input image 1 is the canonical full-body Raymond identity and clothing reference; input image 2 is the exact canonical face reference. Create the SAME Raymond Stonebraker Codd in a warm detailed hand-painted pencil/gouache storybook style. Preserve his friendly blue elephant identity, warm brown eyes, thin round bronze spectacles, blue swept hair tuft, broad ears, upward-curled trunk, cream knitted cardigan, muted blue buttoned shirt, brown belt and trousers, bare blue elephant feet. Exactly 16 complete full-body sprites in a uniform 4 columns by 4 rows grid, four chronological frames per action, one action per row. Same camera facing right in three-quarter view, same character scale and grounded baseline. Transparent background with actual alpha. Every figure fully contained inside its own cell, with transparent safety margin. No visible grid, text, numbers, labels, panels, floor, ground shadow, scenery, watermark or additional characters. Hands EMPTY: remove the book and pointer from the reference. No held objects, projectiles, effects, speed lines, particles or floating symbols; these must be separate game entities.

## Ark 1: bevægelse

Four chronological frames per row. Row 1 idle loop with subtle breathing and ear movement, feet planted. Row 2 walking loop: contact right, passing, contact left, passing; alternate legs and opposing arms. Row 3 running loop with larger stride, opposing arms and legs and a modest forward lean. Row 4 jump sequence: crouch, takeoff, airborne with bent knees, landing crouch. Keep all jumps fully inside their cells. Requested square 1024 by 1024; the built-in result is 1254 by 1254 and is processed by its actual dimensions.

## Ark 2: sociale udtryk

Four chronological frames per row. Row 1 wave: neutral open empty hand, raise hand, wave left, wave right with a friendly encouraging smile. Row 2 celebrate: proud smile, hands near waist, both arms partially raised, arms up and beaming, arms lowering. Row 3 think: curious face, empty hand near chin, curled trunk and raised eyebrow, gentle nod. Row 4 talk: palm-up friendly start, small open mouth and gesture, smaller mouth with outward hand, relaxed listening. Natural facial anatomy, recognizable trunk and small coordinated gestures; not sixteen different character designs. Requested square 1024 by 1024; the built-in result is 1254 by 1254.

## Runtimeformat og kvalitet

Hvert kildeark deles i 16 celler efter de faktiske billedmål. Hver celle skaleres til 232 × 232 og får 12 px transparent luft på alle sider; de fire frames pakkes i ét 1024 × 256-PNG pr. animation. Dette er filopdeling og skalering af de tegnede frames; der fremstilles ikke falske animationer ved at vippe et stillbillede.

De otte runtimeark ligger i `web/wwwroot/assets/avatars/raymond/sprites/`. Se `manifest.json` samme sted. Alle ark har alpha, fire 256 × 256-frames og transparente kanter; ingen frame rammer cellekanten. Originale ImageGen-resultater og pakke-/kontrolscript er bevaret i workspace-mappen `design/raymond-avatar/` uden for repositoryet. Kildearkene er også kontrolleret på en lys baggrund.

Ansigtsikon og fire reaktioner genbruges direkte fra karakterbiblioteket, så Raymonds eksisterende profil og avatar har samme identitet. Replikkerne er tekstlige; der tilføjes ikke en robotstemme til Raymond.
