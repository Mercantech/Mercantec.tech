# Nyt i LUP

Undervisernes billedbaserede ændringsoversigt ligger på `/teacher/updates`. Indgangen findes under avatarikonet. Katalog og billeder leveres fra `/api/platform-updates` og kræver en autentificeret underviser/admin. De private billeder er indlejrede ressourcer i API'et og ligger ikke i web-appens offentlige filer.

## Tilføj en ændring

1. Tag et skærmbillede af den faktiske funktion med testdata, og beskær det omkring ændringen. Gem et optimeret WebP-billede i `Images/`. Brug gerne 1.000-1.300 px i bredden, når originalen har opløsning til det.
2. Tilføj en post i `PlatformUpdatesCatalog.cs` med stabilt ID, dato, kort titel og 1-2 sætninger. `After` er obligatorisk; `Before` tilføjes kun, når der findes et ægte tidligere billede af samme funktion. Billed-ID er filnavnet uden `.webp`.
3. Skriv en præcis alternativ tekst. Hold testpersoner og rigtige elevdata adskilt; billederne må ikke indeholde faktiske private elevoplysninger.
4. Byg og opdatér både API og web. Nye billeder kommer automatisk med som indlejrede ressourcer. Datoerne sorteres nyeste først i visningen.

## Billedgrundlag i første udgave

De første 13 poster dækker større ændringer fra 4.-7. november 2026, ikke hvert enkelt Git-commit. De historiske skærmbilleder stammer fra projektets tidligere isolerede browserkontroller. `image-sources.json` dokumenterer originalernes relative placering i det lokale arbejdsområde, beskæringer og billedstørrelser. De færdige billeder leveres i `Images/`; originalerne er ikke en build-afhængighed.

- `notification-dropdown`, `library`, `hold-navigation`: tidligere skærmbilleder i arbejdsområdets `output/lup-*.png`.
- `characters`, `notebook`, `roulette`: nye optagelser af den faktiske app fra `origin/main` ved `0f706f7`, med isolerede fiktive konti og API-fixtures. Ekstern trafik var blokeret.
- `published-pages`, `poll-studio`, `newyear`, `avatar`, `apartment`: eksisterende billeder fra `design/`, beskåret omkring funktionen.
- `gift-before` / `gift-after`: den tidligere pakkevisning sammenlignet med den personlige åbnescene.
- `student-before` / `student-after`: elevforsiden før og efter det kompakte Lectio-/VUF-panel blev tilføjet.

Oversigten vedligeholdes som en del af koden. Den publicerer ikke automatisk nye poster ved hver ændring.
