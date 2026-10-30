---
type: prompt
status: active
tags:
  - prompt
  - review
---

# Kode-review

## Hvornår

Efter en PR eller et sæt lokale ændringer, før merge/deploy.

## Prompt

```
Review de aktuelle ændringer i Mercantec.tech.
Tjek: Mercantec Auth (ingen egen JWT), ingen secrets, Docker/env-klarhed, Agent-regler i obsidian.
Giv konkrete findings med filstier. Svar på dansk. Ingen omskrivning medmindre jeg beder om fix.
```

## Output

Liste af findings (kritisk / forslag) — ikke en fuld omskrivning.
