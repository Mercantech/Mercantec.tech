---
type: feature
status: plan
tags: [feature, afstemning, stemning]
---

# Afstemninger og stemning

## Problem og retning

Underviseren skal kunne tage klassens stemning og stille andre hurtige spørgsmål i undervisningen, inspireret af AhaSlides. LUP skal ikke være afhængig af en AhaSlides-konto eller en ekstern integration for dette flow.

Fire svartyper: stemningstjek med fem følelser, valg mellem muligheder, skala 1–5 og korte fritekstsvar. Underviseren kan gemme kladde, starte, lukke og genåbne samt vælge, hvornår elever må se resultater. Tavlevisningen viser spørgsmålet og de samlede svar.

## Acceptkriterier

- [ ] Afstemningen tilhører et forløb/hold, og kun dets undervisere kan styre den.
- [ ] Åbne afstemninger vises samlet under elevens Aktiviteter.
- [ ] En elev kan afgive og ændre ét svar pr. runde, mens den er åben; ændringer øger ikke antallet af deltagere.
- [ ] Lukning afvises ikke kun i UI: API'et afviser nye svar på en lukket runde.
- [ ] En ny stemningsrunde er adskilt fra tidligere runder og nulstiller ikke historikken.
- [ ] Resultatvisning og frigivelse håndhæves også i API'et.
- [ ] Elevnavne vises ikke i resultaterne. Lagringsmodel, adgang og eventuel identitetskobling afklares før implementering.
- [ ] Underviserens tavle og elevernes visninger opdateres på tværs af forskellige klienter.
- [ ] Fritekst valideres, længdebegrænses og vises som tekst.

## Ikke leveret af designreferencen

Den lokale visning demonstrerer spørgsmål, svar og livscyklus i én browser. Flerbrugeropdateringer, persistens, Auth, servervalidering og moderering af fritekst skal implementeres i .NET/Blazor. Transporten for opdateringer afklares i implementeringsopgaven.

## Afhængigheder

[[Hold-og-Lectio]] · [[Underviser-og-elev-views]] · [[Aktiviteter-og-status]] · [[LUP-design-og-leveranceplan]]

## GitHub-opfølgning

- [#31 – Afstemninger og stemningstjek med elevdeltagelse og tavlevisning](https://github.com/Mercantech/Mercantec.tech/issues/31)
