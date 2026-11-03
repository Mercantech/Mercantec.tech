---
type: feature
status: plan
tags:
  - feature
  - quest
---

# Quests og planner

## Problem

Undervisere skal kunne planlægge obligatoriske og frivillige opgaver på et forløb; elever skal se hvad der skal laves og hvornår.

## Forslag

- **Quest planner** (underviser): placér quests på et forløb (fx delafleveringer)
- **Quest-typer:** Main (obligatorisk), Side og Daily (frivillige)
- **Elev-view (v1):** mine quests + deadlines (udbygges over tid)
- Quest kan kræve **aflevering** (link, video, fil m.m.) før godkendelse/XP
- XP fra alle quest-typer → hold-/forløb **og** karriere (se [[Levels-XP-Badges]])

## Acceptkriterier

- [ ] Underviser kan oprette/placere Main/Side/Daily på forløb
- [ ] Elev ser egne quests og deadlines
- [ ] Quest kan have afleveringskrav
- [ ] Godkendt quest giver XP efter reglerne

## Afhængigheder

- [[Hold-og-Lectio]] / forløb
- [[Afleveringer]]
- [[Levels-XP-Badges]]

## Risks

- For kompleks planner i v1

## Out of scope

- Fuldt elev-quest-UX (kommer iterativt)

## Relateret

- [[Feature-oversigt]]
- [[Hold-dashboard]]
- [[Domæne-model]]

## Opfølgning fra LUP-designet

Se [[Forloebsvisning-og-kalender]] og [[Aktiviteter-og-status]] for ugekalender, formular/træk-og-slip og samlet elev-UX. Det er opfølgninger efter v1-listen i issue #11, ikke ændrede kriterier for at lukke den.

- Udført, afleveret og godkendt er tre forskellige tilstande. En egen Main-markering erstatter ikke afleveringskravet.
- Daily har datoafgrænset status; en kort registrering kan være nok uden en ekstra rapport.
- XP og guld udløses af godkendelse og tildeles ikke dobbelt.
- Åbne afstemninger og quests samles i Aktiviteter; personlige funktioner følger aktivt medlemskab.
