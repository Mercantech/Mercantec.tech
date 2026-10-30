---
type: feature
status: done
tags:
  - feature
  - gdpr
  - sikkerhed
---

# GDPR og brugerrettigheder

## Problem

Brugere (elever/undervisere) skal kunne forstå, hvilke personoplysninger platformen behandler, og udøve grundlæggende rettigheder (indsigt og sletteanmodning), uden at vi bygger fuld DPO-proces i v1.

## Forslag

- Offentlig side `/privatliv` (formål, datakategorier, Auth vs app vs Lectio, kontakt).
- Authenticated `/rettigheder`: JSON-eksport + sletteanmodning.
- API under `/api/privacy/*`.
- Principper i [[ADR-002-gdpr-principper]].

## Acceptkriterier

- [x] `/privatliv` offentlig
- [x] `GET /api/privacy/export`
- [x] `POST /api/privacy/deletion-request`
- [x] Links fra nav/profil
- [x] ADR + denne note

## Afhængigheder

- Auth (JWT / `sub`)
- Senere: domæne-DB (#4) til rigtig sletning/eksport af Membership m.m.

## Risks

- Bruger forveksler app-sletning med Auth/Lectio-sletning — teksten skal være tydelig.
- Eksport må ikke lække tokens.

## Out of scope

- Automatisk sletning i Mercantec Auth / Lectio
- Juridisk godkendt samtykke-flow
- Retention-jobs

## Relateret

- [[Ansvarsområder]] — Sikkerhed / GDPR (Fælles)
- [[Feature-oversigt]]
- [[Sprints]]
- Issue [#18](https://github.com/Mercantech/Mercantec.tech/issues/18)
