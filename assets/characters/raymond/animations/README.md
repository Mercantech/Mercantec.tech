# Raymond arbejder med databaser

Tre korte, illustrerede GIF-loops til `/characters/raymond`:

| Fil | Handling | Faglig forbindelse |
| --- | --- | --- |
| `join-tables.gif` | Raymond følger forbindelsen mellem to tabeller med pegepinden. | JOIN og fælles nøgler |
| `sort-rows.gif` | Raymond ordner nummererede rækkekort fra 3, 1, 2 til 1, 2, 3. | ORDER BY og resultatets rækkefølge |
| `find-row.gif` | Raymond undersøger en manglende række med en lup. | WHERE og kontrol af filtre |

## Originaler og prompts

Hver undermappe indeholder det originale `sheet.png` samt `prompts.md` med
præcise prompts, referencer, eventuelle fravalgte forsøg og visuel kontrol.
Illustrationerne er lavet med den indbyggede `image_gen.imagegen`, med den
oprindelige Raymond fra `SQL-H1/assets/mascot/candidates/01-sql-underviseren.png`
som identitetsreference. Hans malede stil, brune øjne, blå farver, bronzebriller,
hårtot og snabel bevares. Arkene er opake med en lys baggrund.

- [Forbind tabeller](join-tables/prompts.md)
- [Sæt rækkerne i orden](sort-rows/prompts.md)
- [Find den manglende række](find-row/prompts.md)

## GIF-filer og genopbygning

De genererede ark har seks animationstegninger i et 3 × 2-grid. FFmpeg udskærer
cellerne i læserækkefølge og samler dem med en fælles farvepalette til hver GIF.
Der tilføjes et kort ophold ved begyndelsen og slutningen. Ingen automatisk
frameinterpolation eller manuelt malede mellemtegninger.

Kør fra projektet med PowerShell og FFmpeg/ffprobe på PATH:

```powershell
./scripts/Render-RaymondAnimations.ps1
```

Output: `web/wwwroot/images/characters/raymond/animations/`. Hver GIF er
512 × 512 px og looper over 2,25 sekunder. PNG-filen med samme navn er den
første frame, der bruges som stillbillede på siden. Produktionsappen serverer
de færdige filer og kræver ikke FFmpeg.

Siden starter med stillbilleder. **Afspil** indlæser den valgte GIF, og **Stop**
viser stillbilledet igen. Ingen animation starter automatisk. Profilskift
nulstiller afspilningen. **Hent GIF** gemmer selve animationen til genbrug.
