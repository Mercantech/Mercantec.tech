---
type: kontekst
status: active
tags:
  - domæne
  - ordliste
---

# Domæne-model

Fælles ordliste for Mercantec.tech — aftalt i feature-afklaringen. Feature-katalog: [[Feature-oversigt]].

## Begreber

| Begreb | Betydning |
|--------|-----------|
| **Modul / blok** | Ét fagligt indslag. Kan indgå i et forløb **eller** tages frivilligt uden hold → XP til karriere. |
| **Forløb** | Faglig ramme (fx H3, Game Design, valgfag). Indeholder moduler og quests. |
| **Hold** | Kun elevliste. **1:1 med forløb** (ét forløb = ét hold). Elev kan være på flere hold. |
| **Optag** | Primært fra Lectio; manuel tilføjelse i platformen som supplement. |
| **Underviser / elev** | To forskellige views. Rolle fra Mercantec Auth og/eller sat i platformen. |
| **Hold-/forløbs-XP** | XP tjent inden for et konkret forløb/hold. |
| **Karriere-XP** | Samlet XP (hold + frivillige moduler + quests m.m.). Alt quest-XP tæller med. |
| **Main quest** | Obligatorisk på forløbet (fx delafleveringer). |
| **Side / Daily quest** | Frivillige; ekstra XP (tæller også til karriere). |
| **Aflevering** | Elevens private indsendelse som svar på en quests krav, med status og underviserfeedback (link, tekst, video, fil m.m.). |
| **Projekt** | Dedikeret showcase (solo/gruppe) — **adskilt** fra quest-aflevering. |
| **Logbog** | Elevens løbende refleksion; synlig for underviser. |

## Relationer

```mermaid
flowchart TB
  forloeb[Forløb]
  hold[Hold_elevliste]
  elev[Elev]
  modul[Modul]
  quest[Quest]
  forloeb ---|"1:1"| hold
  hold -->|"mange elever"| elev
  elev -->|"flere hold"| hold
  forloeb --> modul
  forloeb --> quest
  modul -->|"kan tages uden hold"| elev
```

## XP-lag

```mermaid
flowchart LR
  holdXp[Hold_forløbs_XP]
  frivillig[Frivilligt_modul]
  questXp[Quest_XP_alle_typer]
  karriere[Karriere_XP]
  holdXp --> karriere
  frivillig --> karriere
  questXp --> holdXp
  questXp --> karriere
```

## Relateret

- [[Platform-vision]]
- [[Feature-oversigt]]
- [[Ansvarsområder]] — Lectio under Integrationer
- [[Relaterede-projekter]] — Lectio-Collector

## Begreber fra LUP-designet

- **LUP**: Lokal UndervisningsPlatform; produktnavn, ikke et nyt repositorie eller domæne.
- **Guld**: virtuel saldo til kosmetiske køb; adskilt fra XP og level.
- **Afstemning**: undervisningsrunde knyttet til et forløb/hold.
- **Aktivitetsoverblik**: egne afstemninger og Main/Side/Daily med separat udført-, afleverings- og godkendelsesstatus.
- **Fag/emne i UI**: gruppering af fagligt indhold; endnu ikke en beslutning om nye databaseentiteter.

Se [[LUP-design-og-leveranceplan]]. Forløb og hold forbliver 1:1.
