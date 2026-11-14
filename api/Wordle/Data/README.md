# Wordle-ordbanken

`guesses.txt` rummer 6.419 gyldige danske gæt fra version 1.5.1.0 af [Det Centrale Ordregister](https://ordregister.dk/). Ordregisterets [dokumentation](https://ordregister.dk/doc/COR.html) beskriver grundressourcen som frit tilgængelig og åben for enhver anvendelse. Den oprindelige [TSV-fil](https://ordregister.dk/files/cor1.5.1.0.tsv) hentes af `node scripts/import-wordle-guesses.mjs`. Scriptet bevarer normerede former med fem bogstaver i det danske alfabet og frasorterer blandt andet forkortelser og egennavne.

`answers.json` indeholder 960 unikke dagsord. Hvert ord hører til én karakter og har en selvstændig, dansk forklaring med faglig sammenhæng. Et dagsord skal findes i gætlisten. Vælg genkendelige danske ordformer og etablerede lån, og kontrollér at forklaringen bruger den betydning, som passer til karakterens fag.
