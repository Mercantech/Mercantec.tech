# Komponentstandard og skabelon til LUP-editoren

Brug denne fil, når en blok i Notion-editoren oprettes eller ændres. Den fælles standard samler krav til udseende, betjening, temaer og formater. Kopiér skabelonen nederst ind i komponentens featurebeskrivelse, og udfyld den med den faktiske adfærd.

En komponent skal kunne bruges af underviseren i kladden og forstås af eleven på den udgivne side. Det samme indhold skal også fungere i præsentation og ved eksport med en beskrevet fallback.

## 1. Visuelt udtryk

- Brug platformens eksisterende farver, skrifter, afstande og kontrolstørrelser. Editorens `--notion-paper`, `--notion-ink`, `--notion-muted`, `--notion-rule`, `--notion-blue` og `--notion-tint` følger LUP.
- Hold komponenten kompakt. En kort etiket og de relevante handlinger kan ligge i en diskret værktøjslinje. Indholdet skal have mest plads.
- Vis avancerede valg efter en brugerhandling. Knapper med ikoner skal have et tydeligt navn for skærmlæsere og en forklarende titel.
- Brug samme sprog og knaptekster på tværs af blokke, eksempelvis **Kode**, **Diagram** og **Kopiér**.
- Indholdet skal passe i editorens kolonne ved 320 px og 390 px. Brede kodeeksempler og diagrammer får et lokalt rulleområde.
- Layoutet skal bevare sin placering, når en knap viser feedback, et diagram opdateres eller en menu åbnes.

## 2. Temaer og tilgængelighed

Alle komponenter skal afprøves i **Lys**, **Mørk**, **Jul**, **Nytår**, **Hacker** og **System**. System afprøves med både lyst og mørkt operativsystem.

Temaet skal hentes fra komponentens egen editor eller præsentation. Flere editorer og flere blokke kan være monteret samtidig. Et skift fra Jul til Nytår skal også opdatere komponenter, selv om begge temaer er mørke.

For biblioteker, der genererer deres egne farver, læses de beregnede platformfarver fra komponentens vært og omsættes til bibliotekets temakonfiguration. Et temaskift ændrer præsentationen af det gemte indhold.

- Platformens tekst, kildekode, hjælpetekst og fejl skal have mindst 4,5∶1 i kontrast mod deres baggrund. Afprøv også komponentens standardindhold.
- Handlinger skal kunne bruges med tastatur og have synlig fokusmarkering. Der skal være en tilsvarende handling til dobbeltklik og andre musespecifikke genveje.
- Berøringsbetjening skal have mindst 44 px høje primære kontroller.
- Skærmlæseren skal kunne finde komponentens navn, aktive valg og feedback.
- Respektér reduceret bevægelse. Undgå animation, som gør indhold eller betjening sværere at følge.
- Forfatterens eksplicitte diagramfarver og formattering er en del af indholdet. Bevar dem ved temaskift, og gør det muligt at redigere kilden. Brugerdefinerede farver skal vurderes sammen med indholdet, da de kan ændre kontrasten.

## 3. Indhold, tilstand og samarbejde

Gem den kanoniske kilde og brugerens indholdsvalg i blokkens schema. Hold åbne menuer, fokus, visningstilstand og igangværende rendering i lokal UI-tilstand.

- Bevar kilde, markering, markørposition, scroll og fortrydelse gennem visuelle opdateringer.
- En fejl i previewet skal bevare den redigerbare kilde og forklare, hvordan brugeren kommer videre.
- Automatisk tolerering af en syntaksfejl må bruges i previewet. En ændring af den gemte kilde skal følge en brugerhandling.
- Ved forsinket lagring skal de sidste indtastninger afleveres, når brugeren afslutter redigering eller forlader komponenten. Afbryd gamle timere og netværks- eller renderopgaver, når de bliver overhalet.
- Et langsomt resultat må kun vises, hvis det stadig svarer til den aktuelle kilde og det aktuelle tema.
- Udgivet visning må anvende den udgivne snapshot. Den skal kunne tegnes uden at forbinde til kladdens samarbejdsrum.
- Komponentens egne knapper og felter skal håndtere deres tastatur- og DOM-hændelser, så editoren bevarer dokumentet og markeringen.
- Genbrug BlockNotes blokstruktur og eksisterende API'er. Bevar metadata, blokmenu og kilde ved ændringer i rendering.
- Nye schemafelter skal have standardværdier, så eksisterende dokumenter kan åbnes. Opdatér samarbejdets versionskontrol, når ældre klienter kan miste nye data.

## 4. Visninger og formater

Beskriv hver række for komponenten. Visninger med begrænsninger skal have en forståelig fallback, der bevarer det relevante indhold og dets adgangsregler.

