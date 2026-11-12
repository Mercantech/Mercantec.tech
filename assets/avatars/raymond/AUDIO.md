# Raymonds komplette lydpakke

28 originale lydeffekter og fem faktiske danske talereplikker til Raymond / Professor Codd.

- Format: PCM16 WAV, mono, 44.100 Hz.
- Karakter: varme træresonanser, bløde tunge elefantfodtrin, cardigan-/luftbevægelser og venlige snabel-/hornklange.
- Tale: dansk Røst Chatterbox med en syntetisk stemmereference formet til Raymonds dybe, rolige klang. Se [den fælles stemmeproduktion](../voices/README.md).
- Raymond har ikke robottens ringmodulation, bip eller forsinkede robotstemme. Ingen BIP-01-lydfiler er genbrugt.
- Små lydeffekter har bevidst lavere lydstyrke end angreb. Talen eksporteres med en peakbegrænser.
- `walk.wav` og `run.wav` er ét fodtrin pr. kontakt, så spillet kan time dem til hver fod, i stedet for at køre et forkert fast gangloop.
- Alle filer er hændelseslyde. Ingen automatisk konstant idle-/sovelyd eller tale.

## Filer og preview

`audio-manifest.json` beskriver de 28 præcise action-id'er, danske labels, taletekst, varigheder og afspilningsregler. `file` er relativ til denne mappe; `path` er relativ til avatarpakkens rod.

`speech-provenance.json` angiver stemmereferencens fingerprint, modelrevision, indstillinger og klippenes SHA-256. Den arkiverede masterpakke indeholder `audio-demo.wav` med de oprindelige Microsoft Helle-replikker, cuefiler og `all-sfx-preview.wav` med de 28 lydeffekter.

Masterpakkens oprindelige signal- og browserrapporter beskriver de arkiverede optagelser. Platformens stemmeproveniens beskriver de fem nye runtime-filer.

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

Den arkiverede masterpakke `design/raymond-avatar-v2/` indeholder SFX-generatoren, rå WinRT-filer og lydprøver fra den oprindelige produktion. Brug [Røst-arbejdsgangen](../voices/README.md) til platformens replikker.
