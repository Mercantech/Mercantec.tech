# Julemandens komplette lydpakke

**28 originale lydeffekter og fem indtalte danske replikker.** Alle enkeltklip er mono PCM16 WAV ved44.100Hz. Klippene er nye kompositioner med en varm palet af klokker, kaneklokker, blød sne, frakkestof og vinterluft. Der er ikke genbrugt Raymond-/robotoptagelser, samples fra nettet eller kendte julesangsmelodier.

## Replikker

1. `phrase_1_welcome.wav`: Ho ho ho! Er du klar til lidt julemagi?
2. `phrase_2_start.wav`: Selv den største opgave løses ét skridt ad gangen.
3. `phrase_3_success.wav`: Godt klaret! Det fortjener en plads på den gode liste.
4. `phrase_4_retry.wav`: En lille pause, og så prøver vi igen.
5. `phrase_5_milestone.wav`: Sammen får vi magien til at virke!

Platformens tale er genereret med dansk Røst Chatterbox og en syntetisk stemmereference formet til Julemandens varme, fyldige klang. Let rum giver stemmen resonans. Referencens fingerprint, modelrevision, indstillinger og hvert klips SHA-256 står i `speech-provenance.json`. Se [den fælles stemmeproduktion](../voices/README.md).

## Filer og afspilning

- `audio-manifest.json`: alle28handlings-ID'er, fem replikker, præcise varigheder, beskrivelser og provenance.
- Arkivets `audio-demo.wav` og `audio-demo-cues.json`: prøve af bevægelse, evner, ultimate, jubel og de oprindelige Microsoft Helle-replikker.
- `all-sfx-preview.wav` og cuefil: samtlige28lydeffekter i rækkefølge.
- `audio-validation.json`: PCM-format, varighed, RMS, peak/DC, stilhed, endepunkter, SHA-256 og unikke bølgeformer.
- `browser-audio-validation.json`: alle33WAV-filer afkodet af Chrome AudioContext og en succesfuld HTMLAudioElement-afspilningsstart pr. fil.

28SFX følger nøjagtigt karakterens28handlings-ID'er. Fodtrin er ét klip pr. fodkontakt; idle og søvn starter ikke automatisk vedvarende baggrundslyd. Pickup/interact er generiske og angiver ikke materialet på en bestemt genstand. Evnelyde følger Stjerneskud, Gavegreb, Julefred, Kanesus, Julevarme og Den store gavefest.

Signal- og browserresultater kan læses i runtime-rapporterne. Udtale og stemmeklang vurderes ved lytning.

## Genskabelse

Den arkiverede masterpakke `design/santa-avatar-v1/` indeholder SFX-generatoren, rå WinRT-filer og lydprøver fra den oprindelige produktion. Brug [Røst-arbejdsgangen](../voices/README.md) til platformens replikker. De 33 enkeltklip og JSON-manifester ligger i runtime. Demoerne ligger i masterpakken.
