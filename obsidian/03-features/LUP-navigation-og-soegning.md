---
type: feature
status: active
tags: [feature, navigation, search, design]
---

# LUP-navigation og søgning

## Retning

**LUP – Lokal UndervisningsPlatform** har én kompakt header. Logoet åbner forsiden før login og det personlige overblik efter login. De fælles indgange er **Mine hold** og **Oversigt**; undervisere har desuden **Min kalender** på tværs af hold. Søgning, én notifikationsindgang og profil ligger til højre. Mobilnavigationen foldes ind i Menu.

Profilmenuen samler Min profil, Mit univers, Indstillinger og Log ud. Underviserværktøjer vises kun til undervisere. Indstillinger samler tema og kontotilknytning. Oversigten samler undervisningsplaner, fag, offentlige DEMO-projekter og badgekrav; eksempelforløb er en særskilt foldet sektion for loggede ind. Mit univers samler progression, badges, egne projekter, avatar, lejlighed, butik og spil.

På **Underviserværktøjer** er genvejene grupperet under **Undervisning**, **Elever og opfølgning** og **Opsætning**. Vælg et hold under **Holdværktøjer** for at gå direkte til dets forløb, quests, afleveringer til vurdering, kalender, grupper, materialer, afstemninger og indstillinger. Samme område har genveje til grupperoulette, sidegraf, Mine hold og oprettelse fra Lectio. Holdvælgeren viser de hold, underviseren kan administrere.

## Holdets navigation

Holdcentralen har fem hovedfaner: **Overblik, Undervisning, Opgaver, Kalender og Klassen**. Undervisning har Forløb, Materialer og Afstemninger. Undervisere finder Til vurdering under Opgaver og Holdindstillinger under Klassen. Afstemninger oprettes og besvares på det valgte hold; de har fortsat HoldId som adgangsgrænse. De gamle direkte afstemningslinks virker fortsat.

Valgt hold og undervisningsforløb følger med i URL'er til faner, sider, editor, quests, kalender, grupper og afstemningens tavlevisning. Brødkrummer og lokale tilbageknapper fører tilbage til samme kontekst, også efter genindlæsning. Wrapperens workspace-id og det faktiske hold-id holdes adskilt; kontekst valideres mod API'ets hold og forløb og giver aldrig adgang i sig selv. Kun et eksplicit valg af Alle holdets forløb eller et andet hold nulstiller forløbsvalget. Opgave- og kalenderfanerne viser stadig hele holdets data.

Personlige sider kræver login. Oversigten og de allerede offentlige læsesider kan åbnes uden login. Notifikationer og klokken bruger samme personlige oversigt fra `GET /api/me/actions?version=2`. Den samler obligatoriske afleveringer og rettelser, aktuelle frivillige Side/Daily-quests, ubesvarede afstemninger, underviserens vurderinger, egne uåbnede gaver samt ulæst feedback og notesbogskommentarer/-delinger. Rød er vigtige mangler, orange er handlinger/ulæste beskeder, grøn vises kun efter et vellykket svar uden ventende punkter. Grå bruges under indlæsning og ved fejl.

Fremtidige daily-gentagelser tæller først på deres danske dato. Udløbne frivillige muligheder og indsendt/godkendt arbejde står ikke som mangler. Gaver er personlige og får aldrig et opdigtet hold; de kan åbnes fra `/gifts/{id}` i alle temaer og fra Mit univers. En gave forsvinder først fra klokken, når modtageren pakker den ud.

Feedback, kommentarer og delinger får vedvarende læsekvitteringer via `POST /api/me/actions/read`. Kun indhold, der er hentet og faktisk vises, kvitteres. Skjulte historiske feedbackversioner tæller først, når de åbnes. Ugyldige links og fejl kvitterer ikke andre beskeder. Kvitteringer kan ikke fuldføre opgaver, besvare afstemninger eller åbne gaver. Kvitteringer er personlige, eksporteres med egne data og revaliderer fortsat kildeadgang; en tilbagekaldt deling giver ingen ny læseadgang. Elevpreview har kun elevens holdhandlinger og kan hverken læse private gaver/notesbogsbeskeder eller kvittere med lærerens konto.

Et klik på klokken åbner en lille oversigt med alle aktuelle handlinger og rulning, de vigtige først. Direkte links bevarer hold-/forløbskontekst eller åbner den konkrete personlige gave/besked. Se alle notifikationer åbner den fulde oversigt. Escape, klik udenfor og navigation lukker dropdownen; konto- og elevskift nulstiller gammel personlig visning.

Klokken opdateres efter svar/aflevering, gaveåbning og læsekvittering, ved navigation med kort fælles cache og hver 45. sekund. Version 1 bevarer det gamle kontraktudsnit for allerede åbne klienter; version 2 tilføjer de nye typer. Migreringen `ActionCenterReadReceipts` tilføjer kun en kvitteringstabel.

