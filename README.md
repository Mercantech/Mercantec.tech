# LUP – Lokal UndervisningsPlatform

Central læringsplatform for undervisning (Kasper & Mathias).

Repository og domæne hedder fortsat `Mercantec.tech` / `mercantec.tech`.

## Start Blazor-appen

Den aktuelle LUP-flade ligger i web/. Start lokalt fra repoets rod:

~~~bash
dotnet run --project web
~~~

Åbn **http://localhost:3000**. HTTP-startprofilen bruger denne port, fordi den er registreret til Mercantec Auths lokale callback. Login bruger fortsat Mercantec Auth. For rigtige hold- og Lectio-data skal API og Postgres også køre; se Docker-opstart nedenfor. Grundtemaerne vælges under **profil → Udseende**: System, Lyst, Mørkt, Jul og Hacker. Temavalg findes også før login.

[Julegaver og sne](obsidian/03-features/Julegaver-og-sne.md) beskriver rigtig gavelevering mellem konti, modtagere og sneindstillinger. Begge brugere skal bruge samme API/database.

[Designgennemgangen](obsidian/03-features/Blazor-designgennemgang.md) beskriver, hvad der er genskabt i Blazor, og hvilke datakoblinger der stadig mangler.

## Den oprindelige designreference

Den oprindelige klikbare designreference ligger i [`design/lup/`](design/lup/README.md). Fra repoets rod:

```bash
python -m http.server 3001 --bind 127.0.0.1 --directory design/lup
```

Åbn **http://localhost:3001**. På Windows kan [`design/lup/start.cmd`](design/lup/start.cmd) også dobbeltklikkes. Visningen kræver hverken .NET, database, login eller en API-nøgle; lokale valg gemmes i browseren. Docker-opstart står i [designets README](design/lup/README.md).

Det er UI-skabelonen til samarbejdet, ikke en erstatning for Blazor-/API-appen. Se [LUP-design og leveranceplan](obsidian/03-features/LUP-design-og-leveranceplan.md) og [den samlede featureplan](obsidian/03-features/Feature-oversigt.md).

## Den eksisterende .NET-app

**Stack:** .NET 10 — Blazor WebAssembly PWA (`web/`) + ASP.NET Core API med controllers (`api/`) + Postgres.

Planlægning: [`obsidian/`](obsidian/) — [Start her](obsidian/00-home/Start%20her.md).  
Sprints: [Project #9](https://github.com/orgs/Mercantech/projects/9/views/1) · [Sprints](obsidian/03-features/Sprints.md).

## Struktur

| Sti | Indhold |
|-----|---------|
| `api/` | .NET 10 Web API + EF/Postgres (Forløb, Hold, Membership) |
| `web/` | Blazor WASM PWA + nginx i Docker |
| `docker-compose.yml` | Dokploy (`dokploy-network`, aliases) |
| `docker-compose.local.yml` | Lokal port-mapping + lokalt net |
| `design/lup/` | Selvstændig klikbar LUP-designreference (port 3001) |
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
- Web: `http://localhost:3000` med HTTP-startprofilen; Development peger API mod port 8080. Brug `dotnet run --project web --launch-profile http`, hvis din IDE vælger en anden profil.

## Domæne-API (stubs)

Kræver Bearer JWT. Underviser/Admin til create.

| Metode | Sti |
|--------|-----|
| GET/POST | `/api/forloeb` |
| GET | `/api/forloeb/{id}` |
| GET | `/api/holds/{id}` · `/api/holds/{id}/memberships` |
| POST | `/api/holds/{id}/memberships` |
| GET | `/api/me/memberships` |

Julegaver: `POST /api/gifts/profile`, `GET /api/gifts/recipients`, `GET/POST /api/gifts` og `POST /api/gifts/{id}/open`. Alle kræver login; se [adgangsregler og drift](obsidian/03-features/Julegaver-og-sne.md).

Migreringer køres automatisk ved API-start (`Database.Migrate()`).

## Test

```bash
dotnet test tests/Mercantec.Tech.Api.Tests
node --test tests/lup-ui.test.cjs
dotnet build web
```

Gavetestene bruger en isoleret SQLite-database; ingen rigtige gaver sendes. Sneens tre regressionstests bruger Nodes indbyggede testkører uden npm-afhængigheder.

## Dokploy

`docker-compose.yml` med eksternt `dokploy-network`. Env fra Dokploy / `.env.example`.  
Domain: service `web`, port `3000`, host `mercantec.tech`. Se [Dokploy-drift](obsidian/02-ai-workflows/Dokploy-drift.md).

## Scaffold-check (#1)

- [x] API med health-endpoint
- [x] Web (Blazor PWA) der bygger
- [x] Compose med dokploy-network + unikke aliases
- [x] `docker-compose.local.yml`
- [x] README med compose-kommandoer
