---
type: feature
status: active
tags:
  - karakterer
  - lup
---

# Karaktersider

Offentlig oversigt på `/characters` med egne undersider:

- `/characters/lupp`: LUPP er underviser i **Game Design** og hjælper med spilidéer, gameplay loops, målgrupper, styring og feedback. Blyantportrættet og de ni undervisningsillustrationer er hentet uændret fra [Game Design-lektionen](https://mercantec.notion.site/Game-design-400dab5ca23780bbaa93fa9e9e9f2e6b?pvs=74). Den fulde lektion tilgås gennem linket. LUPPs AI-editorhjælp og nytårsrolle bevares som øvrige optrædener; nytårsbilledet kan vælges som alternativt udtryk. Se originale filer, dimensioner, SHA256 og stabile Notion-referencer i `web/wwwroot/images/characters/lupp/README.md`.
- `/characters/vatson`: Vatson hører til det kommende **VATEK**-projekt. Profilen er markeret **På vej**. Hans originale robotportræt er hentet uændret fra VATek.dk; kilde i `web/wwwroot/images/characters/vatson/README.md`. Hans eksisterende emojis i editoren bevares i deres nuværende katalog.
- `/characters/raymond`: Raymond Stonebraker Codd, også Professor Codd, SQL-underviseren fra SQL-H1. Portræt, underviser, festtøj og sommerferie er kopieret uændret fra SQL-H1s eksisterende webassets. Se kildehenvisninger i `web/wwwroot/images/characters/raymond/README.md`.
- `/characters/beaver`: **Bæver** underviser i **Sikkerhed og test** fra det blå teams perspektiv: sikker kode, adgangskontrol, logging og regressionstests. Byggeri og vedligeholdelse er dyremetaforen for robuste systemer. Bæver er et foreløbigt navn; routen og billedstien er stabile ved senere navnevalg.
- `/characters/fox`: **Ræv** underviser i **Sikkerhed og test** fra det røde teams perspektiv: angrebsflader, uventede input og afprøvning af rettigheder. Alternative veje og udfordring af antagelser er dyremetaforen for etisk sikkerhedstest. Ræv er et foreløbigt navn; routen og billedstien er stabile ved senere navnevalg. Ræv og Bæver er kompetente kolleger, der sammen forstår svagheder, styrker løsninger og skriver tests.

Indholdet samles i `web/Characters/CharacterCatalog.cs`; siderne ligger i `web/Pages/Characters/`, og stylingen i `web/wwwroot/css/lup-characters.css`. `CharacterProfile.Illustrations` indeholder undervisningsgalleriet, adskilt fra portrætvarianterne i `Appearances`. Billedvalget er lokalt komponentstate og nulstilles ved skift til en anden karakter. Alle figurers originale filer ligger i `web/wwwroot/images/characters/`; prompts og kildeoplysninger følger billederne.

Oversigt og profiler er tilgængelige uden login og findes i navigationen, footeren, Om LUP og søgningen. Biblioteket har en særskilt kategori til karakterer. Links til editor og personlige indstillinger følger de eksisterende loginregler. Intet nyt loginflow eller nye API-endpoints.
