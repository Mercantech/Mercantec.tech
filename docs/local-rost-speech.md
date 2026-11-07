# Forudindtalte Røst-ord til grupperoulette

Rouletten bruger en fast bank med 24 forled og 24 efterled. `Neon` og `Opfinderne`
afspilles i rækkefølge fra to genbrugelige WAV-klip. De 48 filer dækker alle 576
kombinationer i navnekataloget, uden at køre en stemmemodel under et spin.

Ordlisten ligger i `shared/roulette-voice-words.json`. Filnavnene er stabile,
f.eks. `prefix-16.wav` og `suffix-04.wav`, under
`web/wwwroot/audio/roulette/announcer-v1/`. Et manifest annoncerer først banken
som komplet, når alle 48 validerede lydfiler er færdige. Ved manglende filer eller
et navn uden for kataloget kan appen bruge browserens lokale stemme.

## Byg lydbanken én gang

Brug Python og de lokale model-checkpoints fra en eksisterende installation af
`CoRal-project/roest-v3-chatterbox-500m`. På denne computer er installationsmålet
`D:\Spilstudiet\Chatterbox`, så de store model- og Python-filer ligger på D-drevet.

En ny installation kan oprettes med Python 3.11, `uv` og pakker fra de officielle
PyPI/PyTorch-registre. CUDA-versionen her passer til computerens NVIDIA-GPU:

```powershell
$env:UV_CACHE_DIR = 'D:\Spilstudiet\Chatterbox\.cache\uv'
$env:UV_LINK_MODE = 'copy'
$env:HF_HOME = 'D:\Spilstudiet\Chatterbox\.cache\huggingface'
uv venv --python 3.11 'D:\Spilstudiet\Chatterbox\.venv'
uv pip install --python 'D:\Spilstudiet\Chatterbox\.venv\Scripts\python.exe' torch==2.6.0 torchaudio==2.6.0 --index-url https://download.pytorch.org/whl/cu124
uv pip install --python 'D:\Spilstudiet\Chatterbox\.venv\Scripts\python.exe' chatterbox-tts==0.1.7 fastapi uvicorn soundfile setuptools==80.9.0
& 'D:\Spilstudiet\Chatterbox\.venv\Scripts\hf.exe' download CoRal-project/roest-v3-chatterbox-500m --revision 7ce205cea6b3b36d9f60f18abb88ff21fa04ea0d --local-dir 'D:\Spilstudiet\Chatterbox\models\roest-v3-chatterbox-500m' --include ve.pt s3gen.pt t3_mtl23ls_v2.safetensors conds.pt grapheme_mtl_merged_expanded_v1.json
```

Den første installation henter flere GB. På et eksternt exFAT-drev kan kopiering
af PyTorchs mange små filer tage længere tid end selve downloadet.

Perths vandmærkning bruger `pkg_resources`, som ikke følger med et rent uv-miljø.
Derfor installeres en kompatibel `setuptools` udtrykkeligt. Broen kontrollerer,
at vandmærkningen kan importeres, før de store model-checkpoints indlæses.

```powershell
.\scripts\Start-RostSpeech.ps1 `
    -PythonPath 'D:\Spilstudiet\Chatterbox\.venv\Scripts\python.exe' `
    -ModelPath 'D:\Spilstudiet\Chatterbox\models\roest-v3-chatterbox-500m'
```

Broen kræver Chatterbox, PyTorch, FastAPI, uvicorn og soundfile i det valgte miljø.
Startscriptet installerer og downloader intet. Det starter en skjult proces på
`127.0.0.1:8091` med PID og logfiler under `.tools/rost-speech`. En kold opstart
kan tage flere minutter; `/health` svarer 503, indtil model og vocoder er varme.

Generatoren bruger kun Pythons standardbibliotek og den lokale bro. Lav først en
kort prøve af `NeonOpfinderne`:

```powershell
& 'D:\Spilstudiet\Chatterbox\.venv\Scripts\python.exe' .\scripts\generate_roulette_voice.py `
    --preview .\.tools\rost-speech\arena-preview.wav
