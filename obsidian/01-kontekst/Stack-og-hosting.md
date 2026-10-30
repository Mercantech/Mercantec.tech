---
type: kontekst
status: active
tags:
  - stack
  - hosting
  - auth
---

# Stack og hosting

Faste rammer for Mercantec.tech. Detaljerede AI-workflows: [[Husregler-fra-Cursor]], [[Dokploy-drift]], [[Auth-Mercantec]].

## Tech

- **Backend / API:** .NET 10, ASP.NET Core, **controller pattern** (`api/`).
- **Frontend:** Blazor WebAssembly **PWA** (`web/`), nginx i Docker.
- **DB:** Postgres (`mercantec-tech-db`).
- **Alt Docker-klart** — env-baseret config.

## Auth

Se [[Auth-Mercantec]] (Mercantec Auth, PKCE, lokal logout).

## Hosting / domæne

| | |
|--|--|
| **Prod** | **`https://mercantec.tech`** — **apex, uden subdomæne** |
| Platform | Dokploy (`deploy.mags.dk`) |
| Net | `dokploy-network` + aliases `mercantec-tech-api` / `mercantec-tech-db` |
| Web | Traefik → service `web` port **3000** |
| Secrets / CORS | Env pr. miljø — `Cors:SpaOrigins` inkl. `https://mercantec.tech` |

Lokal: `docker compose -f docker-compose.yml -f docker-compose.local.yml up --build`

## Relateret

- [[Dokploy-drift]]
- [[Agent-regler]]
- [[Cursor-arbejdsgang]]
- [[Ansvarsområder]]
