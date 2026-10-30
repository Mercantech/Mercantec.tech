---
type: kontekst
status: draft
tags:
  - team
  - ansvar
---

# Ansvarsområder

Hvem ejer hvad på Mercantec.tech. **Ejer** = primær kontakt og beslutningstager for området. Den anden kan stadig bidrage — ejeren har det sidste ord.

Ejer angives som **MAGS**, **KASC** eller **Fælles**.

## Oversigt

| Område            | Ejer   | Dækker                                                                                                                                                           |
| ----------------- | ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Auth              | MAGS   | Mercantec Auth-klient, redirect-URIs, roller, token-flow i app                                                                                                   |
| Sikkerhed / GDPR  | Fælles | Elevdata, samtykke, logning, adgangskontrol, dataminimering — se [[GDPR-og-rettigheder]] / [[ADR-002-gdpr-principper]]                                           |
| Integrationer     | MAGS   | Eksterne datakilder og broer; afstemmes altid med Auth. Primær kilde til elevers skoleforløb: [Lectio-Collector](https://github.com/Mercantech/Lectio-Collector) |
| Deploy            | MAGS   | Dokploy, Docker Compose, miljøer (dev/prod), domæner, logs                                                                                                       |
| Design            | KASC   | UI/UX, branding, typografi, visuelle retningslinjer                                                                                                              |
| Backend / API     | Fælles | .NET API, data-modeller, endpoints (forbruger integrationskontrakter)                                                                                            |
| Frontend          | Fælles | Web-UI, routing, auth-PKCE i browser, state                                                                                                                      |
| Database          | Fælles | Postgres-schema, migrationer, backups                                                                                                                            |
| Indhold           | Fælles | Undervisningsmateriale, opgaver, forløbsstruktur                                                                                                                 |
| AI-workflows      | Fælles | Denne Obsidian-vault, prompts, agent-regler                                                                                                                      |
| Produkt / roadmap | Fælles | Feature-prioritering, [[Feature-oversigt]], releases                                                                                                             |

## Integrationer ↔ Auth ↔ Lectio

- **Auth** afgør *hvem* der er logget ind, roller og tokens.
- **Lectio-Collector** leverer *skoleforløbsdata* om elever (eksternt repo: [Mercantech/Lectio-Collector](https://github.com/Mercantech/Lectio-Collector)).
- **Integrationer** ejer kontrakten mellem platformen og Collector (og evt. andre kilder) — aldrig udenom Auth eller uden om sikkerhedsansvaret (GDPR).
- Se også [[Relaterede-projekter]].

## Sådan opdaterer vi

1. Ændr ejer i tabellen (MAGS, KASC eller Fælles).
2. Ved større ansvarsskift: kort note under [[_index|Beslutninger]] eller en [[ADR]].
3. AI skal spørge den relevante ejer ved tvivl om området (se [[Agent-regler]]).

## Relateret

- [[Start her]]
- [[Stack-og-hosting]]
- [[Relaterede-projekter]]
- [[Cursor-arbejdsgang]]
