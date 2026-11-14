---
type: feature
status: active
tags:
  - lup
  - spil
---

# Dagens Wordle

Åbn **Spil → Dagens Wordle** for at møde dagens LUP-karakter og se temaet. Log ind for at spille. Gæt et dansk ord på fem bogstaver med det fysiske tastatur eller spillets danske skærmtastatur.

- **✓** Bogstavet står på den rigtige plads.
- **↔** Bogstavet er med og står et andet sted.
- **×** Denne forekomst af bogstavet er brugt op eller findes ikke i ordet.

Du har seks forsøg. Et ord uden for gætteordlisten bruger ikke et forsøg. Efter runden vises løsningen og en kort forklaring fra karakterens fagområde. Dine accepterede gæt gemmes på din konto, så du kan fortsætte på en anden enhed samme dag.

Den danske dato skifter ved midnat. Både sejr og nederlag tæller som en dag i din streak, når du gør runden færdig. Springer du en dag over, starter næste gennemførte dag en ny streak. Din rekord gemmes.

## Ordbank og kalender

De 11 værter skifter i en fast rotation. Første ordbank giver 960 forskellige dagsord. API'et gemmer den valgte opgave, før den vises, så en udrulning ikke skifter dagens ord. En ny ordbank kan udvide spillets kalender. Hvis ingen ny ordbank er klar, fortsætter hver vært med sit eget udvalg af ord efter de første 960 dage.

Ordbank og gætteord ligger i `api/Wordle/Data/`. Dagsord og facit behandles i API'et. Gætteordlisten er afledt af [Det Centrale Ordregister](https://ordregister.dk/).

Første leverance af [#96](https://github.com/Mercantech/Mercantec.tech/issues/96).
