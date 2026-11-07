# Dansk Chatterbox-prøve

Prøven kører uden at ændre platformens eksisterende Røst-integration. Den
genererer fem WAV-filer med danske lyde, tal/datoer og tekniske udtryk samt en
`results.json` med modelrevision, pakkeversioner, opstartstid, genereringstid,
lydlængde og målt maksimal proces-RAM. Ved CUDA måles også PyTorchs maksimalt
allokerede GPU-hukommelse (ikke hele GPU'ens forbrug).

## Model og afgrænsning

`chatterbox-tts==0.1.7` fra PyPI indlæser Multilingual V2. Denne prøve må derfor
ikke kaldes en V3-måling. Modelrevisionen er låst til
`5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18` i `ResembleAI/chatterbox`.
Der bruges modellens indbyggede stemme, `language_id="da"`, standardparametre
og seed 42 for hvert klip. Ingen personers stemmer klones.

## Gentag med Docker

Kør fra repository-roden:

```powershell
docker compose -f docker-compose.chatterbox-trial.yml run --build --rm chatterbox-trial
```

Dette er en selvstændig CPU-benchmark med fire CPU'er og 8 GB RAM som grænse.
Den starter ikke produktionsstacken og eksponerer ingen porte. Modelcache gemmes
i en navngiven volume; resultater lander i `.tools/chatterbox-trial/results/`.
Første kørsel kræver internet og flere GB download. Pakken og modelrevisionen er
låst; transitive afhængigheder er ikke et fuldt låst produktionsmiljø.

## Gentag direkte i et isoleret Python 3.11-miljø

```powershell
python -m venv .tools/chatterbox-trial/venv
& .tools/chatterbox-trial/venv/Scripts/python.exe -m pip install torch==2.6.0 torchaudio==2.6.0 --index-url https://download.pytorch.org/whl/cpu
& .tools/chatterbox-trial/venv/Scripts/python.exe -m pip install chatterbox-tts==0.1.7 setuptools==80.9.0 psutil
$env:HF_HOME = Join-Path $PWD '.tools/chatterbox-trial/cache/huggingface'
$env:PKUSEG_HOME = Join-Path $PWD '.tools/chatterbox-trial/cache/pkuseg'
& .tools/chatterbox-trial/venv/Scripts/python.exe scripts/chatterbox_trial.py --device cpu --threads 4
```

Direkte Python-kørsel har ingen containergrænse for RAM. CUDA kræver en egnet
PyTorch-installation og NVIDIA-hardware; brug derefter `--device cuda`.
Når cachen er komplet, tilføj `--offline` for at bruge de gemte modelvægte uden
netværkskald til Hugging Face. Dette flag er ikke en netværksblokering:
tokenizeren indlæser også en kinesisk segmenteringsmodel på cirka 35 MB fra
GitHub ved første opstart, selv ved dansk syntese. `PKUSEG_HOME` gemmer den i
en persistent cache. I den første lokale prøve blev den hentet til pakkens
standardmappe `~/.pkuseg`. Et fuldt offline-image skal forudindlæse begge caches.

## Sådan læses resultaterne

`real_time_factor = generation_seconds / audio_seconds`: under 1 betyder,
at lyden genereres hurtigere end afspilningstiden. Download, indlæsning og
opvarmning måles ikke som del af de varme genereringskald. RAM måles ved
sampling af proces-RSS hvert 100 ms fra modelindlæsningen og kan overse korte
toppe; tallet er ikke et mål for hele maskinens eller containerens RAM.

Lyt til alle klip og kontrollér udtale, udeladte/ekstra ord, tal, pauser og
tekniske forkortelser. Maskinelt gyldige WAV-filer dokumenterer ikke god udtale.
Prøven sætter derfor `listening_review` til `pending`.

## Mulig integration efter prøven

En intern Python-service kan tilbyde `/health` og `/speech`, mens .NET-API'et
styrer Mercantec Auth, adgang, tekstgrænser, kø og eventuel cache i MinIO.
Modellen skal indlæses én gang og generere sekventielt. TTS skal være valgfri,
så fejl ikke forhindrer platformen i at starte. Brug privat net mellem API og
TTS; repositoryets nuværende fælles `dokploy-network` er ikke et privat net.
Den eksisterende `RostSpeechGenerator` accepterer kun loopback og
`host.docker.internal`. En Compose-service kræver derfor en særskilt adapter
eller en eksplicit udvidelse af valideringen til det konfigurerede interne
værtsnavn; det er ikke nok at ændre en URL i miljøvariablerne.
Længere lærestof skal opdeles ved sætningsgrænser: modellen har en intern
grænse for antallet af genererede taletokens. En cache skal inkludere tekst,
sprog, stemme, modelrevision og genereringsindstillinger i sin nøgle.

Kilder: [Chatterbox](https://github.com/resemble-ai/chatterbox),
[modelkort](https://huggingface.co/ResembleAI/chatterbox),
[Docker GPU-konfiguration](https://docs.docker.com/compose/how-tos/gpu-support/).


## Målt 7. november 2026

Lokal Windows-laptop med Intel Core i7-1355U, 16 GB RAM og fire PyTorch-tråde.
Python 3.11, Chatterbox 0.1.7 / Multilingual V2, PyTorch 2.6.0+cpu og
Transformers 5.2.0. Ingen GPU blev benyttet.

| Klip | Lyd, sekunder | Generering, sekunder | Real-time factor |
|---|---:|---:|---:|
| 01-velkomst.wav | 4.56 | 101.65 | 22.29 |
| 02-danske-lyde.wav | 3.80 | 77.23 | 20.32 |
| 03-tal.wav | 6.08 | 67.94 | 11.17 |
| 04-fagtekst.wav | 6.72 | 77.02 | 11.46 |
| 05-tekniske-ord.wav | 5.92 | 64.69 | 10.93 |

Samlet generering: 388.53 sekunder for 27.08 sekunders
lyd, svarende til 14.35 gange lydens længde.
Målt maksimal proces-RSS: 5.79 GB
(5.39 GiB). Modelindlæsning:
57.27 sekunder inklusive tokenizerens første hjælpedownload;
opvarmning: 77.02 sekunder. Det store modeldownload tog
cirka 25 minutter separat og indgår ikke i genereringstiderne.

Alle fem gemte filer er genåbnet og valideret som mono PCM16 WAV ved 24 kHz,
med endelige, ikke-tomme lyddata og den rapporterede varighed. Fagtekstklippet
har én rå sample ved/over fuld skala. Det er ikke en lyttevurdering.
Modellens log viste også repetition/long-tail-stopmekanismer under nogle
genereringer; korrekt udtale og fuldstændighed er derfor stadig ubekræftet.

Konklusion: Denne CPU-opsætning er for langsom til interaktiv oplæsning.
Den kan bruges til forudgenererede klip eller baggrundsjob. GPU-ydelse,
Multilingual V3, serverdrift, samtidige brugere og dansk talekvalitet er ikke
valideret. Næste tekniske prøve bør være på den tiltænkte GPU-vært.

[Rå målinger](benchmarks/chatterbox-cpu-2026-11-07.json). De fem WAV-filer,
`validation.json` og en pakkeliste ligger lokalt under `.tools/chatterbox-trial/`
og er ikke lagt i git. Compose-konfiguration og Python-syntaks er valideret;
Docker-image er ikke bygget/kørt, da den lokale Docker-motor var stoppet.
