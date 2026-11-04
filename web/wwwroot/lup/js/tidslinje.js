/** Tidslinje: Inden H1 → H1 → Inden H2 → … → H6 */

const img = (name, alt) =>
  `<figure class="praktik-fig"><img src="assets/praktik/${name}" alt="${alt}" loading="lazy" /></figure>`;

const bridge = (text) =>
  `<p class="tl-bridge">${text}</p>`;

export const tidslinje = [
  {
    id: "inden-h1",
    type: "praktik",
    label: "Inden H1",
    title: "Praktik mellem GF2 og H1",
    summary:
      "Kendskab til virksomhedens teknologier, og blød start på versionsstyring og database.",
    image: "gf2-h1.png",
    body: `
      ${bridge(
        "Jeres lærling kommer lige fra GF2 og møder virksomheden for første gang. Inden H1 anbefaler vi, at I viser jeres stack, værktøjer og daglige arbejdsgange — og giver en blød start på Git og jeres databasestruktur. På H1 (10 uger) bygger lærlingen videre med C#, SQL, Blazor og Git."
      )}
      ${img("gf2-h1.png", "Praktikmål mellem GF2 og H1")}
      <div class="cols">
        <div>
          <p><strong>Praktikmål for virksomheden generelt</strong></p>
          <ul class="check-list">
            <li><strong>Programudvikling.</strong> Lærlingen har kendskab til programudvikling og til virksomhedens teknologier og struktur.</li>
          </ul>
        </div>
        <div>
          <p><strong>Forberedelse til 1. hovedforløb</strong></p>
          <ul class="check-list">
            <li><strong>Versionsstyring.</strong> GitHub bruges på H1 — sæt lærlingen ind i jeres værktøjer og grundprincipper.</li>
            <li><strong>Database.</strong> Vis virksomhedens databasestruktur og valg inden SQL på H1.</li>
            <li><strong>Motivation og nysgerrighed.</strong> Vi forventer ikke bredt programmeringskendskab — fokus er, at lærlingen møder motiveret og nysgerrig op.</li>
          </ul>
        </div>
      </div>
    `,
  },
  {
    id: "h1",
    type: "skole",
    label: "H1",
    image: "h1.png",
  },
  {
    id: "inden-h2",
    type: "praktik",
    label: "Inden H2",
    title: "Praktik mellem H1 og H2",
    summary:
      "Webapplikationer, versionering og database — plus forberedelse til ORM og frontend på H2.",
    image: "h1-h2.png",
    body: `
      ${bridge(
        "Jeres lærling kan nu arbejde med grundlæggende C#, SQL, Blazor og Git. Inden H2 anbefaler vi, at I sætter lærlingen på rigtige opgaver hos jer — gerne små fullstack- og dataopgaver, så kompetencerne bliver brugt. På H2 (10 uger) kommer EF Core, API og mere avanceret fullstack i centrum."
      )}
      ${img("h1-h2.png", "Praktikmål mellem H1 og H2")}
      <div class="cols">
        <div>
          <p><strong>Praktikmål for virksomheden generelt</strong></p>
          <ul class="check-list">
            <li><strong>(Web)applikationer.</strong> Enkle fullstack-løsninger med moderne frontend og backend.</li>
            <li><strong>Versionering.</strong> Forståelse for virksomhedens Git-arbejdsgange.</li>
            <li><strong>Databaseudvikling.</strong> Erfaring med databaser, typer og værktøjer.</li>
          </ul>
        </div>
        <div>
          <p><strong>Forberedelse til 2. hovedforløb</strong></p>
          <ul class="check-list">
            <li><strong>Versionsstyring.</strong> Mere praktisk indsigt i professionel versionskontrol.</li>
            <li><strong>Frontend.</strong> Holde Blazor-færdigheder ved lige.</li>
            <li><strong>Database.</strong> Kendskab til dataarbejde og evt. ORM i virksomheden.</li>
          </ul>
        </div>
      </div>
    `,
  },
  {
    id: "h2",
    type: "skole",
    label: "H2",
    image: "h2.png",
  },
  {
    id: "inden-h3",
    type: "praktik",
    label: "Inden H3",
    title: "Praktik mellem H2 og H3",
    summary:
      "Web, API, versionering og database — plus forberedelse til IoT/Arduino, frontend og ORM på H3.",
    image: "h2-h3.png",
    body: `
      ${bridge(
        "Jeres lærling kan nu bygge større fullstack-løsninger med API, EF Core og Blazor. Inden H3 anbefaler vi, at I holder API-, data- og Git-kompetencerne varme — og gerne giver et første kig på Arduino eller C++, hvis I har mulighed. På H3 (9 uger) kobles det til IoT og dashboards."
      )}
      ${img("h2-h3.png", "Praktikmål mellem H2 og H3")}
      <div class="cols">
        <div>
          <p><strong>Praktikmål for virksomheden generelt</strong></p>
          <ul class="check-list">
            <li><strong>(Web)applikationer.</strong> Moderne fullstack-webløsninger på frontend og backend.</li>
            <li><strong>(API)applikationer.</strong> API-løsninger med moderne, anvendte teknologier.</li>
            <li><strong>Versionering.</strong> Forståelse for virksomhedens Git-arbejdsgange og værktøjer.</li>
            <li><strong>Databaseudvikling.</strong> Erfaring med databaser, typer og relevante værktøjer.</li>
          </ul>
        </div>
        <div>
          <p><strong>Forberedelse til 3. hovedforløb</strong></p>
          <ul class="check-list">
            <li><strong>Versionsstyring.</strong> Praktisk indsigt i professionel versionskontrol og begyndende projektstyring.</li>
            <li><strong>Frontend.</strong> Holde Blazor/SPA-færdigheder ved lige — komponenter og brugerinteraktion.</li>
            <li><strong>Client / IoT.</strong> Bekendtskab med Arduino eller C++ inden IoT-arbejdet på H3.</li>
            <li><strong>Database.</strong> Fortsat arbejde med virksomhedens data og valg af ORM.</li>
          </ul>
        </div>
      </div>
    `,
  },
  {
    id: "h3",
    type: "skole",
    label: "H3",
    image: "h3.png",
  },
  {
    id: "inden-h4",
    type: "praktik",
    label: "Inden H4",
    title: "Praktik mellem H3 og H4",
    summary:
      "API, versionering og database — plus blød start på Flutter/Dart inden H4.",
    image: "h3-h4.png",
    body: `
      ${bridge(
        "Jeres lærling kan nu følge kæden fra sensor til API og dashboard. Inden H4 anbefaler vi, at I fortsætter med API, data og Git hos jer — og gerne lader lærlingen kigge lidt på mobiludvikling, hvis det giver mening. På H4 (7 uger) skifter fokus til Flutter/Dart og mobil integration."
      )}
      ${img("h3-h4.png", "Praktikmål mellem H3 og H4")}
      <div class="cols">
        <div>
          <p><strong>Praktikmål for virksomheden generelt</strong></p>
          <ul class="check-list">
            <li><strong>(API)applikationer.</strong> API-løsninger med moderne, anvendte teknologier.</li>
            <li><strong>Versionering.</strong> Forståelse for virksomhedens Git-arbejdsgange og værktøjer.</li>
            <li><strong>Databaseudvikling.</strong> Erfaring med databaser, typer og relevante værktøjer.</li>
          </ul>
        </div>
        <div>
          <p><strong>Forberedelse til 4. hovedforløb</strong></p>
          <ul class="check-list">
            <li><strong>Versionsstyring.</strong> Praktisk indsigt i professionel versionskontrol og begyndende projektstyring.</li>
            <li><strong>Frontend / mobil.</strong> På H4 introduceres Flutter/Dart — det er en fordel, hvis lærlingen har kigget på mobiludvikling.</li>
            <li><strong>Database.</strong> Fortsat arbejde med virksomhedens data og valg af ORM.</li>
          </ul>
        </div>
      </div>
    `,
  },
  {
    id: "h4",
    type: "skole",
    label: "H4",
    image: "h4.png",
  },
  {
    id: "inden-h5",
    type: "praktik",
    label: "Inden H5",
    title: "Praktik mellem H4 og H5",
    summary: "Softwaretest, app og sikkerhed — plus blød start på Linux og projektstyring.",
    image: "h4-h5.png",
    body: `
      ${bridge(
        "Jeres lærling kan nu bygge mobilapps med API-integration og test. Inden H5 anbefaler vi, at I bruger perioden på app, test og sikkerhed — og gerne giver et første møde med Linux eller drift, hvis I har det i huset. På H5 (9 uger) fylder Docker, pipelines og mini-svendeprøve."
      )}
      ${img("h4-h5.png", "Praktikmål mellem H4 og H5")}
      <div class="cols">
        <div>
          <p><strong>Praktikmål for virksomheden generelt</strong></p>
          <ul class="check-list">
            <li><strong>Softwaretest.</strong> Kvalitetssikring og test af programmer</li>
            <li><strong>App.</strong> Udviklet eller hjulpet med app i et framework</li>
            <li><strong>Sikkerhed.</strong> Indsigt i sikkerhedsløsninger i softwareudvikling</li>
          </ul>
        </div>
        <div>
          <p><strong>Forberedelse til 5. hovedforløb</strong></p>
          <ul class="check-list">
            <li><strong>Linux.</strong> Kendskab til Linux i virksomhedens systemlandskab</li>
            <li><strong>Projektstyring.</strong> Mindre opgaver med planlægning og dokumentation</li>
          </ul>
        </div>
      </div>
    `,
  },
  {
    id: "h5",
    type: "skole",
    label: "H5",
    image: "h5.png",
  },
  {
    id: "inden-h6",
    type: "praktik",
    label: "Inden H6",
    title: "Praktik mellem H5 og H6",
    summary: "Projektstyring og embedded — forberedelse til svendeprøven.",
    image: "h5-h6.png",
    body: `
      ${bridge(
        "Jeres lærling kan nu arbejde med Linux, containere, data og et mini-svendeprøveprojekt. Inden H6 anbefaler vi, at I giver lærlingen mere selvstændigt ansvar og træner dokumentation og proces — det er den bedste forberedelse til svendeprøven. På H6 (5 uger) gennemføres den formelle prøve."
      )}
      ${img("h5-h6.png", "Praktikmål mellem H5 og H6")}
      <div class="cols">
        <div>
          <p><strong>Praktikmål for virksomheden generelt</strong></p>
          <ul class="check-list">
            <li><strong>Projektstyring.</strong> Deltagelse i udvikling med projektstyringsværktøjer</li>
            <li><strong>Embedded systemer.</strong> Programmering til embedded med Linux eller anden platform</li>
          </ul>
        </div>
        <div>
          <p><strong>Forberedelse til 6. hovedforløb</strong></p>
          <ul class="check-list">
            <li><strong>Projektstyring.</strong> Ledelse, dokumentation og procesrapport</li>
            <li><strong>Projektorienteret arbejde.</strong> Ansvar for udviklingsopgaver fra bunden</li>
          </ul>
        </div>
      </div>
    `,
  },
  {
    id: "h6",
    type: "skole",
    label: "H6",
    image: "h6.png",
  },
];
