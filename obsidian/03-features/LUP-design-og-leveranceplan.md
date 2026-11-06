---
type: feature
status: plan
tags: [feature, design, lup]
---

# LUP-design og leveranceplan

Opdateret 3. november 2026 efter Kaspers designgennemgang. Produktnavnet er **LUP – Lokal UndervisningsPlatform**. Repository, domæne, Auth-klient og .NET-projektnavne ændres ikke af navnevalget.

## Leveret til designgennemgang

[Den klikbare visning](../../design/lup/README.md) ligger i `design/lup/` med elev-/underviservisninger, lokale interaktioner og genererede forløbs-/projektbilleder. Den kan køres med Python eller Docker uden .NET, Auth og database.

Det er en **designreference**, ikke en markering af, at backendfunktionerne er implementeret. Ingen eksisterende issues lukkes på baggrund af visningen. Blazor WASM PWA, ASP.NET Core controllers, Postgres og Mercantec Auth er fortsat produktionsstacken.

## Blazor-opfølgning · 4. november 2026

Designet er nu flyttet ind i Blazor-komponenter. [[Blazor-designgennemgang]] beskriver afvigelser, rettelser, de nye temaer og resterende backendarbejde. Den statiske visning i design/lup bevares som den oprindelige reference.

## Julegaver og sne · 4. november 2026

[[Julegaver-og-sne]] tilføjer rigtig levering af gratis julehilsner via API/Postgres med modtagervalg, indbakke, uåbnet-tæller og åbnet-status. Begge brugere skal være registreret ved besøg i samme installation. Gaver overfører ikke butiksting eller guld. Sneen har system/aktiv/stille og en Start-knap, der kan tilsidesætte reduceret bevægelse for sneen. Produktionsdeployment kontrolleres særskilt; øvrige backendleverancer nedenfor er ikke ændret.

## Fælles afstemninger og gaveløft · 5. november 2026

[[Afstemninger-og-stemning]] er nu implementeret med fælles API/Postgres, fire svartyper, kladder, start/luk/genåbn, nye runder, adgangsfiltrerede svar/resultater og tavlevisning. De tidligere lokale afstemningsindgange er erstattet; quests og økonomi er fortsat separate leverancer. Opdatering på tværs af klienter sker hvert 6. sekund. Fritekst er kun for underviseren; videre moderation og redigering af gemte kladder er opfølgninger. Deployment og fælles accepttest af #31 kontrolleres særskilt.

[[Julegaver-og-sne]] beskriver elevens adgang til registrerede undervisere uden holdkobling, opdateret modtagervælger og den nye gaveceremoni. Der er ikke tildelt nye Auth-roller eller overført butiksting/guld.

## Omfang og opfølgning

| Område | Beslutning / ønsket adfærd | Plan |
| --- | --- | --- |
| LUP-shell | Én header; foldbare kategorier; profilmenu til tema og rolle; fast kontekststi | [[LUP-navigation-og-soegning]] |
| Søgning | Forløb, fag/emner, quests, badges og projekter; adgangsfiltrerede resultater | [[LUP-navigation-og-soegning]] |
| Forløb | Sammenhængende dokument som Notion med uger, dage, materialer og egne sider | [[Forloebsvisning-og-kalender]] |
| Kalender | Ugevisning, aktivitetstidspunkter og questplanlægning; formular og træk-og-slip | [[Forloebsvisning-og-kalender]] · [[Quests-og-planner]] |
| Fremmødeform | Grøn: fuldt fremmøde; blå: online; gul: arbejde hjemme; altid med tekst | [[Forloebsvisning-og-kalender]] |
| Afstemninger | Stemning, valg, skala 1–5, korte svar; start/luk, resultater og tavlevisning | [[Afstemninger-og-stemning]] |
| Aktiviteter | Ét sted til åbne afstemninger og Main/Side/Daily; udført ≠ afleveret ≠ godkendt | [[Aktiviteter-og-status]] |
| Status | Rød: vigtigt; orange: kan gøre; grøn: ingen handling mangler. Ingen gentagne afstemningsbannere | [[Aktiviteter-og-status]] · [[Notifikationer]] |
| Progression | Forløbs-XP, karriere-XP, level og guld; belønning kun én gang | [[Levels-XP-Badges]] · [[Guld-butik-og-oplaasninger]] |
| Badges | Lille → Mellem → Stor → Mega → Ultra mega; ejet/mangler og krav pr. niveau | [[Emne-badges]] · [[Levels-XP-Badges]] |
| Avatar | Fælles 2D-udtryk, garderobe og genbrug i et lille prøvespil | [[Karakter-og-certifikatvæg]] |
| Butik | Møbler og avatartøj; level-/badgekrav, køb med guld og synligt ejerskab | [[Guld-butik-og-oplaasninger]] |
| Lejlighed | Eget 8×6-rum; placér, flyt, fjern og behold inventar | [[Lejlighed-hus-moebler]] |
| Projekter | Mine projekter og fælles galleri; deling er adskilt fra private afleveringer | [[Projekter-Steam-galleri]] · [[Afleveringer]] |

## Rækkefølge frem for flere v1-krav

Eksisterende Sprint 1/2- og NTH-issues beholder deres afgrænsede v1-acceptkriterier. Kalender, fuld dokumentvisning, aktivitetsoversigt, økonomi og butik føres som opfølgninger; fx bliver træk-og-slip ikke et nyt krav for at lukke questlistens v1-issue #11.

