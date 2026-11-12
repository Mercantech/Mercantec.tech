# LUPP · original lydpakke

28 originale lydeffekter og fem danske stemmereplikker. Alle runtimefiler er mono PCM16 WAV ved44.100Hz. Lyde afspilles kun efter et aktivt valg; idle og skridt starter ikke automatisk.

SFX er særligt komponeret til LUPPs lilla lerfigur: bløde lertrin, små plop, elastiske svup, filtmallet-toner og idé-/prototypepulser. Hvert af de28forløb har sin egen rytme, tonekurve og lagdeling. Der bruges ikke optagelser, kendte sange eller kopierede bølgeformer fra robotten, Raymond eller julemanden.

## Fem replikker

1. Jeg er LUPP. Hvad skal spilleren prøve først?
2. En lille prototype kan gøre en stor idé tydelig.
3. Prøv det af, lyt til feedback, og ret til.
4. En fejl er en invitation til et nyt forsøg.
5. Dit spil er ude i verden. Lad os se, hvad spillerne gør!

Platformens tale er genereret med dansk Røst Chatterbox og en syntetisk stemmereference formet til LUPPs lyse, lette og legende klang. Referencens fingerprint, modelrevision, indstillinger og hvert klips SHA-256 står i `speech-provenance.json`. Se [den fælles stemmeproduktion](../voices/README.md).

## Filer og genskabelse

- audio-manifest.json forbinder alle28action-id'er og fem replikker til de faktiske WAV-filer.
- Den arkiverede masterpakke `design/lupp-avatar-v1/` indeholder `audio-demo.wav`, cuefil og rå renderinger med den oprindelige Microsoft Helle-stemme.
- all-sfx-preview.wav indeholder alle28SFX i rækkefølge med tilhørende cue-JSON.
- De arkiverede scripts `generate_audio.py` og `render_danish_speech.ps1` kan genskabe den oprindelige masterpakke med dens SFX og WinRT-tale.
- Runtime-rapporten `audio-validation.json` angiver målt PCM-format, varighed, RMS, peak, DC, stilhed, endepunkter og SHA-256.
- Browserkontrollen registrerer Chrome AudioContext-afkodning og HTMLAudioElement-afspilningsstart i `browser-audio-validation.json`.

Signal- og browserresultater kan læses i de to runtime-rapporter. Udtale og stemmeklang vurderes ved lytning.
