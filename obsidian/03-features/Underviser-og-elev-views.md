---
type: feature
status: plan
tags:
  - feature
  - auth
  - ui
---

# Underviser- og elev-views

## Problem

Undervisere og elever har forskellige jobs på platformen og må ikke lande i samme startsoplevelse.

## Forslag

To adskilte sider/dashboards styret af rolle i **Mercantec Auth** og/eller rolle sat i **platformen**.

## Acceptkriterier

- [ ] Elev lander i elev-view efter login
- [ ] Underviser lander i underviser-view efter login
- [ ] Rolle kan komme fra Auth **eller** platform-konfiguration
- [ ] Navigation og primære handlinger matcher rollen

## Afhængigheder

- Auth (Mercantec Auth)
- [[Onboarding-modal]]
- [[Domæne-model]]

## Risks

- Bruger der er underviser på ét forløb og elev på et andet (afklar UX senere hvis aktuelt)

## Out of scope

- Fuld feature-paritet i begge views fra dag ét

## Relateret

- [[Feature-oversigt]]
- [[Domæne-model]]
- [[Platform-vision]]
