---
type: feature
status: plan
tags:
  - feature
  - xp
  - gamification
---

# Levels, XP og badges

## Problem

Elever skal kunne se progression både på det aktuelle forløb og over hele deres “karriere” på platformen.

## Forslag

To XP-lag:

1. **Hold-/forløbs-XP** — inden for konkret forløb/hold
2. **Karriere-XP** — samlet (hold + frivillige moduler + quests)

Levels og badges knyttes til progression (detaljer om thresholds senere). **Alt quest-XP** (Main, Side, Daily) tæller til karriere-XP.

Badge-katalog, sammenhæng og design: [[Badge-oversigt]] · [[Sammenhæng]] · `06-badges/`.

## Acceptkriterier

- [ ] Elev kan se hold-/forløbs-XP for aktive hold
- [ ] Elev kan se karriere-XP
- [ ] Quest-completion giver XP til relevant hold **og** karriere
- [ ] Frivilligt modul uden hold giver karriere-XP
- [ ] Badges kan tildeles/vises (regler kan udbygges)
- [ ] Hvert badge viser **% af brugere** der har det (se [[Badge-statistik]])

## Afhængigheder

- [[Quests-og-planner]]
- [[Karakter-og-certifikatvæg]]
- [[Domæne-model]]

## Risks

- Inflation af XP; uklare level-kurver

## Out of scope

- Betalt / eksternt XP-system

## Relateret

- [[Feature-oversigt]]
- [[Domæne-model]]
- [[Badge-oversigt]]
- [[Badge-statistik]]
- [[Karakter-og-certifikatvæg]]

## LUP-design: guld, fem niveauer og belønninger

[[Guld-butik-og-oplaasninger]] specificerer virtuelt guld, entydige belønninger og køb. Karriere-level og emnebadgets niveau er forskellige. Lille, Mellem, Stor, Mega og Ultra mega følger fortsat [[Emne-badges]]. UI'et kan samle dem under samme emne uden at ændre aftalen om separate badge-ID'er pr. niveau.

Visningen viser ejet/mangler og krav pr. niveau. Hawaii/Fancy Friday er særlige kriterier og kan åbne kosmetiske køb. De viste levelkurver, XP-beløb og priser er afklaringsgrundlag, ikke fastlagte produktionsværdier. Eksisterende krav om badge-statistik bevares.
