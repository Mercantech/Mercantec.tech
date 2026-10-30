---
type: workflow
status: active
tags:
  - cursor
  - ai
---

# Cursor-arbejdsgang

Sådan arbejder vi med Cursor på Mercantec.tech.

## Før kode

1. Læs [[Start her]] og relevant kontekst ([[Platform-vision]], [[Stack-og-hosting]]).
2. Arbejde styres via [[GitHub-Issues-og-Projects]] — Issue + [Project #9](https://github.com/orgs/Mercantech/projects/9/views/1?system_template=kanban).
3. Brug **Plan mode** til større ændringer — godkend planen før implementering.
4. Tjek [[Prompt-bibliotek]] for genbrugelige prompts.
5. Respektér [[Agent-regler]].

## Under implementering

- Små, fokuserede ændringer; match eksisterende stil når der er kode.
- Ingen secrets i repo; env via Dokploy.
- Svar og note-indhold på **dansk**, medmindre andet er aftalt.
- Hold Issue/board opdateret når status ændrer sig.

## Efter implementering

- Luk eller opdater Issue; flyt kortet på Project #9.
- Commit/push kun når vi eksplicit beder om det (eller når husregler for det konkrete repo siger andet).
- Opdater vault-noter hvis workflow eller beslutning ændrede sig ([[_index|Beslutninger]], ny [[ADR]] ved behov).

## Relateret

- [[GitHub-Issues-og-Projects]]
- [[Agent-regler]]
- [[Prompt-bibliotek]]
- Skabelon: [[Workflow]]
