---
type: feature
status: implemented
tags: [lup, design, offentlig]
---

# Offentlig forside

Forsiden følger det godkendte [designforslag](../../design/public-home-reference.png): kompakt introduktion, forløbsvælger, søgning, projekter, fag og offentlige ressourceindgange. Farver og typografi genbruger LUP; den mørkeblå gæsteheader og indholdet tilpasser sig skærmbredden. Eksisterende temaer og loginflow bevares.

## Offentligt indhold

- Forløbsvælgeren åbner de eksisterende undervisningsplaner for H1, H2, H3 og Game Design. Alle H1–H6 og valgfag kan åbnes fra plankataloget.
- Fag og emner har egne sider under /subjects. De bruger generelle undervisningsbeskrivelser og de eksisterende fagtekster, uden holddatoer, deltagerlister eller personlige markeringer.
- Projektpræsentationer under /explore/projects har egne detaljesider. De tre udvalgte undervisningseksempler er markeret **DEMO**, som aftalt. Ingen elevnavne, kommentarer, reaktioner eller private projektfelter medtages.
- Badgekatalog og krav pr. niveau er tilgængelige uden login. Personlig badgeoptjening er ikke en del af den offentlige model.
- Søgning uden login medtager kun de offentlige indgange. Quests tilføjes fortsat for brugere med login.

## Adgang

PublicPageAccess afgrænser gæster til de offentlige læseruter. Routeren monterer ikke de personlige sidekomponenter uden login. Profil, Lectio, hold, Notion, kalender, afstemninger, afleveringer, egne projekter, XP/guld og avatar kræver fortsat login. API-controllerne og deres autorisation er uændrede; offentlig UI-adgang giver ikke offentlig API-adgang til holddata.

Udgivelse til et hold er ikke internetoffentliggørelse. Projektkataloget er en særskilt, eksplicit kurateret liste. Elevers egne projekter publiceres ikke automatisk. Fremtidig offentlig deling af rigtige elevprojekter kræver sit eget publiceringsvalg, datakontrakt og adgangskontrol; dette arbejde indfører ikke det workflow.

## Implementering

- web/Public/PublicCatalog.cs: offentlige indholdsmodeller, udvalgte projekteksempler og søgeindeks.
- web/Pages/Home.razor: den nye forside og forløbsvælger. Et valg ændrer ikke elevens aktive hold/forløb.
- web/Pages/Public/Content.razor: fag, emner og projektpræsentationer.
- web/Components/PublicBadgeCatalog.razor og PublicBadgeRequirements.razor: badgekrav uden personlige niveauer.
- web/Auth/PublicPageAccess.cs og App.razor: gæsteadgang før sidekomponenter indlæses.

Arbejdet er udført i en separat worktree for at holde eksisterende, igangværende ændringer i den fælles arbejdsmappe urørte.

## Kontrol

Se PublicContentTests for læseruter, beskyttede sider, offentlige søgeresultater, DEMO-projekter og badgekontrakten. Alle 97 webtests består. Debug-build har 0 fejl og 0 advarsler, og Release-publish er gennemført. SDK’ets anbefaling af den valgfrie wasm-tools-workload er uændret.

I browseren er forløbsvælger, H2-plan med teori, C#-fag, IoT/MQTT-emner, projektoversigt/detalje, fem badgekrav, Hawaii-oplåsning, søgning og direkte adgang til en beskyttet profilside gennemgået uden login. Mobilmenu og bredder 320/390 px samt lyst/mørkt tema er kontrolleret uden konstateret vandret overløb. Ingen registrerede JavaScript-fejl. API-kald uden login til medlemskaber, Lectio og afstemninger returnerer fortsat 401.

Der er ikke skrevet til rigtige hold, projekter, elever eller afleveringer under kontrollen.
