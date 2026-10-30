---
type: workflow
status: active
tags:
  - auth
  - ai
---

# Auth (Mercantec Auth)

Porteret fra generelle Cursor-rules — gælder Mercantec.tech.

## Principper

- Brug **kun** Mercantec Auth — ingen egen JWT/login, ingen implicit flow, ingen password grant.
- Manifest: https://auth.mercantec.tech/.well-known/mercantec-auth.json
- AI-prompt: https://auth-spa.mercantec.tech/ai-prompt.html
- OIDC: `/.well-known/openid-configuration` · JWKS: `/.well-known/jwks.json`
- iss: `https://auth.mercantec.tech` · aud: `mercantec-apps` · RS256 via JWKS

## SPA (Blazor WASM)

- Authorization code + **PKCE S256**
- Authorize: **fuld browser-navigation** (ikke fetch) til `/oauth/authorize`
- Token: POST `/oauth/token` (`application/x-www-form-urlencoded`)
- Tokens i **sessionStorage/memory** — ikke localStorage
- Ved 401: ét refresh-forsøg; gem altid roteret refresh-token
- Logout: **kun lokal** — kald ikke `https://auth.mercantec.tech/signout` som standard

## API

- JWT-middleware: JWKS + iss/aud/exp/kid
- 401 ugyldig token, 403 manglende rolle
- Bruger-nøgle i DB: `sub` (GUID) — aldrig e-mail

## Registrering (dette projekt)

- **client_id:** `mercantec-tech` (offentlig SPA, PKCE)
- Redirect-URIs:
  - `https://mercantec.tech/auth/callback`
  - `http://localhost:3000/auth/callback`
  - `http://127.0.0.1:3000/auth/callback`
  - `http://localhost:5173/auth/callback`
- SPA-origin i `Cors:SpaOrigins` (apex: `https://mercantec.tech`)
- Kontakt: mags@mercantec.dk

## Relateret

- [[Agent-regler]]
- [[Stack-og-hosting]]
- [[Underviser-og-elev-views]]
- Issue: Auth i Sprint 0 (#2)
