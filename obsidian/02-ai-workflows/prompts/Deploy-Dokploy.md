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

Når vi skal oprette, opdatere eller fejlsøge deploy på Dokploy.

## Prompt

```
Brug Dokploy MCP (ikke ad hoc curl).
Følg alias-mønsteret på dokploy-network (unikke *-api / *-db aliases).
Tjek logs via MCP ved fejl (subnet-pool, inotify, nginx upstream, localhost-DB).
Læs obsidian Stack-og-hosting og Agent-regler først.
Opgave: <BESKRIV>.
```

## Output

Kort status + hvad der blev deployet/fejlet; ingen secrets i chat-output.