| Visning eller format | Forventet adfærd |
| --- | --- |
| Kladde | Redigering følger `editor.isEditable`. Kilden og indholdsvalg kan gemmes og fortrydes. |
| Udgivet side | Samme indhold gengives fra den udgivne snapshot. Relevante læsehandlinger, eksempelvis kopiering, er tilgængelige. |
| Præsentation | Indholdet er læsbart på en slide. Private elev- og holddata følger præsentationens eksisterende filtrering. |
| Mobil | Kontroller, kilde og preview kan bruges med touch og inden for skærmens bredde. |
| HTML og Markdown | Komponentens eksport beskriver en tekstlig eller semantisk repræsentation. Import og eksport skal bevare den understøttede kilde. |
| Udskrift og browser-PDF | Vis indholdet med et læsbart layout. Skjul redigeringskontroller, og tilpas brede elementer til papirets bredde. Beskriv eventuelle begrænsninger. |
| Kopiering | Beskriv præcist, hvad knappen kopierer. UI-elementer som linjetal og hjælpetekst indgår kun, hvis handlingen udtrykkeligt beder om dem. |
| Tomt indhold og fejl | Vis en kort vejledning eller en forståelig fejl. Kilden skal fortsat kunne åbnes og kopieres. |

HTML fra renderbiblioteker skal behandles efter den eksisterende sikkerhedsmodel. Mermaid bruger eksempelvis strict-konfiguration og rensning af den genererede SVG. Brug stabile ID'er til relationer og den eksisterende autorisation til beskyttede data.

## 5. Første anvendelse på Mermaid

Mermaid-blokken gemmer Mermaid-kode i `props.source`. Previewet er en afledt SVG. Komponentens værktøjslinje samler navnet **Mermaid**, et valg mellem **Diagram** og **Kode** i kladden og **Kopiér** til den rå kilde.

Ved redigering er kilden tilgængelig sammen med previewet, så en fejl kan rettes løbende. Elevvisning og præsentation viser diagrammet og relevante læsehandlinger.

BlockNotes interne FullHTML-eksport bevarer blokkens metadata og kilde i `data-source`. Den eksterne HTML-repræsentation bruger `pre`/`code` med `language-mermaid`. Markdown bevarer kilden som en Mermaid-kodeblok. Disse repræsentationer giver en kildefallback i værktøjer, der ikke kan tegne diagrammet.

Ved browserudskrift vises diagrammet inden for papirets bredde. Komponentens redigeringskontroller og åbne kildefelt skjules, mens en eventuel fejlfallback bevares.

Diagrammets standardfarver hentes fra den lokale platformvært. Gemt Mermaid-kode med eksplicitte `style`- eller `classDef`-regler bevarer disse regler. Rendering af flere diagrammer skal koordinere Mermaid-bibliotekets fælles konfiguration, så tema og kilde bliver koblet korrekt.

Implementeringen findes i [mermaid-block.tsx](src/mermaid-block.tsx), fælles kildebehandling i [mermaid-utils.ts](src/mermaid-utils.ts) og udseendet i [editor.css](src/editor.css).

## Skabelon til en komponentbeskrivelse

Kopiér afsnittet nedenfor og erstat felterne i kantede parenteser.

~~~md
# [Komponentens navn]

## Formål og placering

[Hvilket undervisningsbehov løser komponenten, og hvordan indsættes den?]

## Indhold og schema

- Bloktype og kildefil — [type og sti]
- Kanonisk indhold — [kilde, tekst, relationer eller filer]
- Gemte valg og standardværdier — [felter]
- Eksisterende dokumenter — [hvordan åbnes ældre data?]
- Adgang — [hvem må læse og ændre hvad?]

## Udseende og tema

[Etiket, primære handlinger, platformtokens og placering af detaljer.]
[Hvordan opdateres previewet ved alle temaer og i præsentation?]

## Betjening

[Beskriv tastatur, touch, fokus, markering, fortrydelse og kopiering.]
[Beskriv gemning, ventetilstand og håndtering af samtidige ændringer.]

## Visninger og formater

| Visning | Resultat og fallback |
| --- | --- |
| Kladde | [redigering] |
| Udgivet | [læsning] |
| Præsentation | [slides og eventuel filtrering] |
| Mobil | [layout og touch] |
| HTML/Markdown | [eksport, import og kendte begrænsninger] |
| Udskrift/browser-PDF | [layout og eventuelle begrænsninger] |
| Kopiér | [præcist indhold] |
| Tomt/ugyldigt | [vejledning og gendannelse] |

## Verifikation

- [ ] Alle fem eksplicitte temaer samt System i lys og mørk.
- [ ] Desktop, 320 px og 390 px med lokalt scroll ved bredt indhold.
- [ ] Tastatur, touch, fokusmarkering og oplæste labels.
- [ ] Kontrast for indhold, kontroller, status og fejl.
- [ ] Indhold og markering bevares ved tema- og størrelsesskift.
- [ ] Sidste indtastning gemmes ved afslutning, og fortrydelse virker.
- [ ] Flere instanser kan bruges uden at påvirke hinanden.
- [ ] Gem, genindlæs og udgiv giver samme indhold og valg.
- [ ] Udgivet visning, præsentation, eksport og udskrift følger kontrakten.
- [ ] Tom kilde, fejl og forsinkede resultater er afprøvet.

Afprøvet med [miljø, dato, relevante build- eller browsertests].
Kendte begrænsninger [konkrete afvigelser og deres fallback].
~~~

## Tekniske referencer

- [Editorens integration og drift](README.md)
- [Forløbseditorens featurebeskrivelse](../obsidian/03-features/Forloebseditor.md)
- [BlockNote custom blocks og eksport](https://www.blocknotejs.org/docs/features/custom-schemas/custom-blocks)
- [Mermaid-temakonfiguration](https://mermaid.js.org/config/theming.html)
- [WCAG 2.2 om tekstkontrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)
