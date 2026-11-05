---
type: adr
status: accepted
tags:
  - adr
  - hosting
  - auth
---

# ADR-004: lup.dk som alias til LUP-appen

## Status

accepted

## Kontekst

Produktnavnet er LUP, og `lup.dk` er et separat Cloudflare-domæne. Appen kører allerede på `https://mercantec.tech` (ADR-001). Auth bruger én offentlig PKCE-klient (`mercantec-tech`).

## Beslutning

**`lup.dk` (og `www.lup.dk`) er et produktionsalias til samme Dokploy-compose (`web`:3000) — ikke en ny stack eller ny `client_id`.**

- Traefik Host-regler / Dokploy `domain-create` peger begge hosts på service `web`
- API `Cors__SpaOrigins` og Auth-serverens `Cors:SpaOrigins` inkluderer begge origins
- Redirect-URIs: `https://lup.dk/auth/callback` og `https://www.lup.dk/auth/callback` på klienten `mercantec-tech`
- Primær apex forbliver `mercantec.tech` (ADR-001)

## Konsekvenser

- DNS/Cloudflare for `lup.dk` skal pege til samme Dokploy-origin/tunnel som `mercantec.tech`
- Auth-admin (MAGS) skal whiteliste redirects + SPA-origins manuelt, hvis Auth MCP ikke er tilgængelig
- Dokploy env `CORS_SPA_ORIGINS` skal opdateres hvis den er sat eksplicit (ellers dækker compose-default)

## Relateret

- [[ADR-001-apex-domæne]]
- [[Dokploy-drift]]
- [[Auth-Mercantec]]
- [[Stack-og-hosting]]
