# Lokal dansk Røst-oplæsning

Grupperouletten kan gøre WAV-klip klar med en eksisterende lokal installation af
`CoRal-project/roest-v3-chatterbox-500m`. Tavlevisningen læser gruppenavnet. API'et
understøtter også en udtrykkeligt valgt navneliste fra holdet. Der bruges ingen ekstern taletjeneste.

## Start den eksisterende model

Brug Python fra installationens eget virtuelle miljø og mappen med modellens
allerede downloadede checkpoints:

```powershell
.\scripts\Start-RostSpeech.ps1 `
    -PythonPath '<eksisterende-venv>\Scripts\python.exe' `
    -ModelPath '<eksisterende-model-snapshot>'
```

Installationens miljø skal allerede have Chatterbox, PyTorch, FastAPI, uvicorn og
soundfile. Scriptet installerer og downloader intet og ændrer ingen modelkilde.
Det starter en skjult proces, binder kun `127.0.0.1:8091` og gemmer PID og logfiler
under den ignorerede mappe `.tools/rost-speech`. En anden lokal port kan vælges
med `-Port`. CPU bruges, hvis den eksisterende PyTorch-installation ikke kan bruge CUDA.

`GET http://127.0.0.1:8091/health` svarer med HTTP 503 under opstart og HTTP 200 med
`ready: true`, når modellen er indlæst og den første korte opvarmning af stemmen er færdig.
En kold modelstart kan tage flere minutter.
Der holdes én model i hukommelsen, og der køres kun én generering ad gangen.

Den lokale bro har kun `GET /health` og `POST /speech` med JSON `{ "text": "..." }`.
Den sidste svarer med PCM WAV. Begge ruter afviser forbindelser fra andre adresser
end loopback; broen skal ikke udgives på et netværksinterface eller gennem en proxy.

## Forbind læringsplatformen

API'et har følgende indstillinger:

```text
Rost__Enabled=true
Rost__BaseUrl=http://127.0.0.1:8091
Rost__TimeoutSeconds=30
```

I lokal Docker Compose bruges `http://host.docker.internal:8091` som standard,
så API-containeren kan nå Windows-værtens lokale bro. Desktop Docker understøtter
dette værtsnavn. `ROST_ENABLED` og `ROST_BASE_URL` kan overskrive de lokale værdier.
Den almindelige API-konfiguration har oplæsning slået fra, indtil den tilsluttes.
Der tillades kun HTTP til loopback eller `host.docker.internal`, og redirects følges ikke.

De autentificerede app-ruter er `GET` og `POST
/api/holds/{holdId}/roulette/speech`. Kun holdets undervisere kan bruge dem.
POST bruger `CreateRouletteSpeech(GroupName, MembershipIds, IncludeMembers)`;
navnelisten kan være tom ved oplæsning af kun gruppenavnet. Der tillades højst 12
medlemmer, som alle skal tilhøre holdet. Navne hentes på serveren. Gruppens navn
kan højst være 120 tegn, og camelCase deles i ord før oplæsning.

Forberedte klip caches i hukommelsen i en time og adskilles efter underviser, hold
og oplæsningstekst. Cachen er begrænset til 32 MiB; et WAV-klip kan højst være 8 MiB.
Modellen har en kort, begrænset ventekø og en konfigurerbar svartid på højst 60 sekunder.
Ved opstart, timeout eller manglende model får appen en status/503, så grupperoulette
stadig kan køres uden oplæsning.
