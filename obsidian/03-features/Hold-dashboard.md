---
type: feature
status: implementeret
tags:
  - feature
  - dashboard
  - underviser
---

# Hold-dashboard

## Implementeret 5. november 2026

Holdets kommandocentral åbnes fra `/courses/{forloebId}`; `/holds/{holdId}` fører til samme hold. Overblikket samler forløbseditor, kommende begivenheder og frister, seneste indhold samt afleveringer, der venter på feedback. Fanerne åbner holdets forløb, quests, kalender, elever/grupper, materialer og holdindstillinger.

- Forløb skrives i den eksisterende editor på `/notion?hold={holdId}` med særskilte kladder og udgivelser.
- Main, daily og side quests kan oprettes, redigeres og udgives med afleveringsform og valgfri frist. De bruger de eksisterende afleveringer med filer, revisioner og feedback. Daily er en questtype; automatisk gentagelse og XP-regler er endnu ikke implementeret.
- Holdkalenderen viser uge, måned eller liste. Undervisere kan oprette, redigere og slette begivenheder. Questfrister medtages automatisk. Tidspunkter vises i dansk tid og gemmes i UTC.
- Manuelle grupper og grupperoulette bruger samme gemte grupper som gruppeafleveringer. Elever kan være i flere grupper. Roulette fordeler alle eller valgte elever efter størrelse eller antal, viser et forslag og gemmer hele runden samlet. Tidligere grupper bevares. Valgfri undgåelse af tidligere makkerpar bruger en afgrænset søgning og melder tydeligt, hvis den ikke finder et forslag.
- Materialer kan uploades, udgives/skjules og åbnes i den eksisterende filviser.

Data og rettigheder kontrolleres pr. hold i API'et. Elever får udgivet indhold og deres egne grupper; elevpreview skjuler administrative handlinger og kladder. Kommandocentralen bruger eksisterende holdmedlemskaber, inklusive elever fra Lectio uden tilknyttet konto.

Migreringen `HoldCommandCenter` tilføjer questtypen (eksisterende quests bliver `main`) og holdkalenderens begivenheder. Den køres ved normal API-start. API-testene dækker adgang, udgivelse, kalender, vurderingskø og atomisk gruppeoprettelse.

## Problem

Underviser mangler overblik over holdet: hvem mangler vurdering, quest-status m.m.

## Forslag

Dashboard pr. hold/forløb med fokus på **manglende vurdering** af quests/afleveringer.

**Kompetencebaseret** overblik = **nice-to-have** (efter quest-status).

## Acceptkriterier

- [x] Liste/oversigt over quests der venter på vurdering
- [x] Hurtig navigation til elev/aflevering
- [ ] (Senere) kompetencedækning — ikke v1-krav

## Afhængigheder

- [[Quests-og-planner]]
- [[Afleveringer]]
- [[Aktivitet-og-elevprofil]]
- [[Underviser-og-elev-views]]

## Risks

- For tungt dashboard før data-modellen er stabil

## Out of scope

- Fuldt kompetence-matrix UI i v1

## Relateret

- [[Feature-oversigt]]
- [[Domæne-model]]
