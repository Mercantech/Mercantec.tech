# Vatsons eksisterende emojis

De 63 PNG-filer er hentet fra LUPs eksisterende emoji-bibliotek den 8. november 2026 med brugerens adgang til biblioteket. Vatson tilhører VATek. Billederne er bibliotekets eksisterende udgaver og er ikke nye illustrationer.

- Bibliotek: https://mercantec.tech/emojis
- Filnavnene følger bibliotekets emoji-navne uden præfikset `vatson-`.
- Filerne er kopieret byteidentisk fra de hentede biblioteksfiler. Der er hverken genereret, omtegnet, beskåret, komprimeret eller opskaleret billeder i denne import.
- De hentede filer er højst 256 pixel på hver led. 58 billeder er 256 × 256; fem har andre mål. De faktiske billedmål bevares. Dette sæt indeholder ikke større originalrenderinger.
- Bibliotekets eksisterende ALT-tekster er bevaret. De danske visningsnavne i kataloget er redaktionelle titler.
- `manifest.json` indeholder bibliotekets emoji-navne og kilde-id'er, lokale billedstier, titler, ALT-tekster, faktiske mål, filstørrelser og SHA256 samt hentningsdato.
- Adgangsbilletter, signerede billed-URL'er, tokens og lokale downloadstier er ikke gemt i kataloget eller manifestet.

Kataloget findes i `web/Characters/VatsonEmojiCatalog.cs`. Manifestets SHA256-værdier gør det muligt at kontrollere, at de lokale filer fortsat er identiske med de importerede biblioteksfiler.
