# Raymonds komplette lydpakke

28 originale lydeffekter og fem faktiske danske talereplikker til Raymond / Professor Codd.

- Format: PCM16 WAV, mono, 44.100 Hz.
- Karakter: varme træresonanser, bløde tunge elefantfodtrin, cardigan-/luftbevægelser og venlige snabel-/hornklange.
- Tale: den installerede danske Windows OneCore-stemme Microsoft Helle med separat, lavere karaktertone (AudioPitch 0,86), rolig talehastighed 0,97 og mild varm EQ. Det er lokal syntetisk tale; ingen stemmekloning eller ekstern API.
- Raymond har ikke robottens ringmodulation, bip eller forsinkede robotstemme. Ingen BIP-01-lydfiler er genbrugt.
- Headroom: maksimal peak er under −2,3 dBFS; alle filer har bløde ind-/udfasninger og ingen hårde kliks i enderne. Små bevægelser har bevidst lavere lydstyrke end angreb.
- `walk.wav` og `run.wav` er ét fodtrin pr. kontakt, så spillet kan time dem til hver fod, i stedet for at køre et forkert fast gangloop.
- Alle filer er hændelseslyde. Ingen automatisk konstant idle-/sovelyd eller tale.

## Filer og preview

`audio-manifest.json` beskriver de 28 præcise action-id'er, danske labels, taletekst, varigheder og afspilningsregler. `file` er relativ til denne mappe; `path` er relativ til avatarpakkens rod.

`audio-demo.wav` er en 39,59 sekunders gennemgang af bevægelse, de fem evner, ultimate og alle fem replikker. `audio-demo-cues.json` indeholder starttider. `all-sfx-preview.wav` afspiller alle 28 lyde, og `all-sfx-preview-cues.json` viser deres rækkefølge.

`audio-validation.json` kontrollerer samtlige 33 filer: format, varighed, peak, RMS, DC-offset, lydløse endepunkter, signal og forskellige SHA-256-fingeraftryk. `browser-audio-validation.json` dokumenterer, at Chromium kan afkode alle filer og starte afspilning af alle fem replikker. Der er ingen automatisk transskription eller påstået menneskelig lyttekontrol i disse rapporter. Brug previewet til den subjektive lyttevurdering.

## De fem replikker

1. Jeg er Professor Codd, men du må gerne kalde mig Raymond.
2. Lad os finde forbindelsen — én række ad gangen.
3. Sådan! Nu hænger tabellerne sammen.
4. En lille fejl er bare et godt sted at blive klogere.
5. Tak for pladsen i din samling. Hvad skal vi undersøge først?

## Fem evner og ultimate

| Action-id | Raymonds lyd | Timing |
|---|---|---|
| `ability_1_pulse` | Snabelpuls: kort snabelhorn, lufttryk og blødt impact | Ved affyring; projektilet er separat |
| `ability_2_magnet` | Relationsgreb / JOIN: lag trækkes mod en fælles tone | Ved aktivering |
| `ability_3_shield` | Tryghedsskjold: varm beskyttende akkord | Når skjoldet åbner |
| `ability_4_dash` | Elefantstorm: tungt afsæt og bred luftstrøm | Ved afsæt |
| `ability_5_repair` | Gendannelse: fem stigende træklange og rolig afslutning | Ved genopretning |
| `ultimate_charge` | Den store JOIN: voksende, dyb snabelresonans | Ved opladning; stop ved afbrydelse |
| `ultimate_release` | Den store JOIN: majestætisk horn-/luftbølge | Ved udløsning |

## Genskab

Kør fra workspace-roden:

```text
python design/raymond-avatar-v2/scripts/audio/generate_audio.py
powershell.exe -NoProfile -NonInteractive -File design/raymond-avatar-v2/scripts/audio/render_danish_speech.ps1
python design/raymond-avatar-v2/scripts/audio/generate_audio.py
python design/raymond-avatar-v2/scripts/audio/verify_audio.py
node design/raymond-avatar-v2/scripts/audio/verify_browser.cjs
```

SFX-scriptet bruger Python-standardbiblioteket. Talerenderingen kræver Windows med en installeret da-DK OneCore-stemme; den ændrer ikke registreringsdatabasen og installerer ikke noget. FFmpeg bruges til høj kvalitet ved resampling og mild EQ. Originale rå talefiler samt de faktiske lokale stemmeindstillinger gemmes under `scripts/audio/raw/`.
