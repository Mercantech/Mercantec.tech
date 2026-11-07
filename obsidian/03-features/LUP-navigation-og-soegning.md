---
type: feature
status: active
tags: [feature, navigation, search, design]
---

# LUP-navigation og søgning

## Retning

**LUP – Lokal UndervisningsPlatform** har én kompakt header. Logoet åbner det personlige overblik. De fælles indgange er **Mine hold** og **Bibliotek**; undervisere har desuden **Min kalender** på tværs af hold. Søgning, én notifikationsindgang og profil ligger til højre. Mobilnavigationen foldes ind i Menu.

Profilmenuen samler Min profil, Mit univers, Indstillinger og Log ud. Underviserværktøjer vises kun til undervisere. Indstillinger samler tema og kontotilknytning. Biblioteket samler undervisningsplaner, fag, offentlige DEMO-projekter og badgekrav; eksempelforløb er en særskilt foldet sektion for loggede ind. Mit univers samler progression, badges, egne projekter, avatar, lejlighed, butik og spil.

## Holdets navigation

Holdcentralen har fem hovedfaner: **Overblik, Undervisning, Opgaver, Kalender og Klassen**. Undervisning har Forløb, Materialer og Afstemninger. Undervisere finder Til vurdering under Opgaver og Holdindstillinger under Klassen. Afstemninger oprettes og besvares på det valgte hold; de har fortsat HoldId som adgangsgrænse. De gamle direkte afstemningslinks virker fortsat.

Valgt hold og undervisningsforløb følger med i URL'er til faner, sider, editor, quests, kalender, grupper og afstemningens tavlevisning. Brødkrummer og lokale tilbageknapper fører tilbage til samme kontekst, også efter genindlæsning. Wrapperens workspace-id og det faktiske hold-id holdes adskilt; kontekst valideres mod API'ets hold og forløb og giver aldrig adgang i sig selv. Kun et eksplicit valg af Alle holdets forløb eller et andet hold nulstiller forløbsvalget. Opgave- og kalenderfanerne viser stadig hele holdets data.

Personlige sider kræver login. Biblioteket og de allerede offentlige læsesider kan åbnes uden login. Notifikationer og klokken bruger samme personlige oversigt fra `GET /api/me/actions`, uden opdigtede kommentarer eller tællere. Rød betyder overskredet obligatorisk aflevering eller aflevering, som skal rettes; orange betyder en kommende obligatorisk aflevering, en åben ubesvaret afstemning eller underviserens ventende vurderinger. Grøn vises kun efter et vellykket svar uden handlinger. Grå bruges under indlæsning og ved fejl. Kommende daily-gentagelser tæller først på deres danske dato; en indsendt eller godkendt aflevering er ikke længere en elevhandling.

Klokken opdateres efter svar/aflevering og ved navigation med kort fælles cache, samt hver 45. sekund. Konto- og elevskift nulstiller gamle data. Elevpreview læser kun den valgte elevs handlinger på lærerens egne hold; ingen tilknyttet konto eller intet fælles autoriseret hold giver ukendt status.

Biblioteket har lokal offentlig søgning og fire kategorier med direkte links: `/library?category=courses|subjects|projects|badges`. Læringsplaner vises med indeks, mål og ugeplan, fag med emnelinks, projekter med DEMO-billeder og badges med kriterier. De offentlige badgekrav vises ens med og uden login; personlige badges findes under Mit univers.

System, lyst, mørkt, jul, nytår og hacker kan vælges under Indstillinger (gæster har temavalg i headeren). [[Temaer-og-nytaar]] beskriver temakataloget. Rainbow er udgået.

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
