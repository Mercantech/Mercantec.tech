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
| **Prod** | **`mercantec.tech`** (apex — **intet** subdomæne) |
| Dev (senere) | aftales separat (branch + env) — ikke hardcod i kode |

Dokploy: domain på service **`web`**, port **3000**. For mercantec.tech bag Cloudflare: følg org-praksis (`certificateType` / https ift. tunnel).

## Netværk (påkrævet)

Hosten har opbrugte address pools → **ingen nye bridge-net**.

1. `dokploy-network` som compose `default` (`external: true`)
2. Aliases: **`mercantec-tech-api`**, **`mercantec-tech-db`**
3. Nginx: `resolver 127.0.0.11` + `set $upstream_api http://mercantec-tech-api:8080`
4. Connection string: `Host=mercantec-tech-db`

Proxyér **aldrig** til generisk `http://api:...` på det delte net.

## .NET API

- `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`
- `ASPNETCORE_URLS=http://+:8080`
- Connection string fra **env** (`ConnectionStrings__Default` / `CONNECTION_STRING`)

## MCP-flow

Brug **Dokploy MCP** (ikke ad hoc curl):

1. Compose peger på GitHub repo/branch + `composePath`
2. `compose-saveEnvironment` (`createEnvFile: true`)
3. `domain-create` → host **`mercantec.tech`**, service `web`, port 3000
4. `compose-deploy`
5. Fejl: `deployment-readLogs` / `compose-readLogs`

Typiske fejl: subnet-pool, inotify, localhost-DB, nginx upstream DNS.

## Relateret

- [[Stack-og-hosting]]
- [[Deploy-Dokploy]] (prompt)
- [[Husregler-fra-Cursor]]
- Issue #5 (Dokploy setup)
