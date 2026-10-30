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

## Problem

Elevlister skal spejle skolevirkeligheden uden at låse platformen totalt til Lectio.

## Forslag

- Hold = **kun elevliste**, **1:1 med forløb**
- Elev kan være på **flere** hold
- Optag: **primært Lectio** (via Lectio-Collector); **manuel** tilføjelse i platformen som supplement

## Acceptkriterier

- [ ] Opret forløb opretter/tilknytter præcis ét hold
- [ ] Elevliste synkroniseres primært fra Lectio
- [ ] Underviser/admin kan tilføje elev manuelt
- [ ] Samme elev kan tilhøre flere hold

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
