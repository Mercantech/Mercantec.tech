---
type: feature
status: plan
tags:
  - feature
  - projekt
  - galleri
---

# Projekter og Steam-galleri

## Problem

Elever (og grupper) skal kunne fremvise tidligere og aktuelle produkter med rige links — adskilt fra skoleafleveringer.

## Forslag

- **Projekt** = dedikeret upload (solo eller gruppe): GitHub, hjemmeside, Unity/spil, video, billeder
- **Galleri** (“Steam-like”) med filter; **åbent først** (synligt på platformen)
- Tidligere forløbsprojekter kan knyttes til elev/gruppe over tid

## Acceptkriterier

- [ ] Opret projekt solo eller som gruppe
- [ ] Understøt links/media (GitHub, site, spil, video, billeder)
- [ ] Galleri med filter (fx forløb, tags)
- [ ] Default synlighed: åben på platformen

## Afhængigheder

- [[Social-portfolio]]
- [[Hold-og-Lectio]] (valgfri kobling til forløb)
- Design (KASC)

## Risks

- Moderering af offentligt indhold
- GDPR for elevproduktion

## Out of scope

- Automatisk konvertering af quest-aflevering til projekt

## Relateret

- [[Feature-oversigt]]
- [[Afleveringer]]
- [[Domæne-model]]

## LUP-design: mine projekter og fælles galleri

UI'et skelner tydeligt mellem egne projekter og projekter, ejerne har delt. Private afleveringer bliver aldrig automatisk del af galleriet.

**Afklaring:** den oprindelige plan ovenfor siger åben som standard; designreferencen starter privat og lader ejeren vælge deling. Den endelige standard for publicering afklares fælles før backendimplementeringen. Søgning skal respektere samme synlighed som galleriet. Se [[LUP-design-og-leveranceplan]].
