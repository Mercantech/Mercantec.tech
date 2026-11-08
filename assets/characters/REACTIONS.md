# Karakterernes emojis og reaktioner

Raymond har 48 reaktioner: de 12 eksisterende originaler og 36 nye. Bæver og Ræv
har hver 12. Alle 60 nye billeder er genereret enkeltvis med den indbyggede
imagegen-billedgenerator med de eksisterende figurer som identitetsreferencer.
PNG-originalerne er transparente, 1254 × 1254 px og bevaret uden billedændringer.

## Billeder og prompts

- Originaler: `web/wwwroot/images/characters/{raymond,beaver,fox}/reactions/*.png`.
- De fulde prompts, beskrivelser og genereringsoplysninger ligger i de fire
  `reactions-20261108*.json`-filer i karakterernes mapper herunder.
- `reactions-catalog.json` samler navne, ALT-tekster, offentlige billedstier og
  stabile UUIDv5-ID'er. IDs afledes af karakterens slug og billedets stabile ID;
  navne og beskrivelser kan ændres uden at skifte emojiernes identitet.

## Webvisning og editor

Små WebP-forhåndsvisninger bruges i profiler, bibliotek og emojivælgere, mens
profilernes links åbner eller downloader den originale PNG. Forhåndsvisningerne
bevarer transparens og billedudsnit. GIF-animationer og andre eksisterende
illustrationer er uændrede.

`shared/CharacterEmojiCatalog.cs` er fælles kilde for profilgallerier og
`/api/emojis`, så Blazor og React-editoren ser de samme 72 karakteremojis. De er
indbyggede og kan ikke omdøbes eller slettes i emoji-administrationen. Ældre
uploadede emojis og deres shortcodes bevares ved eventuelle navnekollisioner.
Vatsons eksisterende emojis følger deres hidtidige integration.

Forhåndsvisninger og det genererede C#-katalog kan genskabes fra originalerne:

```powershell
npm install --prefix .tools/character-previews sharp@0.35.5
node scripts/prepare-character-emoji-assets.mjs
```

Ved rene tekstændringer kan `--reuse-previews` tilføjes. Scriptet kontrollerer
da hashes for begge billeder og genbruger kun de allerede validerede filer.

Dette er kun pakning af statiske webaktiver. Docker-build og appens runtime
kræver ikke Sharp. `reactions-preview-report.json` dokumenterer filstørrelser
og hashes for originaler og forhåndsvisninger.
