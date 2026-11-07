# Standardemojis

Et udvalg af 40 almindelige reaktioner og symboler til undervisning, udvikling og planlægning. Billederne er uændrede PNG-filer på 72 × 72 px fra Twemoji v17.0.1.

Grafikken er skabt af Twitter, Inc. og Twemoji-bidragydere og distribueres under Creative Commons Attribution 4.0 International. Den fulde licens følger med i [LICENSE-GRAPHICS.txt](LICENSE-GRAPHICS.txt). Kilde og licens er også tilgængelige fra platformens emoji-bibliotek. [Twemoji v17.0.1](https://github.com/jdecked/twemoji/tree/v17.0.1), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)

Datamigrationen `SeedStandardEmojis` importerer pakken én gang i det eksisterende bibliotek. Eksisterende navne og ID'er bevares ved konflikt. Undervisere kan derefter omdøbe og slette disse emojis på samme måde som uploadede billeder.

Billederne indgår som indlejrede API-ressourcer med nøgler under `standard-emojis/`. De bliver vist gennem bibliotekets almindelige autentifikation og billedtickets. Egne uploads bruger fortsat det konfigurerede objektlager.

API'et udleder `isStandard`, `category` og danske/engelske `keywords` fra den stabile billednøgle. Kategorier og søgeord findes i `StandardEmojiMetadata.cs` og følger med ved omdøb. Egne uploads returnerer `isStandard: false`, `category: "custom"` og søger på deres aktuelle navn. Vælgernes søgning og kategorier ændrer ingen gemte emojis eller dokumenter.
