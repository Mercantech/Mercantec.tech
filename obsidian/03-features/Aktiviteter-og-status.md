---
type: feature
status: plan
tags: [feature, aktivitet, status]
---

# Aktiviteter og status

## Samlet indgang

Aktiviteter samler elevens åbne afstemninger og Main-, Side- og Daily-quests i det aktive forløb. Der kan filtreres efter type og efter handlinger, som mangler. Afstemninger gentages ikke i bannere, genvejsbokse og flere opgavelister på forsiden.

## Tre forskellige tilstande

1. **Arbejde udført:** elevens egen markering.
2. **Afleveret/registreret:** elevens svar på et konkret questkrav.
3. **Godkendt:** underviserens vurdering, som kan udløse belønning.

En Main-markering erstatter ikke en krævet aflevering. Daily-markering gælder den enkelte undervisningsdag og kan være selve den korte registrering; der kræves ikke en ekstra rapport uden et udtrykkeligt questkrav.

## Handlingsstatus

| Farve | Elev | Underviser |
| --- | --- | --- |
| Rød – vigtigt | Ubesvaret åben afstemning eller Main-quest, der kræver elevens handling | Aflevering til vurdering |
| Orange – kan gøre | Valgfrie Side-/Daily-aktiviteter | Eksempelvis afstemningskladde, der kan startes |
| Grøn – intet mangler | Ingen aktuelle handlinger | Ingen aktuelle handlinger |

En elevaflevering, der afventer underviseren, er ikke længere elevens manglende handling. Afvisning/feedback med krav om rettelser skal igen kunne kræve handling. Farve suppleres altid med tekst og tilgængeligt navn.

## Acceptkriterier

- [ ] Ét samlet, rolle- og medlemskabsfiltreret aktivitetsoverblik.
- [ ] Egne udført-markeringer kan ændres, men en godkendt aktivitet ændres ikke uden en defineret vurderingsproces.
- [ ] Daily-status er knyttet til dato; en markering fra i går udfylder ikke i dag.
- [ ] API'et leverer et konsistent grundlag for både listen og headerens status; ingen uvedkommende elevdata.
- [ ] Rød har forrang over orange; grøn vises kun, når ingen relevant handling mangler.
- [ ] Statusmenuen viser et kort udsnit og linker til alle aktiviteter.
- [ ] Aktiv-forløbsfunktioner skjules eller viser en korrekt tomtilstand uden et aktivt medlemskab.

## Afhængigheder

[#11](https://github.com/Mercantech/Mercantec.tech/issues/11) · [#12](https://github.com/Mercantech/Mercantec.tech/issues/12) · [[Afstemninger-og-stemning]] · [[Notifikationer]] · [[Levels-XP-Badges]]

Dette er handlingsstatus; den er ikke det samme som antallet af ulæste historiske notifikationer.

## GitHub-opfølgning

- [#32 – Samlet aktivitetsoverblik, Daily-registrering og rød/orange/grøn status](https://github.com/Mercantech/Mercantec.tech/issues/32)
