# LUPP · original lydpakke

28 originale lydeffekter og fem danske stemmereplikker. Alle runtimefiler er mono PCM16 WAV ved44.100Hz. Lyde afspilles kun efter et aktivt valg; idle og skridt starter ikke automatisk.

SFX er særligt komponeret til LUPPs lilla lerfigur: bløde lertrin, små plop, elastiske svup, filtmallet-toner og idé-/prototypepulser. Hvert af de28forløb har sin egen rytme, tonekurve og lagdeling. Der bruges ikke optagelser, kendte sange eller kopierede bølgeformer fra robotten, Raymond eller julemanden.

## Fem replikker

1. Jeg er LUPP. Hvad skal spilleren prøve først?
2. En lille prototype kan gøre en stor idé tydelig.
3. Prøv det af, lyt til feedback, og ret til.
4. En fejl er en invitation til et nyt forsøg.
5. Dit spil er ude i verden. Lad os se, hvad spillerne gør!

Talen er syntetiseret lokalt med den installerede danske Windows OneCore-stemme Microsoft Helle (Female). SpeakingRate1.0 og AudioPitch1.03 giver et let livligt, tydeligt udtryk; efterbehandling bruger let varm EQ, en lille tilstedeværelsesløftning, præcis resampling og korte sikre fades. Stemmens faktiske metadata og rå lokale renderinger er bevaret i den komplette workspace-pakke design/lupp-avatar-v1/scripts/audio/raw/voice.json og samme mappe. Ingen ekstern API, stemmekloning eller robotvocoder anvendes.

## Filer og genskabelse

- audio-manifest.json forbinder alle28action-id'er og fem replikker til de faktiske WAV-filer.
- audio-demo.wav giver en samlet prøve af bevægelse, evner, ultimate, jubel og alle replikker; audio-demo-cues.json giver tidspunkter.
- all-sfx-preview.wav indeholder alle28SFX i rækkefølge med tilhørende cue-JSON.
- ../scripts/audio/generate_audio.py er den fulde selvstændige generator for de28originale SFX og den lokale talepakning.
- render_danish_speech.ps1 renderer de præcise danske tekster med WinRT uden netværkskald.
- verify_audio.py undersøger signalet og sikrer33forskellige bølgeformer, korrekt format, duration, RMS, peak, DC, stilhed og klikfri kanter. Hashes sammenlignes også mod alle tre tidligere avatarpakker.
- verify_browser.cjs bruger rigtig Chrome AudioContext-afkodning og HTMLAudioElement.play()-løfter for alle33WAV-filer. Det dokumenterer succesfuld afkodning og native afspilningsstart, ikke subjektiv lytning eller et fuldt afspillet klip.

Værktøjet understøtter ikke subjektiv lyttekontrol eller taletransskription; sådan en kontrol påstås ikke. Se audio-validation.json og browser-audio-validation.json for faktisk målte resultater og SHA256.
