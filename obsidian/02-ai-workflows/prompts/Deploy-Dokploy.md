---
type: prompt
status: active
tags:
  - prompt
  - deploy
  - dokploy
---

# Deploy-Dokploy

## Hvornår

Oprette, opdatere eller fejlsøge deploy på Dokploy for Mercantec.tech.

## Prompt

```
Brug Dokploy MCP (ikke ad hoc curl).
Læs obsidian/02-ai-workflows/Dokploy-drift.md og Stack-og-hosting.
Prod-domæne: mercantec.tech (APEX — intet subdomæne). Service web, port 3000.
Aliases: mercantec-tech-api, mercantec-tech-db. Nginx resolver + proxy til mercantec-tech-api:8080.
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false. Connection string Host=mercantec-tech-db.
Cors:SpaOrigins skal inkludere https://mercantec.tech.
Ved fejl: MCP logs (subnet-pool, inotify, nginx DNS, localhost-DB).
Opgave: <BESKRIV>.
```

## Output

Kort status + hvad der blev deployet/fejlet; ingen secrets i chat.
