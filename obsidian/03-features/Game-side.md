---
type: feature
status: i brug
tags:
  - feature
  - spil
---

# Spiloversigt

`/games` er den offentlige spiloversigt. LUP Arena åbner på `/games/arena` eller `/arena`. Bomberman, Wizard Duel, Tetris, Pong og Tower Defense har egne indgange under `/games`, sammen med Arduino-guiden, manualerne, turneringsbrættet og spilstatus.

Elevspil uploades privat under **Mine projekter → Spil**. De indgår ikke i det offentlige katalog. Se [[Spilupload]] for filvalidering og LUPP-oplåsning.

## Drift og gamle adresser

Den eksisterende [Mercantech/Games-app](https://github.com/Mercantech/Games) og dens spilservices leverer klassikerne. De er indlejret på LUP fra den faste eksterne origin `https://games.mercantec.tech`. Spillets scripts, cookies og lokale data forbliver på den origin. Hver indgang har også et link til at åbne spillet direkte.

Spilcoverne er kopieret fra Mercantech/Games til `web/wwwroot/assets/games/classics/`, så kataloget har egne billeder.

Traefik-reglen i `docker-compose.yml` flytter præcis `https://games.mercantec.tech/` til `https://lup.dk/games` på den fælles Dokploy-proxy. De gamle spil-, guide-, API- og MQTT-adresser skal fortsat nå deres eksisterende tjenester. Det bevarer også spillets Arduino-controllere, som bruger de gamle API-adresser. Hvis domænerne på et tidspunkt fordeles på hver sin proxy, skal den samme præcise forsideomdirigering konfigureres på proxyen, der modtager `games.mercantec.tech`.

Efter deploy skal den gamle forside give 301 til `https://lup.dk/games`. Kontrollér også, at `https://games.mercantec.tech/guide` og mindst én af de gamle spiladresser fortsat svarer med 200, og at spillet kan åbnes fra LUPs katalog.

På LUP fører `/games/avatar-demo` til spiloversigten. `/games/mine` og `/games/upload` fører til **Mine projekter → Spil**.
