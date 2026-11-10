---
type: feature
status: plan
tags:
  - feature
  - hold
  - lectio
  - integration
---

# Hold og Lectio

## Mine hold

På **Mine hold** (`/courses`) viser hvert kort holdets navn, tilhørende forløb, kilde og oprettelsesdato. Undervisere kan også se antallet af medlemmer og hvor mange der mangler en tilknyttet LUP-konto på hold, de administrerer. Kortene har direkte links til forløb, quests og grupper.

Søg på holdnavn, forløb eller Lectio-hold, og filtrér på kilde eller favoritter. Stjernen markerer et hold som favorit og flytter det øverst. Favoritter følger LUP-kontoen på tværs af enheder. Elevvisning viser kun elevens relevante hold og har ingen favorit- eller redigeringshandlinger.

Vælg **Omdøb** på et hold, du administrerer. Det nye navn vises i LUP på samme hold med de eksisterende links, elevlisten og Lectio-tilknytningen.

## Opret fra Lectio

1. Vælg **Opret fra Lectio** på **Mine hold**.
2. Vælg et af dine fulgte Lectio-hold, eller brug listen over alle gemte hold. Søg på navn, hvis listen er lang.
3. Kontrollér elevlisten, ret eventuelt navnet, og vælg **Opret hold**. Det nye hold vises på **Mine hold**, og du kan åbne det fra kvitteringen.

Allerede importerede hold linker til det eksisterende hold i LUP, når du har adgang til det. Import af flere Lectio-hold samlet og administration af tilknytninger findes fortsat under **Avancerede Lectio-værktøjer**. Et hold kan også oprettes via **Nyt hold manuelt**.

Oversigten hentes fra `GET /api/holds/mine`. Favoritter lagres pr. konto og hold via `PUT` og `DELETE /api/holds/{id}/favorite`, og navnet gemmes via `PATCH /api/holds/{id}/name`. API'et kontrollerer den aktuelle holdadgang og kræver administrationsadgang for omdøbning. Migreringen `HoldFavorites` køres ved API-start.

## Problem

Elevlister skal spejle skolevirkeligheden uden at låse platformen totalt til Lectio.

## Forslag

- Hold = **kun elevliste**, **1:1 med forløb**
- Elev kan være på **flere** hold
- Optag: **primært Lectio** (via Lectio-Collector); **manuel** tilføjelse i platformen som supplement

## Acceptkriterier

- [x] Opret forløb opretter/tilknytter præcis ét hold (API stub)
- [ ] Elevliste synkroniseres primært fra Lectio (kontrakt + scoped read; fuld sync senere)
- [x] Underviser/admin kan tilføje elev manuelt (API stub)
- [x] Samme elev kan tilhøre flere hold (model)
- [x] Elev ser kun egne Lectio-data; underviser ser alt ([[ADR-003-lectio-integration]])

## Afhængigheder

- Integrationer / [Lectio-Collector](https://github.com/Mercantech/Lectio-Collector)
- Auth (bruger-id = `sub`)
- [[Domæne-model]]
- [[Ansvarsområder]]

## Risks

- Lectio-sync vs. manuelle overrides (konflikter)
- GDPR / elevdata — se sikkerhedsansvar

## Out of scope

- Flere hold på samme forløb
- Hold som løse grupper på tværs af forløb

## Relateret

- [[Feature-oversigt]]
- [[Relaterede-projekter]]
- [[Domæne-model]]
