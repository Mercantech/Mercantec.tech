---
type: feature
status: plan
tags: [feature, navigation, search, design]
---

# LUP-navigation og søgning

## Retning

**LUP – Lokal UndervisningsPlatform** har én kompakt header. Overblik og Aktiviteter er direkte indgange; Læring, Udforsk og Mit univers samler relaterede funktioner. Underviseren har en tydelig indgang til afstemninger. På mindre skærme foldes navigationen ind i Menu uden en ekstra global menurække.

Profilmenuen samler tema, brugerprofil og valg af aktiv rolle. Et aktivt forløb er en UI-kontekst blandt autoriserede medlemskaber; det giver ikke i sig selv adgang. Funktioner til afstemninger, egne quests og logbog skjules, når de ikke er relevante. Materialer, egne projekter og univers kan bruges mellem forløb.

System, lyst, mørkt og rainbow kan vælges frit. En fast sti og forløbsmenu gør det muligt at vende tilbage fra emne, quest og aflevering.

## Global søgning

Søg fra hele platformen i forløb, fag/emner, quests, badges og projekter. Resultater viser type og faglig kontekst og linker til en rigtig indholdsside. Man kan afgrænse efter kategori. Tomme resultater er forskellige fra en API-fejl.

## Acceptkriterier

- [ ] Headeren forbliver på én række ved desktop- og mobilbredder; alle funktioner er tastaturtilgængelige.
- [ ] Navigation og handlinger tilpasses rolle, medlemskab og aktiv forløbskontekst.
- [ ] Temavalg huskes; grundtemaer kræver ikke guld, badges eller level.
- [ ] En synlig kontekststi følger forløb → fag/emne → quest → aflevering.
- [ ] Søgning virker på tværs af de relevante indholdstyper og giver stabile direkte links.
- [ ] Private projekter, afleveringer og elevdata lækker ikke gennem søgeresultater eller uddrag.
- [ ] Ingen dobbeltmenuer eller gentagne afstemningsbannere.

## Afhængigheder

[[Underviser-og-elev-views]] · [[Hold-og-Lectio]] · [[Aktiviteter-og-status]] · [[LUP-design-og-leveranceplan]]

LUP-shell og global søgning er separate implementeringsopgaver; der introduceres ikke en ny frontendstack.

## GitHub-opfølgning

- [#27 – LUP-shell: kompakt navigation, temaer og forløbskontekst](https://github.com/Mercantech/Mercantec.tech/issues/27)
- [#28 – Global søgning i forløb, emner, quests, badges og projekter](https://github.com/Mercantech/Mercantec.tech/issues/28)
