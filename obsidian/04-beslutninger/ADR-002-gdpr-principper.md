---
type: adr
status: accepted
tags:
  - adr
  - gdpr
  - sikkerhed
---

# ADR-002: GDPR-principper for Mercantec.tech

## Status

accepted

## Kontekst

Platformen behandler elev- og underviserdata. Auth ligger centralt hos Mercantec Auth; Lectio er ekstern. Vi skal have klare principper før domænedata vokser.

## Beslutning

1. **Brugernøgle** er Auth `sub` (GUID) — aldrig e-mail som primær nøgle.
2. **Dataminimering** — lagre kun det, der er nødvendigt for forløb, hold, quests og afleveringer.
3. **Adgangskontrol** — JWT (iss/aud/JWKS); elev ser egne data; underviser ser holdets data; peer-profil er ikke default.
4. **Indsigt** — autentificeret eksport af egne platform-data (`GET /api/privacy/export`).
5. **Sletning** — anmodning via app (`POST /api/privacy/deletion-request`); Auth-konto og Lectio håndteres uden for denne app, indtil der findes aftalt automatik.
6. **Tokens/secrets** eksporteres aldrig.
7. **Offentlig information** på `/privatliv`; selvbetjening på `/rettigheder`.

## Konsekvenser

- Feature-noter (afleveringer, badges, Lectio) skal pege på GDPR-ansvar.
- Domæne-skeleton (#4) skal kunne udvide eksport/sletning med Membership m.m.
- Juridisk godkendt samtykketekst og DPO-workflow er senere arbejde.

## Relateret

- [[GDPR-og-rettigheder]]
- [[Ansvarsområder]]
- [[Auth-Mercantec]]
- Issue [#18](https://github.com/Mercantech/Mercantec.tech/issues/18)