```

Generér derefter banken:

```powershell
& 'D:\Spilstudiet\Chatterbox\.venv\Scripts\python.exe' .\scripts\generate_roulette_voice.py
```

Hvert ord genereres separat én gang. Pipeline beskærer lang stilhed, beholder
konsonanter, giver stemmen let PA-kompression og to korte rumrefleksioner og
normaliserer til -2 dBFS med korte ind- og udfasninger. Filerne er mono PCM16
ved 24 kHz. Korte pauser og samlede afspilningstider håndteres af browseren.

En afbrudt kørsel kan genoptages med samme kommando. SHA256, ordliste, stemme og
behandlingsversion bestemmer, hvilke færdige klip der genbruges. Rå modelklip
gemmes separat under den ignorerede `.tools/rost-speech/raw`, med nøgler baseret på
stemme, tekst og inferenceprofil. Derfor kræver ændrede arenaeffekter ingen nye
modelkald; bankkørslen genbruger også de to klip fra prøven. `--raw-cache` kan
vælge en anden lokal cachemappe. Råklippene udgives ikke sammen med webudgaven.

`--force` genindspiller alle ord og omgår både den færdige bank og råcachen.
Et ændret lydsæt bør udgives under et nyt versionsnavn, når eksisterende
udgivne filer skal blive cachebare.

Når lydbanken er bygget og inkluderet i webudgaven, behøver hverken underviserens
computer eller webserveren at køre Røst under undervisningen.

## Opråb under showet

Rundestart, gruppeintro, afslutning, gemning, spænding og seks korte fejringer har
deres egen bank med 11 forudindtalte klip. De faste tekster og ID'er ligger i
`shared/roulette-announcements.json` og deles med appen gennem
`RouletteAnnouncementCatalog`. De indeholder kun generiske fraser.

```powershell
& 'D:\Spilstudiet\Chatterbox\.venv\Scripts\python.exe' .\scripts\generate_roulette_voice.py --cues
```

Denne tilstand skriver kun til `web/wwwroot/audio/roulette/cues-v1/`; den eksisterende
bank med gruppenavnenes 48 ord bliver ikke ændret. Klippene er begrænset til 15
sekunder, mono PCM16 ved 24 kHz og 1 MiB pr. fil. Manifestet markeres først klar,
når alle 11 validerede klip er færdige. En afbrudt kørsel genoptager de manglende
fraser og genbruger den lokale råcache ligesom ordbanken.

Appens almindelige showopråb bruger disse filer. Kun navne kan vælges for kortere
oplæsning. Hverken opråbene eller gruppenavnene genereres under et spin, og
elevnavne sendes aldrig til generatoren gennem dette flow.

## Speakerstil og en mere begejstret indtaling

Standardstemmen får arenaeffekter i den færdige bank. Det er lydbehandling, ikke
en garanti for et råbende stemmeudtryk: [Røsts modelkort](https://huggingface.co/CoRal-project/roest-v3-chatterbox-500m#known-limitations)
oplyser, at den danske finetune ikke understøtter Chatterboxs intensitetskontrol.
At skrue op for `exaggeration` gør derfor ikke denne model til en wrestling-speaker.

En kort, original, energisk referenceoptagelse kan bruges til et andet stemmeudtryk.
Gem den som 5–20 sekunders WAV, og angiv den ved start:

```powershell
.\scripts\Start-RostSpeech.ps1 `
    -PythonPath '<miljø>\Scripts\python.exe' `
    -ModelPath '<lokale checkpoints>' `
    -ArenaReferencePath '<energisk indtaling>.wav'
```

Lyt til en ny prøve, og genbyg banken. Referencefilens sti kommer kun fra lokal
proceskonfiguration; HTTP-klienter kan ikke bede broen læse vilkårlige lydfiler.

## Lokal bro og eksisterende API

Broen binder kun loopback, henter ingen model ved opstart og behandler én
generering ad gangen. `GET /health` oplyser parathed; `POST /speech` modtager
`{"text":"Neon!","style":"arena"}` og svarer med WAV. `style` kan udelades for
bagudkompatibel neutral oplæsning. Der bruges ingen ekstern taletjeneste.

Det ældre lærerbeskyttede API under `/api/holds/{holdId}/roulette/speech` er
fortsat tilgængeligt, når `Rost__Enabled=true`, men rouletten genererer ikke lyd
gennem det under et spin. Generel serverkonfiguration har dynamisk Røst slået
fra; lokal Docker kan nå broen med `host.docker.internal:8091`.

Pipelinekontrol uden GPU/model:

```powershell
& 'D:\Spilstudiet\Chatterbox\.venv\Scripts\python.exe' -m unittest discover -s scripts -p test_generate_roulette_voice.py -q
```
