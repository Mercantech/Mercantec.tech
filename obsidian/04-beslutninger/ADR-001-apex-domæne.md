---
type: adr
status: accepted
tags:
  - adr
  - hosting
---

# ADR-001: Prod på apex mercantec.tech

## Status

accepted

## Kontekst

Platformen skal hostes under mercantec.tech. Mange andre apps bruger subdomæner (`*.mercantec.tech`).

## Beslutning

**Produktion kører på `https://mercantec.tech` (apex) — uden subdomæne.**

## Konsekvenser

- Dokploy domain-create: host `mercantec.tech`, service `web`, port 3000
- CORS / Auth redirect-URIs skal bruge apex-origin
- `www` kun via eksplicit redirect senere, hvis ønsket

## Relateret

- [[_index]]
- [[Dokploy-drift]]
- [[Stack-og-hosting]]
