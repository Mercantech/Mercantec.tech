# LUP – Lokal UndervisningsPlatform

Den seneste klikbare designreference til Kasper og Mathias. Den kan startes uden .NET, Postgres eller login. Blazor-/API-implementeringen ligger fortsat i repoets `web/` og `api/`.

## Start lokalt

Fra repoets rod, med Python 3:

```bash
python -m http.server 3001 --bind 127.0.0.1 --directory design/lup
```

Åbn **http://localhost:3001**. Stop med `Ctrl+C`.

På Windows kan du også dobbeltklikke på [`start.cmd`](start.cmd).

Med Docker fra repoets rod:

```bash
docker build -t lup-design ./design/lup
docker run --rm -p 127.0.0.1:3001:80 lup-design
```

Åbn samme adresse. Docker-imaget bruger samme nginx-version som repoets Blazor-container.

## Afprøvning

- Åbn profilen øverst til højre for at skifte elev/underviser, aktivt forløb eller tema.
- **Læring** samler forløb, kalender, quests/afleveringer og logbog.
- **Aktiviteter** viser åbne afstemninger og Main-, Side- og Daily-quests.
- **Underviser → Afstemninger** kan starte/lukke spørgsmål og vise resultater på tavlen.
- **Mit univers → Butik** viser tøj, møbler, levelkrav og badgekrav. Tildel fx Hawaii-badget i underviserens badgevisning for at åbne købet af skjorten.
- Statusindikatoren er rød for vigtige handlinger, orange for valgfrie aktiviteter og grøn, når intet mangler.
- **Sideoversigt** i profilmenuen giver adgang til samtlige sider.

Valg gemmes i browserens `localStorage` på den aktuelle adresse. Genindlæsning og direkte links med `#/...` bevarer konteksten. Browserens tilbage-/fremknapper kan bruges.

For en frisk gennemgang kan man bruge et privat browservindue eller slette nøglen `lup-local-state-v1` i browserens lager.

## Hvad er leveret?

Det er den interaktive UI-skabelon fra designarbejdet, med lokale eksempeldata. Køb, opgavetildeling, elev-/underviserskift og afstemninger ændrer kun den lokale browser. Der oprettes ingen brugere, sendes ingen rigtige afleveringer, og der er ikke synkronisering mellem forskellige deltageres computere. UI'et viser ikke forklarende prototypebannere.

Løsningen introducerer ingen ny produktionsstack og er ikke en erstatning for Blazor, Mercantec Auth eller API'et. Se [LUP-design og leveranceplan](../../obsidian/03-features/LUP-design-og-leveranceplan.md) for implementeringsopgaver og afklaringer.

## Filer

| Fil | Formål |
| --- | --- |
| `index.html` | Dokument og appens faste grundstruktur |
| `styles.css` | LUP-design, kompakt header, temaer og responsive visninger |
| `app.js` | Sider, routing og interaktioner; framework-fri designreference |
| `storage.js` | Lokal lagring; ingen OpenAI-/ChatGPT-afhængighed |
| `assets/` | Genererede, optimerede forløbs- og projektbilleder |
| `vendor/` | Lokal Lucide 0.468.0 med licens; ingen CDN-afhængighed for ikoner |

Google Fonts bruges, når nettet er tilgængeligt; systemets sans-serif bruges som fallback. Billeder, ikoner og appfunktioner ligger lokalt. Der er intet npm-installationstrin og ingen API-nøgle.

### Arbejdet videre

Bevar denne mappe som designreference. Flyt funktioner gradvist til Blazor-komponenter og API-kontrakter frem for at bruge lokaltilstanden som database eller adgangskontrol. Ændring af rolle/forløb i denne visning er et afprøvningsgreb, ikke en autorisationsmodel.