Forslag til afhængigheder:

1. Forløb/medlemskab og quest-/afleveringskontrakter.
2. LUP-shell, dokumentvisning, aktivitetsoversigt og afstemninger.
3. Visuel kalender og global søgning, når indholdet findes.
4. XP-/guldtransaktioner før butik og garderobekøb. Bolig/unlocks forbliver NTH.

GitHub Issues og Project #9 er fortsat sandheden for prioritering og status. Nye backlogopgaver betyder ikke, at de er lovet til et igangværende sprint. Se [[Sprints]].

## Afklaringer før implementering

- **Fag/emne:** designet grupperer moduler under fag. Det er ikke en stiltiende ændring af den aftalte forløb–hold-relation på 1:1 eller et krav om nye databaseentiteter.
- **Badge-ID:** UI kan samle fem niveauer under samme emne, selv om [[Emne-badges]] har separate badge-ID'er pr. niveau. Designets lokale `rank` er ikke et databasevalg.
- **Offentliggørelse:** den gamle projektplan siger åben som standard. Skabelonen starter private projekter og kræver et aktivt delingsvalg. Den endelige standard afklares fælles, før publicering implementeres.
- **Økonomi:** 200 XP pr. level, 100 XP/20 guld for en standardquest og de viste priser er eksempelværdier til afklaring, ikke fastlagte backendkrav.
- **Levels og tema:** system/lys/mørk/jul/nytår/hacker er frit tilgængelige grundtemaer. Level-unlocks i #25 vedrører yderligere kosmetik/profiltemaer.
- **Gratis gave eller køb:** #26 giver et møbel direkte. Den nye butik bruger adgang til køb. Begge tildelingstyper skal være entydige, så et unlock ikke både uddeler og sælger samme eksemplar.
- **Sikkerhed:** rolle-/forløbsskift i designvisningen erstatter ikke Auth og medlemskabskontrol. Køb, belønninger, datoafgrænsning af Daily og synlighed skal valideres af API'et.
- **Indhold:** H3-/Game Design-planerne er inspireret af de delte Notion-forløb. De viste H3-Side/Daily-quests og kosmetiske belønninger er nye designforslag, ikke formelle målpinde.

## Elevforsiden · 5. november 2026

Det oprindelige layout er genskabt i Blazor med aktivt forløb, genveje og en sidekolonne til XP, guld, avatar og badges. Lectio/VUF og Mine hold indgår som kompakte paneler, og den eksisterende forløbstidslinje kan foldes ud. De nye datakilder er bevaret. Guld og avatar følger den eksisterende fælles lokale tilstand; serverbaseret økonomi er fortsat en separat leverance. Se [[Blazor-designgennemgang]].

## UI-retning

Ingen prototype- eller demobannere i produktfladen. Dokumentationen skelner stadig mellem designreference og implementeret produkt. Forklarende status vises med tekst og farve, og aktuel forløbskontekst bevares gennem fag, emne, quest og aflevering.

## GitHub-opgaver

| Issue | Opfølgning | Placering |
| --- | --- | --- |
| [#27](https://github.com/Mercantech/Mercantec.tech/issues/27) | LUP-shell: kompakt navigation, temaer og forløbskontekst | Ny backlog · åben |
| [#28](https://github.com/Mercantech/Mercantec.tech/issues/28) | Global søgning i forløb, emner, quests, badges og projekter | Ny backlog · åben |
| [#29](https://github.com/Mercantech/Mercantec.tech/issues/29) | Forløbsvisning som dokument med uger, dage og faglige sider | Ny backlog · åben |
| [#30](https://github.com/Mercantech/Mercantec.tech/issues/30) | Ugekalender og visuel questplanlægning med fremmødeform | Ny backlog · åben |
| [#31](https://github.com/Mercantech/Mercantec.tech/issues/31) | Afstemninger og stemningstjek med elevdeltagelse og tavlevisning | Ny backlog · åben |
| [#32](https://github.com/Mercantech/Mercantec.tech/issues/32) | Samlet aktivitetsoverblik, Daily-registrering og rød/orange/grøn status | Ny backlog · åben |
| [#33](https://github.com/Mercantech/Mercantec.tech/issues/33) | XP, karriere-level og guld ved entydig questgodkendelse | Ny backlog · åben |
| [#34](https://github.com/Mercantech/Mercantec.tech/issues/34) | NTH: Butik med guld, avatartøj, møbler og badgekrav | Sprint NTH · åben |

V1-issues #8, #11, #12 og NTH #24–#26 har fået referencer til de relevante opfølgninger; deres status og oprindelige acceptkriterier er bevaret.

Project #9-tilknytning/status for de nye issues er ikke verificeret: det tilgængelige GitHub-login mangler Projects-scopes. Eksisterende boardstatus er ikke ændret af denne opdatering.


## Offentlig forside · 6. november 2026

Den nye Blazor-forside giver gæster adgang til undervisningsplaner, fag/emner, udvalgte DEMO-projektpræsentationer og badgekrav. Personlige sider og holdindhold kræver fortsat login. Offentlig søgning bruger et særskilt katalog uden elevdata. Publicering til et hold udvider ikke adgangen til internettet. Se [[Offentlig-forside]].
