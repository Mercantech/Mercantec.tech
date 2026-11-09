# Julemandens komplette lydpakke

**28 originale lydeffekter og fem indtalte danske replikker.** Alle enkeltklip er mono PCM16 WAV ved44.100Hz. Klippene er nye kompositioner med en varm palet af klokker, kaneklokker, blød sne, frakkestof og vinterluft. Der er ikke genbrugt Raymond-/robotoptagelser, samples fra nettet eller kendte julesangsmelodier.

## Replikker

1. `phrase_1_welcome.wav`: Ho ho ho! Er du klar til lidt julemagi?
2. `phrase_2_start.wav`: Selv den største opgave løses ét skridt ad gangen.
3. `phrase_3_success.wav`: Godt klaret! Det fortjener en plads på den gode liste.
4. `phrase_4_retry.wav`: En lille pause, og så prøver vi igen.
5. `phrase_5_milestone.wav`: Sammen får vi magien til at virke!

Talen er faktisk renderet lokalt gennem Windows OneCore. Scriptet foretrækker en installeret dansk mandestemme; denne maskine har kun **Microsoft Helle (da-DK, Female)**. Den konkrete leverance bruger derfor Helle med taletempo0,93 og tonehøjde0,76, varm EQ og resampling til karakterens mørkere klang. Stemmens identitet og de installerede danske stemmer fremgår ærligt af manifestet og `scripts/audio/raw/voice.json`. Der er ikke tale om en indspillet skuespillerstemme, stemmekloning eller en ekstern lyd-API.

## Filer og afspilning

- `audio-manifest.json`: alle28handlings-ID'er, fem replikker, præcise varigheder, beskrivelser og provenance.
- `audio-demo.wav` og `audio-demo-cues.json`: samlet prøve af bevægelse, alle evner, ultimate, jubel og fem replikker.
- `all-sfx-preview.wav` og cuefil: samtlige28lydeffekter i rækkefølge.
- `audio-validation.json`: PCM-format, varighed, RMS, peak/DC, stilhed, bløde kanter, SHA256 og unikke bølgeformer.
- `browser-audio-validation.json`: alle33WAV-filer afkodet af Chrome AudioContext og en succesfuld HTMLAudioElement-afspilningsstart pr. fil.

28SFX følger nøjagtigt karakterens28handlings-ID'er. Fodtrin er ét klip pr. fodkontakt; idle og søvn starter ikke automatisk vedvarende baggrundslyd. Pickup/interact er generiske og angiver ikke materialet på en bestemt genstand. Evnelyde følger Stjerneskud, Gavegreb, Julefred, Kanesus, Julevarme og Den store gavefest.

Lydværktøjerne her understøtter ikke subjektiv lytning eller automatisk transskription. Rapporter beskriver derfor de faktisk udførte signal-/browserkontroller og påstår ikke ørekontrol af stemmekvalitet eller udtale. Demoen gør manuel lytning mulig.

## Genskabelse

Fra fuldpakkens rod på Windows, med Python3, ffmpeg i PATH og en installeret da-DK OneCore-stemme:

```text
python scripts/audio/generate_audio.py
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/audio/render_danish_speech.ps1
python scripts/audio/generate_audio.py
python scripts/audio/verify_audio.py
node scripts/audio/verify_browser.cjs
python scripts/audio/publish_audio.py --repo ../santa-avatar-release-20261109
```

Browserkontrollen bruger Playwright og lokal Google Chrome. `SANTA_BROWSER` kan angive en anden installeret Chromium-eksekverbar fil. Der læses ingen API-nøgler. Alle kildekoder, rå OneCore-WAV-filer og stemmeparametre bevares i fuldpakken. Runtime får de33enkeltklip og JSON-manifester/rapporter; de to længere demo-WAV-filer ligger kun i fuldpakken.
