# Raymond forklarer SQL

16 originale, brede undervisningsinfografikker introducerer de 24 oplyste målpinde
for Databaseprogrammering, fag 16474, på H1–H3. Serien giver fagligt overblik og
eksempler; billederne dokumenterer ikke i sig selv, at en lærling har opnået en
målpind. De oplyste mål er bevaret i `sql-infographics-objectives.json`.

Raymond bevarer den eksisterende malede identitet fra `professor.webp`: blå pels,
brune øjne, bronzebriller, hårtot, beige cardigan, bog og pegepind. Formatet er
1536 × 1024 px med korte danske forklaringer, tabeller og pile. Alle billeder er
genereret enkeltvis med den indbyggede imagegen-billedgenerator.

| Billede | Trin | Målpinde |
| --- | --- | --- |
| 01 · Fra behov til databasedesign | H1 | 1, 4, 8 |
| 02 · Nøgler og relationer | H1 | 1, 5 |
| 03 · Constraints | H1 | 6, 11 |
| 04 · SELECT, filtre og sortering | H1 | 2 |
| 05 · Gruppering og aggregering | H1 | 2 |
| 06 · INSERT, UPDATE og DELETE | H1 | 3, 10 |
| 07 · JOIN og subqueries | H1 | 7 |
| 08 · Normalisering | H1 | 1, 17 |
| 09 · Views og rettigheder | H1 | 9, 10 |
| 10 · Procedurer og triggere | H1 | 10, 12, 13 |
| 11 · Indeks | H2 | 15 |
| 12 · Performancemåling | H2 | 16 |
| 13 · ORM, model og database | H2 | 1, 18, 19, 20 |
| 14 · Dokumentdatabaser og valg | H3 | 21, 22 |
| 15 · Databasesikkerhed | H3 | 14, 23 |
| 16 · Databasetest | H3 | 24 |

## Originaler, prompts og forklaringer

- Native PNG-originaler: `web/wwwroot/images/characters/raymond/infographics/`.
- Fuld prompt, ordret billedtekst, billedreference og kildeartefakt:
  [pakke a](sql-infographics-20261108-a.json),
  [pakke b](sql-infographics-20261108-b.json) og
  [pakke c](sql-infographics-20261108-c.json).
- `sql-infographics-catalog.json` samler danske titler, ALT-tekster, tilgængelige
  tekstforklaringer, SQL-eksempler, trin, målpinde og faglige kildehenvisninger.
- `sql-infographics-preview-report.json` dokumenterer dimensioner, filstørrelser
  og SHA256 for originaler og forhåndsvisninger. Native PNG'er ændres ikke.

## Fagligt grundlag

H1-eksemplerne følger [SQL-H1](https://sql.mercantec.tech/) og projektets
`content/modules.json` og `content/seeds/shop.sql`: PostgreSQL 16 og caféens
`customers`, `products` og `orders`. Billederne bruger udvalgte eller forenklede
eksempelrækker; de gengiver ikke nødvendigvis hele seedets indhold. `orders` er
en forenklet ordrelinjemodel. SQL-mysteriets særskilte SQLite-dialekt indgår ikke.

ORM med EF Core og dokumentdatabaser supplerer H1-indholdet til H2/H3. Hvert billede
har relevante primærkilder i kataloget: PostgreSQL, Npgsql, Microsoft EF Core og
MongoDB. De tilgængelige tekstforklaringer uddyber billedernes forenklinger.

Database- og rolleadministration kræver passende rettigheder i egen øvedatabase;
SQL-H1-sandboxen blokerer bl.a. `CREATE/DROP DATABASE` og oprettelse af roller.
Et view gemmer en forespørgsel og giver ikke automatisk en sikkerhedsgrænse.
Triggere udfører triggerfunktioner, mens procedurer kaldes med `CALL`.
`EXPLAIN ANALYZE` kører faktisk forespørgslen; cost er ikke millisekunder.
Et ORM er et adgangslag, og migrations/scaffolding er særskilte arbejdsgange.

## Webpakning

Installér Sharp lokalt i en ignoreret værktøjsmappe, hvis den ikke allerede findes:

```powershell
npm install --prefix .tools/character-previews --no-save sharp@0.35.5
node scripts/prepare-character-infographic-assets.mjs
```

`--tools-dir=ABSOLUT_MAPPE` kan pege på en eksisterende Sharp-installation.
Scriptet kontrollerer proveniens, native dimensioner, metadata og dækning af alle
24 mål. Det laver 1200 px WebP-forhåndsvisninger og genererer
`shared/CharacterInfographicCatalog.cs`. Dette er pakning af statiske aktiver;
Docker og appens runtime kræver ikke Sharp.

Raymonds profil viser billeder, søgning, trinfiltre, originale PNG-downloads og
tekstforklaringer med kilder. Biblioteket bruger samme katalog, en let miniature
og et direkte link til `#character-gallery-title`.

Serviceworkeren henter ikke postersamlingen eller PNG-originalerne under
appinstallation. Et billede caches først, når det bliver brugt, og kan derefter
vises offline. De målrettede cachetests køres med
`node --test tests/avatar-service-worker.test.cjs`.
