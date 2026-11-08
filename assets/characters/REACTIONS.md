# Karakterernes emojis og reaktioner

LUPP og Raymond har hver 48 reaktioner. Raymonds pakke bevarer de 12 eksisterende
originaler og tilføjer 36 nye; Bæver og Ræv har hver 12. De fire karakterer deler
i alt 120 emojis mellem profiler, bibliotek og editor. Nye billeder er genereret
enkeltvis med den indbyggede imagegen-billedgenerator med de eksisterende figurer
som identitetsreferencer. LUPP bevarer sin lilla 3D-lerform, store øjne og gule
blyant; hans pakke blander mimik, underviserreaktioner og Game Design-motiver.
PNG-originalerne er transparente, 1254 × 1254 px og bevaret uden billedændringer.

## Billeder og prompts

- Originaler: `web/wwwroot/images/characters/{lupp,raymond,beaver,fox}/reactions/*.png`.
- LUPPs fulde prompts, beskrivelser og genereringsoplysninger ligger i
  [pakke a](lupp/reactions-20261108-a.json), [pakke b](lupp/reactions-20261108-b.json),
  [pakke c](lupp/reactions-20261108-c.json) og [pakke d](lupp/reactions-20261108-d.json).
- De øvrige karakterers prompts ligger fortsat i deres
  `reactions-20261108*.json`-filer i mapperne herunder.
- `reactions-catalog.json` samler navne, ALT-tekster, offentlige billedstier og
  stabile UUIDv5-ID'er. IDs afledes af karakterens slug og billedets stabile ID;
  navne og beskrivelser kan ændres uden at skifte emojiernes identitet.

## Webvisning og editor

Små WebP-forhåndsvisninger bruges i profiler, bibliotek og emojivælgere, mens
profilernes links åbner eller downloader den originale PNG. Forhåndsvisningerne
bevarer transparens og billedudsnit. GIF-animationer og andre eksisterende
illustrationer er uændrede.

`shared/CharacterEmojiCatalog.cs` er fælles kilde for profilgallerier og
`/api/emojis`, så Blazor og React-editoren ser de samme 120 karakteremojis. De er
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
Flaget er strikt: en manglende rapportpost eller en ændret kendt fil afvises.

Ved nye katalogposter bruges `--add-new`. Eksisterende PNG- og WebP-filer
hashkontrolleres og genbruges byte for byte; kun poster uden en tidligere
rapportpost får genereret en ny forhåndsvisning. Ændrede kendte filer afvises.
De to genbrugsflag kan ikke kombineres. Uden flag genskabes alle forhåndsvisninger.

Dette er kun pakning af statiske webaktiver. Docker-build og appens runtime
kræver ikke Sharp. `reactions-preview-report.json` dokumenterer filstørrelser
og hashes for originaler og forhåndsvisninger.
