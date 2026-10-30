---
type: workflow
status: active
tags:
  - ai
  - regler
---

# Agent-regler

Korte regler for AI-agenter (Cursor m.fl.) på dette projekt. Uddybende husregler kan ligge i Cursor rules — denne note er den delte, menneske-/AI-læsbare version.

## Må

- Læse og opdatere noter i `obsidian/` som del af planlægning.
- Foreslå og implementere kode inden for [[Stack-og-hosting]].
- Bruge Dokploy MCP til drift, når det er relevant.
- Skrive på dansk.

## Må ikke

- Rulle egen auth/JWT — brug Mercantec Auth.
- Committe secrets (`.env`, credentials, API-nøgler).
- Hardcode miljø-URL’er, CORS-origins eller DB-hosts til localhost som “sandhed” i produktion.
- Tilføje Cursor co-author / Made-with i commits.
- Kalde Auth global signout fra SPA som standard-logout.

## Ved tvivl

1. Tjek denne vault ([[Start her]]).
2. Spørg Kasper/Mathias.
3. Log valget som [[ADR]] hvis det er varigt.
