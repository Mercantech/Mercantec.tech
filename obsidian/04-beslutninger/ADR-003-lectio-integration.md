---
type: adr
status: accepted
tags:
  - adr
  - lectio
  - integration
---

# ADR-003: Lectio som primær elevkilde + adgangskontrol

## Status

accepted

## Kontekst

Elevlister og studieforløb kommer fra [Lectio-Collector](https://github.com/Mercantech/Lectio-Collector) (`lc.mags.dk`). Mercantec.tech har egen Membership (manuel). GDPR kræver at elever kun ser egne data.

## Beslutning

1. **Lectio er primær** for elevliste/skoleforløb; **manuel Membership** i Mercantec.tech er supplement (`Source = manual | lectio`).
2. **Adgang** håndhæves i Lectio-Collector:
   - Underviser/Admin (Auth-roller): alle elever + sync/admin
   - Elev: kun egen Lectio-elev efter kobling Auth `sub` ↔ LectioId
3. **Mercantec.tech** kalder Lectio-API med brugerens Bearer-token (samme `aud: mercantec-apps`) via server-proxy `/api/lectio/*` — ingen cookie-deling.
4. **Konflikt:** Lectio-sync overskriver ikke manuelle Membership-rækker med `Source=manual`. Lectio-medlemskaber markeres `Source=lectio` (sync-job senere).
5. Fuld automatisk hold-sync er **ikke** påkrævet i Sprint 1 — kontrakt + scoped read + link-API er nok.

## Konsekvenser

- Lectio-Collector skal deployes med adgangsmiddleware før elev-login i prod.
- **Elev self-link:** `GET /api/me/lectio-candidates` matcher JWT-navn server-side; `PUT /api/me/lectio-link` tillader kun Lectio-id blandt egne kandidater (ingen fritekst-id, ingen elev-roster i browseren).
- **Underviser-fallback:** `PUT /api/me/lectio-link` med `authSub` (uden kandidat-krav) til elever uden navnematch.
- Elev-lister (`/api/people/students`, fulde programmeringselever) er teacher-only / scoped — elev ser kun sig selv efter kobling.
- Mercantec UI: `/lectio` bekræftelsesflow + blød CTA på elev-dashboard.

## Relateret

- [[Hold-og-Lectio]]
- [[GDPR-og-rettigheder]]
- [[Relaterede-projekter]]
- Issue [#10](https://github.com/Mercantech/Mercantec.tech/issues/10)
