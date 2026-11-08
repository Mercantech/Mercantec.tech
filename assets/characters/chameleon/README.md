# Kamæleonen · Machine Learning

Den valgte grønne violinbygger er fast identitet. Han er oplært i Italien, spiller selv banjo og underviser i Machine Learning. Navnet er foreløbigt; sluggen `chameleon` og filnavnene forbliver stabile ved et senere navnevalg.

Bevar de varme brune kamæleonøjne, det venlige ansigt, den orange kant på kammen, de grønne skæl, den krøllede hale, den cremefarvede skjorte og læderforklædet. Brug `reference.png` som identitetsreference ved nye generationer. Den detaljerede reference er valgt af brugeren; de øvrige konceptretninger skal ikke genindføres som alternativer.

## Materialer

- Fire portræter: violinbygger, underviser, banjo og feltarbejde.
- 48 individuelt genererede transparente PNG-emojis, fordelt på hverdagsudtryk og ML-/håndværksreaktioner.
- 16 originale infografikker med korte danske diagramtekster, læsbare forklaringer og officielle faglige kilder.
- Fire miljøillustrationer: violinværksted, banjo i aftensolen, drivhus og undervisningsstudie.
- Tre GIF-animationer om klassifikation, træning og datadrift, hver i 512 × 512 med 15 tidslinjeframes over 3,75 sekunder.

Alle illustrationer er lavet med den indbyggede billedgenerator. Originale PNG'er ændres ikke ved webpakning; mindre WebP-filer bruges som previews. GIF'ernes seks særskilt tegnede posesituationer er genereret som tre individuelle 3 × 2-ark og udtrukket med FFmpeg. Der bruges ingen syntetisk interpolation eller manuelt tegnede erstatningsfigurer.

## Prompts og genopbygning

- `portrait-prompt.txt` og `portrait-provenance.json`: den valgte detaljerede figur.
- `reactions-a.json` og `reactions-b.json`: alle reaktionsprompts, identiteter og native kilder.
- `ml-infographics.json`: infografikker, prompts, forklaringer og faglige kilder.
- `artwork.json`: miljøer og portrætvarianter med prompts og kilder.
- `animations/prompts.json` og `animations/manifest.json`: tegneprompts, kilder og forløb.
- `preview-report.json`, `qa-report.json` og `animations/validation.json`: dimensioner, hashes og filkontrol.

Kør fra repoets rod med den lokale Sharp-installation i `.tools/character-previews/node_modules`:

```powershell
node scripts/register-chameleon-emojis.mjs
node scripts/prepare-character-emoji-assets.mjs --add-new
node scripts/prepare-chameleon-assets.mjs
./scripts/Render-ChameleonAnimations.ps1
```

Maskinlæring forklares som en faglig proces, der skal evalueres. Kamæleonens tilpasning er en karaktermetafor; nye input indebærer ikke automatisk genoptræning. Testdata bruges ikke til modelvalg. Datadrift, performance og konsekvenser undersøges særskilt.
