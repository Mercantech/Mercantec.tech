# Matematikvideoer til LUP

Vi bruger Manim Community Edition til at tegne grafer og sætte formler med LaTeX. Scenerne ligger i [`scenes/`](scenes/). Du skal kun have Docker med Compose for at rendere; det [officielle Manim-image](https://docs.manim.community/en/stable/installation/docker.html) indeholder Manim og TeX Live.

Opret outputmappen fra repoets rod første gang: `mkdir -p .tools/manim` (Bash) eller `New-Item -ItemType Directory -Force .tools/manim` (PowerShell). Kør derefter, mens Docker kører:

```sh
docker compose -f docker-compose.manim.yml run --rm manim
```

Det giver en hurtig prøve i `.tools/manim/videos/lineaer_funktion/480p15/LineaerFunktion.mp4`. Prøven viser, hvordan en stigning på 1 langs x-aksen giver en stigning på 2 langs y-aksen for funktionen f(x) = 2x + 1.

Render den samme scene i 1080p og 30 billeder pr. sekund med:

```sh
docker compose -f docker-compose.manim.yml run --rm manim manim --config_file manim.cfg scenes/lineaer_funktion.py LineaerFunktion
```

Den færdige MP4 ligger i `.tools/manim/videos/lineaer_funktion/1080p30/`. Til en ny video skal du tilføje en Python-fil i `scenes/` og skifte filnavn og sceneklasse i kommandoen. `-ql` giver den hurtige prøve. Kildekoden hører til i Git, mens MP4 og rendercache ligger i det ignorerede `.tools/`.

Hvis dit bruger-id på Linux afviger fra containerens, så tilføj `--user "$(id -u):$(id -g)"` efter `run --rm`, så Docker skriver filerne som din bruger.

Med [Manim Community 0.22.1 og LaTeX installeret lokalt](https://docs.manim.community/en/stable/installation.html) kan du også rendere fra repoets rod uden Docker:

```sh
python -m manim --config_file tools/manim/manim.cfg --media_dir .tools/manim tools/manim/scenes/lineaer_funktion.py LineaerFunktion
```

## Lineær regression: boligpriser

Scenen [`lineaer_regression.py`](scenes/lineaer_regression.py) følger fem fiktive boligsalg fra datapunkter til en regressionslinje. Den viser residualer, middelkvadratfejl (MSE), et prisbud på 90 m² og en test på et nyt salg. Til sidst er der en kort regneopgave med facit.

```sh
docker compose -f docker-compose.manim.yml run --rm manim manim --config_file manim.cfg scenes/lineaer_regression.py LineaerRegression
```

Videoen har danske undertekster i selve billedet. For også at rendere med speak skal de ti WAV-klip fra [manuskriptet](regression-speak.json) ligge i `.tools/manim/regression-audio/` som `01.wav` til `10.wav`. Den indtalte udgave bruger [Pipers danske stemme](https://huggingface.co/rhasspy/piper-voices/blob/2b1eb66029b7a525aa9aaa7532433f00549e95b1/da/da_DK/talesyntese/medium/MODEL_CARD).

```sh
docker compose -f docker-compose.manim.yml run --rm -e MANIM_REGRESSION_AUDIO_DIR=/manim/media/regression-audio manim manim --config_file manim.cfg scenes/lineaer_regression.py LineaerRegression
```

Ved lokal render sættes `MANIM_REGRESSION_AUDIO_DIR` til den lokale WAV-mappe. Den færdige video ligger i `.tools/manim/videos/lineaer_regression/1080p30/LineaerRegression.mp4`.

## Over- og underfitting

Scenen [`fitting.py`](scenes/fitting.py) sammenligner en ret linje, et andengradspolynomium og et sjettegradspolynomium. Alle tre tilpasses de samme syv punkter, og deres RMSE måles på de samme ni valideringspunkter. Punkterne og tabskurven er konstrueret til undervisning. Til sidst viser videoen tal fra et trænet netværk på HIGGS-data.

```sh
docker compose -f docker-compose.manim.yml run --rm manim manim --config_file manim.cfg scenes/fitting.py Fitting
```

Scenen kan rendere med undertekster alene. Til udgaven med speak lægges `01.wav` til `10.wav` og `timings.json` i `.tools/manim/fitting-audio/`. Tidskoderne angiver start og slut for hvert cue i [manuskriptet](scenes/fitting_speak.json), målt i sekunder fra starten af det pågældende WAV-klip. Udgaven med speak bruger den samme danske Piper-stemme som regressionsvideoen.

```sh
docker compose -f docker-compose.manim.yml run --rm -e MANIM_FITTING_AUDIO_DIR=/manim/media/fitting-audio manim manim --config_file manim.cfg scenes/fitting.py Fitting
```

Videoen følger [LUP-siden om over- og underfitting](https://lup.dk/notion/7831e2e7-8dde-4cd8-ad92-65ed61440290) og dens [kilde om signal, støj og overfitting](https://elitedatascience.com/overfitting-in-machine-learning). [Kursusøvelsen 4.Fit](https://github.com/Mercantech/MachineLearning/blob/a8a88f0d86ffb5936436837d1f696c979736cfb4/4.Fit/overfit_and_underfit.ipynb) bruger 10.000 HIGGS-eksempler til træning og 1.000 til validering. Nøjagtighederne 100,0 % og 66,7 % ved epoke 100 kommer fra [TensorFlows offentliggjorte kørsel](https://www.tensorflow.org/tutorials/keras/overfit_and_underfit#large_model). [Keras](https://keras.io/api/callbacks/early_stopping/) beskriver tidligt stop og gendannelse af bedste vægte, og [scikit-learn](https://scikit-learn.org/stable/modules/cross_validation.html) beskriver krydsvalidering og separat sluttest.

## Læg videoen på en undervisningsside

1. Åbn **Notesbøger** i LUP (`/notion`), og redigér den ønskede undervisningsside.
2. Skriv `/video`, vælg **Upload video**, og vælg MP4-filen. Vent på, at upload og lagring bliver færdig.
3. Klik **Udgiv** eller **Opdatér**. Brug **Mere → Se udgivet** til at kontrollere afspilningen.

Undervisningssider beder om bekræftelse ved filer over 100 MiB. Tilføj gerne en kort tekstforklaring under videoen. Hvis en video får speak, kan undertekster vises i selve videoen, da videoblokken ikke har et felt til separate undertekster.

Se [Manim-skillen](https://github.com/adithya-s-k/manim_skill/tree/main/skills/manimce-best-practices) for mønstre til scener, grafer og formler.
