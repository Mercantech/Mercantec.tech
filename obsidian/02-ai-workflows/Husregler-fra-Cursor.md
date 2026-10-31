---
type: workflow
status: active
tags:
  - ai
  - cursor
  - husregler
---

# Husregler (porteret fra generelle Cursor-rules)

Projekt-specifik udgave til Mercantec.tech. Kilde: generelle Cursor-retningslinjer. Se også [[Agent-regler]], [[Stack-og-hosting]], [[Auth-Mercantec]], [[Dokploy-drift]].

## Sprog

- Svar og vault-noter på **dansk**, medmindre andet er aftalt.

## Sider

- **Router og filer på engelsk.** Blazor-`@page` og sidefiler/mapper: `/courses`, `web/Pages/Courses/` — ikke `/forloeb` eller mappen `Forloeb`.
- **Sidens tekst og navbar må være dansk.** Menupunktet kan hedde "Forløb" og stadig linke til `/courses`.
- Gamle danske URL'er må kun være redirects. API-routes og C#-domænetyper skifter ikke navn, bare fordi en side flytter.

## Logoer og branding

- Brug **officielle** logoer (brand kit, Simple Icons, Wikimedia m.m.) — gem i repo (fx `web/wwwroot/`).
- **Tegn ikke** logoer selv (håndskrevet SVG / lookalikes).
- Følg brand guidelines når de er kendte (Design: KASC).

## Tech

- **.NET 10** API med **controllers** + Blazor WASM **PWA** (aftalt for dette repo).
- Alt **Docker-klart**: Dockerfile, Compose, env-config — ingen “kør kun på host”.

## Sikkerhed & tests

- CI/tests **senere**, når kerneflows virker — **ikke TDD** som standard.
- Commit **aldrig** secrets; credentials i env/Dokploy.

## Git (når vi beder om commit)

- Commit på **main** + push til `origin/main` (ingen feature-branch/PR medmindre aftalt).
- Ingen Cursor co-author / Made-with / trailers.
- Korte beskeder med *hvorfor*; rør ikke `git config`.
- Force-push kun hvis eksplicit bedt.

## Hosting (dette projekt)

- **Prod-URL: `https://mercantec.tech`** — **apex**, **uden** subdomæne (`www` kun hvis I eksplicit sætter redirect).
- Dokploy + `dokploy-network` + aliases `mercantec-tech-api` / `mercantec-tech-db`.
- Drift via **Dokploy MCP** — se [[Dokploy-drift]].

## Relateret

- [[Agent-regler]]
- [[Stack-og-hosting]]
- [[Cursor-arbejdsgang]]
