---
type: kontekst
status: draft
tags:
  - badges
  - progression
---

# Badge-sammenhæng

Hvordan badges relaterer til hinanden og til XP, quests og forløb.

## Lag

```mermaid
flowchart TB
  quest[Quest_completion]
  holdXp[Hold_forløbs_XP]
  karriere[Karriere_XP]
  emne[Emne_badges_lille_til_ultra]
  badgeF[Forløbs_badges]
  badgeK[Karriere_badges]
  vaeg[Certifikat_badge_væg]
  quest --> holdXp
  quest --> karriere
  holdXp --> badgeF
  karriere --> badgeK
  emne --> vaeg
  badgeF --> vaeg
  badgeK --> vaeg
```

## Emne-badges (primær stige)

Se [[Emne-badges]]. Custom badge pr. emne; fem niveauer:

**Lille** (forklare) → **Mellem** (klasseprojekt) → **Stor** (eget projekt til lærer) → **Mega** (fremlæg for klassen) → **Ultra mega** (undervise om emnet).

```mermaid
flowchart LR
  lille[Lille]
  mellem[Mellem]
  stor[Stor]
  mega[Mega]
  ultra[Ultra_mega]
  lille --> mellem --> stor --> mega --> ultra
```

## Regler

1. Badges kan knyttes til **emne-niveauer**, **hold-/forløbs-XP**, **karriere-XP**, **quests**, eller særlige handlinger.
2. Emne-niveauer forudsætter typisk forrige niveau **samme emne**.
3. Visning: legende karakter + **professionel væg** ([[Karakter-og-certifikatvæg]]).
4. Tildeling af emne-badges: typisk underviser efter demonstration/fremlæggelse.

## Kultur-badges (ingen stige)

Frivillige / stemnings-badges uden forudsætningskæde — typisk underviser-tildeling:

- [[Hawaii-skjorte]] — hawaii-skjorte
- [[Fancy-Friday]] — Fancy Friday

## Andre forudsætnings-træer

Udfyld når der kommer flere ikke-emne-badges med kæder.

## Kobling til features

| Fra | Til badges |
|-----|------------|
| [[Emne-badges]] | Emne-stige lille→ultra mega |
| [[Levels-XP-Badges]] | Tildelingsregler, levels |
| [[Quests-og-planner]] | Quest kan pege på emne-niveau |
| [[Projekter-Steam-galleri]] | Evt. dokumentation til Stor/Mega |
| [[Logbog]] | Evt. refleksion før Lille |

## Relateret

- [[Badge-oversigt]]
- [[Emne-badges]]
- [[Levels-XP-Badges]]
