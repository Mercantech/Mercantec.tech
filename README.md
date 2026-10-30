# Mercantec.tech

Central læringsplatform for undervisning (Kasper & Mathias).

**Stack:** .NET 10 — Blazor WebAssembly PWA (`web/`) + ASP.NET Core API med controllers (`api/`) + Postgres.

Planlægning: [`obsidian/`](obsidian/) — [Start her](obsidian/00-home/Start%20her.md).  
Sprints: [Project #9](https://github.com/orgs/Mercantech/projects/9/views/1) · [Sprints](obsidian/03-features/Sprints.md).

## Struktur

| Sti | Indhold |
|-----|---------|
| `api/` | .NET 10 Web API (controllers), JWT Auth |
| `web/` | Blazor WASM PWA + nginx i Docker |
| `docker-compose.yml` | Dokploy (`dokploy-network`, aliases) |
| `docker-compose.local.yml` | Lokal port-mapping + lokalt net |
| `obsidian/` | AI-workflows, features, badges |

**Aliases:** `mercantec-tech-api` · `mercantec-tech-db`  
**Prod:** [`https://mercantec.tech`](https://mercantec.tech) (apex)  
**Auth:** client_id `mercantec-tech` — se [Auth-Mercantec](obsidian/02-ai-workflows/Auth-Mercantec.md)

## Docker (anbefalet lokalt)

```bash
# Start
docker compose -f docker-compose.yml -f docker-compose.local.yml up --build

# Stop
docker compose -f docker-compose.yml -f docker-compose.local.yml down
```

| Service | URL |
|---------|-----|
| Web | http://localhost:3000 |
| API health | http://localhost:8080/api/health (eller `/api/health` via web) |
| Login | http://localhost:3000/login |

Kopiér `.env.example` → `.env` ved behov (secrets commit'tes ikke).

## Lokal udvikling (uden Docker)

```bash
dotnet run --project api --launch-profile http
dotnet run --project web
```

- API: `http://localhost:8080` — `GET /api/health`
- Web: Blazor launch-URL; Development pejer API mod port 8080

## Dokploy

`docker-compose.yml` med eksternt `dokploy-network`. Env fra Dokploy / `.env.example`.  
Domain: service `web`, port `3000`, host `mercantec.tech`. Se [Dokploy-drift](obsidian/02-ai-workflows/Dokploy-drift.md).

## Scaffold-check (#1)

- [x] API med health-endpoint
- [x] Web (Blazor PWA) der bygger
- [x] Compose med dokploy-network + unikke aliases
- [x] `docker-compose.local.yml`
- [x] README med compose-kommandoer
