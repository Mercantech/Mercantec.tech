# Mercantec.tech

Central læringsplatform for undervisning (Kasper & Mathias).

**Stack:** .NET 10 — Blazor WebAssembly PWA (`web/`) + ASP.NET Core API med controllers (`api/`).

Planlægning og AI-workflows: [`obsidian/`](obsidian/) — start med [Start her](obsidian/00-home/Start%20her.md).  
Sprints/issues: [Project #9](https://github.com/orgs/Mercantech/projects/9/views/1) · [Sprints](obsidian/03-features/Sprints.md).

## Lokal udvikling (uden Docker)

```bash
dotnet run --project api --launch-profile http
dotnet run --project web
```

- API: `http://localhost:8080` — health: `GET /api/health`
- Web: se launch URL (Blazor WASM). `appsettings.Development.json` peger API mod port 8080.

## Docker (lokal)

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml up --build
```

- Web: `http://localhost:3000` (nginx proxyer `/api/` → `mercantec-tech-api:8080`)
- Aliases: `mercantec-tech-api`, `mercantec-tech-db`

## Dokploy

Brug `docker-compose.yml` (eksternt `dokploy-network`). Env fra `.env.example`. Auth tilføjes i Sprint 0 (#2).

**Prod-domæne: [`https://mercantec.tech`](https://mercantec.tech)** — apex, **uden** subdomæne. Se `obsidian/02-ai-workflows/Dokploy-drift.md`.

**Auth:** Mercantec Auth, client_id `mercantec-tech`, callback `/auth/callback`. Se `obsidian/02-ai-workflows/Auth-Mercantec.md`.
