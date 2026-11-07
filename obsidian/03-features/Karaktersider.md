---
type: feature
status: active
tags:
  - karakterer
  - lup
---

# Karaktersider

Offentlig oversigt på `/characters` med egne undersider:

- `/characters/lupp`: LUPP er AI-hjælperen i Notion-editoren og den lilla fyrværkerifigur fra nytårstemaet. Genbruger den eksisterende nytårsillustration.
- `/characters/vatson`: Vatson hører til det kommende **VATEK**-projekt. Profilen er markeret **På vej**. Hans originale robotportræt er hentet uændret fra VATek.dk; kilde i `web/wwwroot/images/characters/vatson/README.md`. Hans eksisterende emojis i editoren bevares i deres nuværende katalog.
- `/characters/raymond`: Raymond Stonebraker Codd, også Professor Codd, SQL-underviseren fra SQL-H1. Portræt, underviser, festtøj og sommerferie er kopieret uændret fra SQL-H1s eksisterende webassets. Se kildehenvisninger i `web/wwwroot/images/characters/raymond/README.md`.

Indholdet samles i `web/Characters/CharacterCatalog.cs`; siderne ligger i `web/Pages/Characters/`, og stylingen i `web/wwwroot/css/lup-characters.css`. Billedvalget på Raymonds side er lokalt komponentstate og nulstilles ved skift til en anden karakter.

Oversigt og profiler er tilgængelige uden login og findes i navigationen, footeren, Om LUP og søgningen. Biblioteket har en særskilt kategori til karakterer. Links til editor og personlige indstillinger følger de eksisterende loginregler. Intet nyt loginflow eller nye API-endpoints.