Der opfindes ingen logbogspligt ud fra aktive skabeloner. Badgeopnåelser, profilkrav, fremmøde og holdinvitationer kan først tænde klokken, når de har egentlige tildelings-/krav-/acceptdata. Kalenderbegivenheder er ikke i sig selv en personlig mangel.

Oversigten har lokal offentlig søgning og fem kategorier med direkte links: `/library?category=courses|subjects|projects|badges|characters`. Læringsplaner vises med indeks, mål og ugeplan, fag med emnelinks, projekter med DEMO-billeder og badges med kriterier. De offentlige badgekrav vises ens med og uden login; personlige badges findes under Mit univers.

System, lyst, mørkt, jul, nytår og hacker kan vælges med Tema-knappen i topbaren før og efter login. [[Temaer-og-nytaar]] beskriver temakataloget. Rainbow er udgået.

## Global søgning

Søg fra hele platformen i forløb, fag/emner, quests, badges og projekter. Resultater viser type og faglig kontekst og linker til en rigtig indholdsside. Man kan afgrænse efter kategori. Tomme resultater er forskellige fra en API-fejl.

## Acceptkriterier

- [x] Headeren forbliver på én række ved desktop- og mobilbredder; alle funktioner er tastaturtilgængelige.
- [x] Navigation og handlinger tilpasses rolle, medlemskab og aktiv forløbskontekst.
- [x] Temavalg huskes; grundtemaer kræver ikke guld, badges eller level.
- [ ] En synlig kontekststi følger forløb → fag/emne → quest → aflevering.
- [ ] Søgning virker på tværs af de relevante indholdstyper og giver stabile direkte links.
- [ ] Private projekter, afleveringer og elevdata lækker ikke gennem søgeresultater eller uddrag.
- [x] Ingen dobbeltmenuer eller gentagne afstemningsbannere.

## Afhængigheder

[[Underviser-og-elev-views]] · [[Hold-og-Lectio]] · [[Aktiviteter-og-status]] · [[LUP-design-og-leveranceplan]]

LUP-shell og global søgning er separate implementeringsopgaver; der introduceres ikke en ny frontendstack.

## GitHub-opfølgning

- [#27 – LUP-shell: kompakt navigation, temaer og forløbskontekst](https://github.com/Mercantech/Mercantec.tech/issues/27)
- [#28 – Global søgning i forløb, emner, quests, badges og projekter](https://github.com/Mercantech/Mercantec.tech/issues/28)

## Før login og Om LUP · issue 78

Den offentlige topbar følger det valgte tema via de fælles farvetokens. Forsiden åbnes via LUP-navnet, så det tidligere Undervisning-link er fjernet fra desktop og mobil. Oversigt er det nye navn for den eksisterende `/library`-side; kategori- og planparametre samt gamle links er bevaret.

Om LUP på `/sources` bruger `AboutLup.razor` med særskilte afsnit for elever og undervisere, eksisterende karakterer og genveje til Oversigt og login. Fortolkningerne af LUP ligger i `Branding/LupIdentity.cs` og deles med footeren. De fleste besøg viser det officielle navn; enkelte viser en af de humoristiske varianter. Valget foretages én gang pr. layoutbesøg og ændres ikke ved navigation, temaskift eller periodiske opdateringer. Footerens tekstfelt har fast bredde og én linje, så varianterne ikke ændrer højden.

## Stabil placering og karaktervisning · 10. november 2026

Offentlige sider bruger en fælles indholdsramme på 1320 px med de samme sideindryk som topbaren. Scrollbarplads reserveres på normale sider, så sider med forskellig højde ikke flytter header og indhold. Notion-editoren bevarer sin egen kant-til-kant-arbejdsflade.

Øverst på Om LUP skifter karaktervisningen automatisk mellem karaktererne med faste intervaller. Karaktererne vises kun øverst, med diskrete manuelle pile og pauseknap. De tidligere elev-/underviser-genveje i introduktionen er fjernet; de to beskrivende afsnit er bevaret.

## Fælles portrætter og forsideindikator

Om LUP og Karakterer-siden bruger begge `CharacterCatalog.All`. Det valgfrie `CharacterProfile.Portrait` samler fritlagte og optimerede portrætter i kataloget; `DisplayPortrait` falder tilbage til første optræden for nye karakterer. Hero, karakterkort, profilsidens standardbillede og søgning bruger samme portrætvalg. Om LUP har ingen særskilt liste over figurer eller billedstier.

Gummianden, Kamæleonen og Docker har fået transparente portrætter. Originalerne er bevaret i billedgallerierne. LUP-forklaringerne ligger i `AboutName.razor`, mens forside-linket i topbaren får understregning og `aria-current=page`, når den relevante forside er aktiv.
