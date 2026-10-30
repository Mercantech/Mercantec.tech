---
type: kontekst
status: active
tags:
  - stack
  - hosting
  - auth
---

# Stack og hosting

Faste rammer for Mercantec.tech (fra husregler). AI og mennesker følger disse, medmindre en [[ADR]] ændrer dem.

## Tech

- **Backend / API:** C# / .NET som standard.
- **Frontend:** efter behov (fx Vite/React), klar til Docker.
- **Alt skal køre i Docker** — Compose-venligt, env-baseret config.

## Auth

- Brug altid **Mercantec Auth** — ingen egen login/JWT-løsning.
- Flow: authorization code + PKCE S256.
- Manifest: `https://auth.mercantec.tech/.well-known/mercantec-auth.json`
- Logout i apps: kun lokal (ryd tokens) — kald ikke Auth-signout fra SPA som standard.

## Hosting

- **Dokploy** på `deploy.mags.dk`.
- Genbrug `dokploy-network` med **unikke network-aliases** pr. app (undgå generiske `api`/`db`).
- Domæner typisk under `*.mercantec.tech` eller `*.mags.dk`.
- Secrets og CORS i env pr. miljø — aldrig i git.

## Relateret

- [[Agent-regler]] — hvad AI må/ikke må ift. auth, secrets, deploy.
- [[Cursor-arbejdsgang]] — hvordan vi arbejder i Cursor med disse rammer.
