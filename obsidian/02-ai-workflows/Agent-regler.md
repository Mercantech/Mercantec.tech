---
type: workflow
status: active
tags:
  - ai
  - regler
---

# Agent-regler

Korte regler for AI på Mercantec.tech. Uddybning: [[Husregler-fra-Cursor]], [[Auth-Mercantec]], [[Dokploy-drift]].

## Må

- Læse/opdatere `obsidian/` før og under planlægning.
- Bygge inden for stack: .NET 10 API (controllers) + Blazor WASM PWA + Docker.
- Bruge Dokploy MCP til drift.
- Skrive på **dansk**.
- Bruge officielle logoer (ikke tegne selv).

## Må ikke

- Rulle egen auth/JWT — [[Auth-Mercantec]].
- Committe secrets.
- Hardcode prod-URL’er/CORS/DB-hosts som localhost-sandhed.
- Tilføje Cursor co-author / Made-with i commits.
- Kalde Auth global signout fra SPA som standard-logout.
- Oprette nye Docker bridge-net på Dokploy (brug aliases på `dokploy-network`).
- Antage subdomæne — prod er **`https://mercantec.tech`** (apex).

## Prioritering

- Få noget kørende først; tests/CI senere (ikke TDD-default).
- Arbejde styres af Issues + [[Sprints]] / Project #9.

## Ved tvivl

1. [[Start her]] + [[Stack-og-hosting]]
2. Spørg MAGS/KASC ([[Ansvarsområder]])
3. [[ADR]] ved varige valg
