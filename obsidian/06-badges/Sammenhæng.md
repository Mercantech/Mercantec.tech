---
type: kontekst
status: draft
tags:
  - badges
  - progression
---

# Badge-sammenhæng

Hvordan badges relaterer til hinanden og til XP, quests og forløb. Opdater når I definerer konkrete badges.

## Lag

```mermaid
flowchart TB
  quest[Quest_completion]
  holdXp[Hold_forløbs_XP]
  karriere[Karriere_XP]
  badgeF[Forløbs_badges]
  badgeK[Karriere_badges]
  vaeg[Certifikat_badge_væg]
  quest --> holdXp
  quest --> karriere
  holdXp --> badgeF
  karriere --> badgeK
  badgeF --> vaeg
  badgeK --> vaeg
```

## Regler (aftalt / under udfyldning)

1. Badges kan knyttes til **hold-/forløbs-XP**, **karriere-XP**, konkrete **quests**, eller **særlige handlinger** (fx første projekt).
2. Nogle badges kan have **forudsætninger** (andre badges eller min. level) — tegn dem herunder når de findes.
3. Visning: legende karakter + **professionel væg** ([[Karakter-og-certifikatvæg]]).

## Forudsætnings-træ

Udfyld når kataloget har konkrete badges:

```mermaid
flowchart LR
  b1[Badge_A]
  b2[Badge_B]
  b3[Badge_C]
  b1 -->|"kræver"| b2
  b2 -->|"kræver"| b3
```

*(Erstat med rigtige badge-navne og wikilinks.)*

## Kobling til features

| Fra | Til badges |
|-----|------------|
| [[Levels-XP-Badges]] | Tildelingsregler, levels |
| [[Quests-og-planner]] | Quest-badges / milestones |
| [[Projekter-Steam-galleri]] | Evt. showcase-badges |
| [[Logbog]] | Evt. refleksions-badges |

## Relateret

- [[Badge-oversigt]]
- [[Levels-XP-Badges]]
