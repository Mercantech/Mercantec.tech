---
type: workflow
status: active
tags:
  - dokploy
  - hosting
  - ai
---

# Dokploy-drift

Porteret fra generelle Cursor-rules — tilpasset Mercantec.tech.

## Domæne

| Miljø | Host |
|-------|------|
| **Prod (primær)** | **`mercantec.tech`** (apex — **intet** subdomæne) |
| **Prod (alias)** | **`lup.dk`** (+ `www.lup.dk`) — samme `web`:3000 |
| Dev (senere) | aftales separat (branch + env) — ikke hardcod i kode |

Dokploy: domain på service **`web`**, port **3000**. For mercantec.tech / lup.dk bag Cloudflare: følg org-praksis (`certificateType` / https ift. tunnel).

`lup.dk` er Traefik-labels på `web` (Host `lup.dk` eller `www.lup.dk`). Alternativt Dokploy MCP `domain-create` for begge hosts. DNS/Cloudflare skal pege apex + www til **samme origin/tunnel** som `mercantec.tech`.

## Netværk (påkrævet)

Hosten har opbrugte address pools → **ingen nye bridge-net**.

1. `dokploy-network` som compose `default` (`external: true`)
2. Aliases: **`mercantec-tech-api`**, **`mercantec-tech-db`**, **`mercantec-tech-minio`**
3. Nginx: `resolver 127.0.0.11` + `set $upstream_api http://mercantec-tech-api:8080`
4. Connection string: `Host=mercantec-tech-db`
5. Object storage: API → `http://mercantec-tech-minio:9000` (S3 path-style). MinIO credentials via Dokploy env (`MINIO_ROOT_*`, `MINIO_BUCKET`, `OBJECT_STORAGE_*`) — **ikke** i git.
6. Image: **`pgsty/silo`** (pinned tag i compose). Brug **ikke** `quay.io/minio/minio` / `quay.io/minio/mc` — Quay returnerer **401** (community-images fjernet).
7. MinIO Console (admin UI):
   - **Prod URL:** `https://minio.mercantec.tech` (subdomæne — apex forbliver appen)
   - Kun port **9001** (Console) via Traefik på `dokploy-network`. S3 API **9000** eksponeres **ikke**.
   - Compose sætter Traefik-labels + `MINIO_BROWSER_REDIRECT_URL`. Alternativt Dokploy MCP: `domain-create` → host `minio.mercantec.tech`, service `minio`, port `9001`, `certificateType` ift. Cloudflare-praksis, derefter `compose-deploy`.
   - Login: `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` (stærk adgangskode i Dokploy — ikke default).
   - Lokalt: http://localhost:9001 (med `docker-compose.local.yml`).

Proxyér **aldrig** til generisk `http://api:...` på det delte net.

## .NET API

- `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`
- `ASPNETCORE_URLS=http://+:8080`
- Connection string fra **env** (`ConnectionStrings__Default` / `CONNECTION_STRING`)

## MCP-flow

Brug **Dokploy MCP** (ikke ad hoc curl):

1. Compose peger på GitHub repo/branch + `composePath`
2. `compose-saveEnvironment` (`createEnvFile: true`) — `CORS_SPA_ORIGINS` skal inkludere `https://mercantec.tech;https://lup.dk;https://www.lup.dk` (+ lokale origins efter behov)
3. `domain-create` → host **`mercantec.tech`**, service `web`, port 3000
4. `domain-create` → host **`lup.dk`**, service `web`, port 3000 (og evt. `www.lup.dk`) — eller stol på Traefik-labels i compose
5. (valgfrit hvis labels mangler) `domain-create` → host **`minio.mercantec.tech`**, service `minio`, port **9001**
6. `compose-deploy`
7. Fejl: `deployment-readLogs` / `compose-readLogs`

Typiske fejl: subnet-pool, inotify, localhost-DB, nginx upstream DNS.

## Relateret

- [[Stack-og-hosting]]
- [[Deploy-Dokploy]] (prompt)
- [[Husregler-fra-Cursor]]
- Issue #5 (Dokploy setup)
