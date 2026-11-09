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

## Læg videoen på en undervisningsside

1. Åbn **Notesbøger** i LUP (`/notion`), og redigér den ønskede undervisningsside.
2. Skriv `/video`, vælg **Upload video**, og vælg MP4-filen. Vent på, at upload og lagring bliver færdig.
3. Klik **Udgiv** eller **Opdatér**. Brug **Mere → Se udgivet** til at kontrollere afspilningen.

Undervisningssider beder om bekræftelse ved filer over 100 MiB. Tilføj gerne en kort tekstforklaring under videoen. Hvis en video får speak, kan undertekster vises i selve videoen, da videoblokken ikke har et felt til separate undertekster.

Se [Manim-skillen](https://github.com/adithya-s-k/manim_skill/tree/main/skills/manimce-best-practices) for mønstre til scener, grafer og formler.
