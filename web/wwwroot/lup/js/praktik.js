/** Praktikmålsoversigt — indhold fra Mercantec Notion-wiki */

export const praktik = {
  title: "Praktikmålsoversigt",
  lead:
    "Samlet oversigt over uddannelsen “Datatekniker med speciale i programmering” — skoleindhold og praktikmål mellem forløbene, baseret på MAGS’ praktikmålspjece.",
};

const img = (name, alt) =>
  `<figure class="praktik-fig"><img src="assets/praktik/${name}" alt="${alt}" loading="lazy" /></figure>`;

export const praktikHtml = `
<p>Her er en samlet praktikmålsoversigt over uddannelsen “Datatekniker med speciale i programmering”. Her finder I en oversigt over, hvad der generelt bliver brugt på hvert hovedforløb, samt vores forventninger om, hvad lærlingen lærer ude på lærepladsen.</p>
<p><a class="cta cta-ghost" href="assets/praktik/pjece.pdf" target="_blank" rel="noopener noreferrer">Download pjecen (PDF)</a>
<a class="cta cta-ghost" href="#/" style="margin-left:0.5rem">Tilbage til tidslinjen</a></p>

<h2>Praktik i virksomheden</h2>
<p>Praktikvejledning bruges som redskab til at sikre, at lærlingen opnår de praktikmål, som er beskrevet for uddannelsen. De officielle praktikmål er beskrevet nedenfor.</p>

<details class="praktik-details">
  <summary>Til vejledning: delpraktikmål mellem skoleopholdene</summary>
  <div class="table-wrap">
    <table>
      <thead>
        <tr><th>Nummer</th><th>Titel</th><th>Målpinde</th></tr>
      </thead>
      <tbody>
        <tr><td>16495</td><td>Programudvikling - App</td><td>Lærlingen kan foretage programudvikling af apps.</td></tr>
        <tr><td>12889</td><td>Programudvikling - (web)applikationer</td><td>Lærlingen kan foretage avanceret programudvikling af (web)applikationer.</td></tr>
        <tr><td>16494</td><td>Programudvikling - versionering</td><td>Lærlingen kan anvende versionsstyringsredskaber under programudvikling.</td></tr>
        <tr><td>12888</td><td>Programudvikling - it-systemer</td><td>Lærlingen kan foretage avanceret programudvikling af it-systemer.</td></tr>
        <tr><td>16662</td><td>Programudvikling - Test</td><td>Lærlingen kan med en test foretage kvalitetssikring af et program.</td></tr>
        <tr><td>22302</td><td>Sikkerhed - program- og softwareudvikling</td><td>Lærlingen kan implementere sikkerhedsløsninger i forbindelse med program- og softwareudvikling.</td></tr>
        <tr><td>12891</td><td>Programmering - embedded systemer</td><td>Lærlingen kan programudvikle til embedded systemer.</td></tr>
        <tr><td>16493</td><td>Programudvikling - projektstyring</td><td>Lærlingen kan programudvikle med standardiserede projektstyringsredskaber.</td></tr>
        <tr><td>12890</td><td>Programmering - databaseudvikling</td><td>Lærlingen kan designe, opbygge og implementere databaseløsninger.</td></tr>
      </tbody>
    </table>
  </div>
</details>

<h2 id="gf2">Grundforløb 2</h2>
${img("gf2.png", "Diagram for Grundforløb 2")}

<h3>Praktikmål for praktikperioden mellem GF2 og H1</h3>
${img("gf2-h1.png", "Praktikmål mellem GF2 og H1")}
<p>Sæt kryds i de praktikmål, som lærlingen har opnået i sin praktikperiode.</p>
<p><strong>Praktikmål for virksomheden generelt</strong> — lærlingen har opnået kompetencer, der åbner mulighed for at arbejde med følgende:</p>
<ul class="check-list">
  <li><strong>Programudvikling.</strong> Lærlingen har kendskab til programudvikling og til virksomhedens teknologier og struktur.</li>
</ul>
<p><strong>Forberedelse til 1. hovedforløb</strong> — lærlingen må gerne opnå begyndende kendskab til:</p>
<ul class="check-list">
  <li><strong>Versionsstyring.</strong> På H1 bruges GitHub til versionsstyring — virksomheden kan med fordel sætte lærlingen ind i egne værktøjer og grundprincipper.</li>
  <li><strong>Database.</strong> Databaser og SQL mødes første gang på H1 — det hjælper at vise virksomhedens struktur og valg inden da.</li>
  <li><strong>Motivation og nysgerrighed.</strong> H1 er lærlingens første hovedforløb efter valg af programmering — vi forventer ikke bredt kendskab, men at de møder motiveret og nysgerrige op.</li>
</ul>

<h2 id="h1">H1 — Programmering</h2>
${img("h1.png", "Oversigt over H1")}
<div class="cols">
  <div>
    <h3>Introforløb</h3>
    <ul>
      <li>Fokus på læringstaktikker, studieteknik og lærlingens rolle i undervisningen</li>
      <li>Første C#-opgave: lister, betingelser og brugerinput i konsolapplikationer</li>
    </ul>
    <h3>Objektorienteret programmering (OOP)</h3>
    <ul>
      <li>Klasser og objekter med egenskaber og metoder</li>
      <li>Genbrug, struktur, instanser, namespaces og adgangsniveauer</li>
    </ul>
    <h3>Versionsstyring og dokumentation</h3>
    <ul>
      <li>Gode git-vaner og kodekommentarer</li>
    </ul>
  </div>
  <div>
    <h3>Clientside-programmering</h3>
    <ul>
      <li>Webbaseret platform med moderne frontend/fullstack, fx Blazor</li>
    </ul>
    <h3>SQL og databaser</h3>
    <ul>
      <li>Struktureret data med SQL (SELECT, INSERT, UPDATE, DELETE, joins)</li>
      <li>SQL med .NET via ADO.NET som data access layer</li>
    </ul>
    <h3>Netværk I</h3>
    <ul>
      <li>Router/switch, SSH, VLAN, trunking, subnet og routing (IPv4/IPv6)</li>
      <li>Praktiske labs og afsluttende prøve</li>
    </ul>
  </div>
</div>

<h3>Praktikmål for praktikperioden mellem H1 og H2</h3>
${img("h1-h2.png", "Praktikmål mellem H1 og H2")}
<div class="cols">
  <div>
    <p><strong>Praktikmål for virksomheden generelt</strong></p>
    <ul class="check-list">
      <li><strong>(Web)applikationer.</strong> Enkle fullstack-løsninger med moderne frontend og backend.</li>
      <li><strong>Versionering.</strong> Forståelse for virksomhedens Git-arbejdsgange og værktøjer.</li>
      <li><strong>Databaseudvikling.</strong> Erfaring med databaser, typer og relevante værktøjer.</li>
    </ul>
  </div>
  <div>
    <p><strong>Forberedelse til 2. hovedforløb</strong></p>
    <ul class="check-list">
      <li><strong>Versionsstyring.</strong> Mere praktisk indsigt i professionel versionskontrol inden H2.</li>
      <li><strong>Frontend.</strong> Holde Blazor/frontend-færdigheder ved lige — simple komponenter og interaktion.</li>
      <li><strong>Database.</strong> Kendskab til hvordan virksomheden arbejder med data og evt. ORM.</li>
    </ul>
  </div>
</div>

<h2 id="h2">H2 — Programmering</h2>
${img("h2.png", "Oversigt over H2")}
<div class="cols">
  <div>
    <h3>Videregående objektorienteret programmering</h3>
    <ul>
      <li>Nedarvning, interfaces, override/overload og async</li>
      <li>SOLID og designmønstre som MVC</li>
    </ul>
    <h3>Databaseprogrammering med SQL og ORM</h3>
    <ul>
      <li>Relationsdatabaser, nøgler, normalisering og indekser</li>
      <li>Entity Framework Core</li>
    </ul>
    <h3>Udvikling af REST API’er med ASP.NET Core</h3>
    <ul>
      <li>CRUD-API’er, routing, controllere, DTO’er, fejlhåndtering og validering</li>
    </ul>
    <h3>Frontend-udvikling med SPA-framework</h3>
    <ul>
      <li>Komponenter, data binding, UI/UX, formularer og API-integration</li>
    </ul>
  </div>
  <div>
    <h3>Versionsstyring, dokumentation og samarbejde</h3>
    <ul>
      <li>Git/GitHub: branches, pull requests og releases</li>
      <li>README, ER-diagrammer og API-beskrivelser</li>
    </ul>
    <h3>Serveradministration og sikkerhed</h3>
    <ul>
      <li>Windows Server, Active Directory, backup, VPN og roller</li>
    </ul>
    <h3>IT Service Management og supportsystemer</h3>
    <ul>
      <li>1st level support og ITIL-inspirerede processer</li>
      <li>Simpelt ticketsystem med login, kategorier og status</li>
    </ul>
  </div>
</div>

<h3>Praktikmål for praktikperioden mellem H2 og H3</h3>
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

<h2 id="h3">H3 — Programmering</h2>
${img("h3.png", "Oversigt over H3")}
<div class="cols">
  <div>
    <h3>Serversideprogrammering</h3>
    <ul>
      <li>API’er i C# med modeller og DTO’er til CRUD på tværs af enheder/platforme</li>
      <li>Database via Entity Framework Core</li>
    </ul>
    <h3>GUI-2-programmering</h3>
    <ul>
      <li>Blazor eller andet SPA-framework</li>
      <li>Dashboard med API-integration til IoT-enhed</li>
    </ul>
    <h3>Softwaretest &amp; sikkerhed</h3>
    <ul>
      <li>Sikkerhed i API samt HTTPS</li>
      <li>Auth/login til dashboard og IoT, JWT og kryptering</li>
    </ul>
  </div>
  <div>
    <h3>Databaseprogrammering 3</h3>
    <ul>
      <li>ORM via Entity Framework Core som bindeled mellem backend og datalag</li>
    </ul>
    <h3>IoT og embedded systemer</h3>
    <ul>
      <li>C++ og Arduino — egen enhed med sensorer og aktuatorer</li>
      <li>Integration med egen API ift. sikkerhed, serverside og database</li>
    </ul>
  </div>
</div>

<h3>Praktikmål for praktikperioden mellem H3 og H4</h3>
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

<h2 id="h4">H4 — Programmering</h2>
${img("h4.png", "Oversigt over H4")}
<div class="cols">
  <div>
    <h3>Appprogrammering 1 og 2</h3>
    <ul>
      <li>Mobiludvikling i Flutter/Dart</li>
      <li>App 1: UI, state, komponenter og navigation</li>
      <li>App 2: samme principper med API-integration</li>
    </ul>
    <h3>Softwaretest og sikkerhed</h3>
    <ul>
      <li>Fortsættelse fra H3: HTTPS, auth/login i mobilapp, JWT og kryptering</li>
    </ul>
    <h3>IT-kravspecifikation</h3>
    <ul>
      <li>Casebeskrivelse (kundeperspektiv) og kravspecifikation (udviklerperspektiv)</li>
      <li>Arbejds-/tidsplan og projektstyring ud fra dokumenterne</li>
    </ul>
  </div>
  <div>
    <h3>Programmeringsmetodik</h3>
    <ul>
      <li>Agil metodik (fx Scrum): planlægning, roller og møder</li>
    </ul>
    <h3>Serversideprogrammering</h3>
    <ul>
      <li>Fortsættelse fra H3: API’er, DTO’er, CRUD og EF Core</li>
    </ul>
  </div>
</div>

<h3>Praktikmål for praktikperioden mellem H4 og H5</h3>
${img("h4-h5.png", "Praktikmål mellem H4 og H5")}
<div class="cols">
  <div>
    <p><strong>Praktikmål for virksomheden generelt</strong></p>
    <ul class="check-list">
      <li><strong>Softwaretest.</strong> Kvalitetssikring og test af programmer</li>
      <li><strong>App.</strong> Udviklet eller hjulpet med app i et framework (cross-platform eller native)</li>
      <li><strong>Sikkerhed.</strong> Indsigt i sikkerhedsløsninger i softwareudvikling</li>
    </ul>
  </div>
  <div>
    <p><strong>Forberedelse til 5. hovedforløb</strong></p>
    <ul class="check-list">
      <li><strong>Linux.</strong> Kendskab til Linux i virksomhedens systemlandskab</li>
      <li><strong>Projektstyring.</strong> Mindre opgaver med planlægning, opgavestyring eller dokumentation</li>
    </ul>
  </div>
</div>

<h2 id="h5">H5 — Programmering</h2>
${img("h5.png", "Oversigt over H5")}
<div class="cols">
  <div>
    <h3>Linux-server og systemadministration</h3>
    <ul>
      <li>Opsætning i virtualiseret miljø, brugere, SSH og terminal</li>
    </ul>
    <h3>Docker og containerisering</h3>
    <ul>
      <li>Docker Compose, images, volumes og netværk</li>
    </ul>
    <h3>IoT og embedded-enheder</h3>
    <ul>
      <li>Arduino m.m. — sensordata til backend via message brokers</li>
    </ul>
    <h3>Message Queue</h3>
    <ul>
      <li>AMQP, RabbitMQ og MQTT — beskeder uden HTTP</li>
    </ul>
  </div>
  <div>
    <h3>Big Data og TimescaleDB</h3>
    <ul>
      <li>Tidsseriedata, forespørgsler og visualisering</li>
    </ul>
    <h3>Avanceret JWT og kryptering</h3>
    <ul>
      <li>Public/private key JWT og sikker kommunikation mellem komponenter</li>
    </ul>
    <h3>CI/CD, overvågning og pålidelighed</h3>
    <ul>
      <li>Pipelines, logning, overvågning og deployment</li>
    </ul>
    <h3>Mini-svendeprøve</h3>
    <ul>
      <li>20 dages gruppeprojekt med fokus på proces og dokumentation</li>
    </ul>
  </div>
</div>

<h3>Praktikmål for praktikperioden mellem H5 og H6</h3>
${img("h5-h6.png", "Praktikmål mellem H5 og H6")}
<div class="cols">
  <div>
    <p><strong>Praktikmål for virksomheden generelt</strong></p>
    <ul class="check-list">
      <li><strong>Projektstyring.</strong> Deltagelse i udvikling med projektstyringsværktøjer</li>
      <li><strong>Embedded systemer.</strong> Programmering til embedded med Linux eller anden relevant platform</li>
    </ul>
  </div>
  <div>
    <p><strong>Forberedelse til 6. hovedforløb</strong></p>
    <ul class="check-list">
      <li><strong>Projektstyring.</strong> Øvelse i ledelse, dokumentation og procesrapport inden svendeprøven</li>
      <li><strong>Projektorienteret arbejde.</strong> Ansvar for udviklingsopgaver fra bunden — alene eller i mindre grupper</li>
    </ul>
  </div>
</div>

<h2 id="h6">H6 — Programmering | Svendeprøven</h2>
${img("h6.png", "Oversigt over H6 / svendeprøven")}
<div class="cols">
  <div>
    <h3>Svendeprøve</h3>
    <ul>
      <li>20 dage til et færdigt produkt</li>
      <li>Lærlingens egen casebeskrivelse som kundevinkel</li>
      <li>Produkt- og processrapport + mundtlig fremlæggelse</li>
    </ul>
    <h3>Systemudvikling</h3>
    <ul>
      <li>Hele processen fra idé til implementering</li>
      <li>Planlægning, dokumentation, præsentation og refleksion</li>
    </ul>
  </div>
  <div>
    <h3>Projektstyring</h3>
    <ul>
      <li>Niveau svarende til PRINCE2: roller, faser, risiko, kvalitet og ledelsesdokumenter</li>
    </ul>
    <h3>Eksamen</h3>
    <ul>
      <li>Individuel eksamen med forsvar af egne bidrag</li>
      <li>Selvvalgt fagligt emne med teoretisk dybde</li>
    </ul>
  </div>
</div>
`;
