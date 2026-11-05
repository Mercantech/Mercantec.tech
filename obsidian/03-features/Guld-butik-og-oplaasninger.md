---
type: feature
status: plan
tags: [feature, guld, butik, nice-to-have]
---

# Guld, butik og oplåsninger

## Økonomi og progression

Eleven har forløbs-XP, karriere-XP, et samlet level og virtuelt guld. Godkendelse udløser belønningen; at åbne en side, markere sit arbejde udført eller indsende igen må ikke give ekstra belønninger.

Belønninger skal være entydige og transaktionelle: én pr. elev/quest, og for Daily én pr. elev/quest/dato. XP og guld beregnes af API'et. 200 XP pr. level og 100 XP/20 guld for en standardquest er designets eksempelværdier, som skal afklares før implementering.

## Butik og ejerskab

Kataloget indeholder møbler og kosmetisk avatartøj. En vare kan kræve level, badge/niveau, begge dele eller ingen af delene.

**Oplåst** betyder adgang til at købe. **Ejet** betyder købt eller direkte tildelt. **Taget på/placeret** er en særskilt anvendelse af en ejet vare.

- Hawaii-badget åbner køb af Hawaii-skjorten.
- Fancy Friday åbner et særligt sæt.
- Andre varer kan kræve et emnebadge på fx Stor/Mellem og et bestemt level.
- Tøj kan tages af/på uden at købe igen. Samme avatarprofil kan bruges i understøttede spil.
- Møbler fra butikken skal kunne bruges i rummets eksisterende inventar/placeringsflow.
- En level-sti viser kommende og allerede nåede krav; manglende guld og badges forklares konkret.

## Acceptkriterier

- [ ] XP, level, guldsaldo og posteringer er persistente og knyttet til Auth-`sub`.
- [ ] Gentagen godkendelse giver ikke dobbelt belønning; Daily-dato indgår i nøglen.
- [ ] Køb kontrollerer saldo, krav og ejerskab på serveren og sker atomisk med inventartildeling.
- [ ] Samtidige/dobbelte køb kan ikke give negativ saldo eller dobbelt ejerskab.
- [ ] Startinventar er ejet uden køb; låst/tilgængelig/ejet/taget på kan skelnes i UI.
- [ ] Badge- og levelkrav er synlige på vare-, badge- og oplåsningssider.
- [ ] Tøj følger avataren; møbler følger elevens eget rum.
- [ ] Eksisterende direkte belønninger fra #26 kan repræsenteres særskilt fra adgang til køb.
- [ ] Serveren ejer progressionen; designvisningens rolle- og levelvalg er ikke en produktionsrettighed.

## Afgrænsning og afhængigheder

Butik/garderobe er en **NTH-udvidelse**, ikke et nyt v1-krav i #24–#26. Ingen rigtige penge, handel mellem elever, crafting eller spilfordele. Grundtemaerne system/lys/mørk/jul/nytår/hacker er frit tilgængelige.

[[Levels-XP-Badges]] · [[Karakter-og-certifikatvæg]] · [[Lejlighed-hus-moebler]] · [[Levels-unlocker-ting]] · [[Emne-badges]]

Før XP bliver sandheden for career-level, skal samspillet med den midlertidige manuelle level-sætning i #25 afklares. UI-gruppering af fem badgeniveauer ændrer ikke aftalen om separate badge-ID'er pr. niveau.

## GitHub-opfølgning

- [#33 – XP, karriere-level og guld ved entydig questgodkendelse](https://github.com/Mercantech/Mercantec.tech/issues/33)
- [#34 – NTH: Butik med guld, avatartøj, møbler og badgekrav](https://github.com/Mercantech/Mercantec.tech/issues/34)
