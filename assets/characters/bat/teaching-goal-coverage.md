# Næsebladets undervisningskort og målpindene

Kortene følger en måling fra flagermusens parkeringskælder til en embedded løsning. Nummeret i tabellerne svarer til præfikset på kortets billedfil. Øvelserne er afgrænset til eget lab. De faglige mål er gengivet i forkortet form. [Se de fulde målpinde](https://maalpinde.lup.dk/) · [Officielt datasæt](https://maalpinde.lup.dk/api/uddannelser/1205/programmering?ikraft=latest).

**Teori** dækker begreber, valg og begrundelser. **Kode/bordøvelse** kan gennemføres med papir, testdata eller en lokal computer. **Lab** kræver en selvstændigt gennemført øvelse med udstyr, godkendt øvemiljø eller begge dele. Kortene forklarer og giver opgaver. Opfyldelse af praktiske mål vurderes på elevens løsning, test og dokumentation.

## 17682 · IoT og Embeddede systemer

| Nr. | Emne | Kort | Aktivitet og dokumentation |
| --- | --- | --- | --- |
| 1 | Formål med IoT og IIoT | 01 | Teori. Forklar forskellen på måling, behandling og handling. |
| 2 | Begreber, teknikker, standarder, modeller og processer | 01, 12 | Teori. Tegn en datakæde, og begrund en protokol. |
| 3 | Hvornår en løsning er IoT eller IIoT | 01 | Bordøvelse. Klassificér et lokalt display og en netværksløsning til produktion. |
| 4 | Teknologier og komponenter | 02, 06, 08 | Bordøvelse. Vælg sensor, styrekort, forbindelser og behandling ud fra datablad. |
| 5 | Risici ved IoT | 02, 16 | Bordøvelse. Beskriv konsekvensen af afbrudt netværk, falsk data eller mislykket opdatering. |
| 6 | Løsning af en konkret case | 02 | Udstyrslab. Byg og demonstrér parkeringsprototypen mod acceptkravene. |
| 7 | Integration med eksisterende produktionssystemer | 12, 13 | Teori og softwarelab. Definér datakontrakt og test en lokal modtager. Integration med det faktiske produktionssystem er et særskilt lab. |
| 8 | Anvendelsesmuligheder | 01 | Bordøvelse. Sammenlign parkering, plantekasse og produktionsbånd. |
| 9 | Byg og dokumentér en lille IoT-løsning | 02, 07, 12, 13 | Udstyrs- og softwarelab. Saml dokumenteret hardware, indikator og gateway. Gem kode og testresultat. |
| 10 | Valg af sensortyper | 03, 04, 05 | Teori og udstyrslab. Begrund signaltype og sensorvalg med måleresultater. |
| 11 | Kvalitet og gyldighed i analoge og digitale signaler | 03, 04, 05 | Udstyrslab. Sammenlign målinger med reference og log spredning, udfald og tidsgrænse. |
| 12 | Analyse af analoge, digitale, serielle og trådløse signaler | 03, 08, 11 | Udstyrslab. Dokumentér spænding, flanker, dekodet UART og Wi-Fi-linkkvalitet med egnet udstyr. |
| 13 | Valg af kommunikationsprotokol | 08, 12 | Teori og softwarelab. Vælg bus og netværksprotokol efter ledninger, datamodel og krav. |
| 14 | Analyse og afhjælpning af sikkerhedsrisici | 16 | Bordøvelse og godkendt lab. Skriv scenarier og kontrollér identitet, opdateringsgodkendelse og gendannelse. |
| 15 | Fejlfinding på kablet og trådløs kommunikation | 11 | Udstyrslab. Genskab en afbrydelse og kontrollér link, adresse og applikationssvar. |
| 16 | Designvejledninger og industristandarder | 02, 12, 16 | Teori. Begrund valg med datablad, OPC UA-specifikation og relevant OT-vejledning. |
| 17 | Komponenter, I/O, netværk, software og værktøjer | 06, 07, 08 | Bordøvelse og udstyrslab. Dokumentér pinout, strøm, logikspænding og softwareværktøj. |
| 18 | Byg en konkret embedded prototype | 02, 07, 10 | Udstyrslab. Saml sensor og indikator og afprøv grænserne for lysstyringen. |
| 19 | Styr I/O via systemets biblioteker | 07, 08 | Udstyrslab. Brug Arduino I/O og et sensorbibliotek, og kontrollér det elektriske signal. |
| 20 | Brug værktøjer til kommunikation og udvikling | 06, 08, 11 | Udstyrslab. Udvikl med Arduino IDE og undersøg data via seriel log og signalværktøj. |
| 21 | Operatorer, kontrolstrukturer, løkker og arrays | 09, 10 | Kodeøvelse. Test differencer, betingelser, løkker og samples i et array. |
| 22 | Integrér sensor og behandl signal før netværket | 04, 05, 09, 10, 13 | Udstyrs- og softwarelab. Registrér pulsen, validér, filtrér og publicér kun den aftalte datakontrakt. |
| 23 | Analysér input mod reference og reagér | 03, 05, 07, 10 | Udstyrslab. Sammenhold analogt og digitalt input med reference, og test handlingen ved afvigelse. |
| 24 | Test og fejlfind implementeringen | 09, 10, 11 | Kodeøvelse og udstyrslab. Log forventet og faktisk resultat ved grænseværdi, timeout og forbindelsesfejl. |

## 17683 · IoT og Embeddede Linux systemer

| Nr. | Emne | Kort | Aktivitet og dokumentation |
| --- | --- | --- | --- |
| 1 | Installér og konfigurér et embedded Linux-system | 14 | Linux-lab. Installér et officielt image og dokumentér første opstart og konfiguration. |
| 2 | Filsystem, standardmapper og shell | 14 | Linux-lab. Find konfiguration og logfiler og beskriv deres funktion. |
| 3 | Ekstern SSH- og SFTP-klient | 14 | Linux-lab. Kontrollér værtsnøglen, log ind via fx PuTTY og overfør en øvefil via fx WinSCP. |
| 4 | Installér, opdatér og fjern pakker | 14 | Linux-lab. Brug pakkestyringen i et klargjort øvemiljø og registrér ændringerne. |
| 5 | Brugere, profiler og rettigheder | 15 | Linux-lab. Begræns tjenestebrugerens rettigheder til de nødvendige ressourcer. |
| 6 | Udvikl en app til embedded Linux | 13, 15 | Linux-lab. Skriv, dokumentér og kør en gateway-app, der validerer sensorlinjer. |
| 7 | Bearbejd sensordata og send resultat til centralt system | 13, 15 | Udstyrs- og softwarelab. Indlæs lokal sensor, fx en intelligent sensor, og vis resultater hos en central øvemodtager. Gemte data giver en softwareøvelse for samme kæde. |
| 8 | Sikkerhedspraksis og opdateringsprocedurer | 15, 16 | Linux-lab og bordøvelse. Kør en begrænset tjeneste, beskriv troværdige kilder, og test opdatering og genoprettelse i et øvemiljø. |

## Udvalgt overlap med 16481 · Linux rettet mod server og embedded

| Nr. | Emne | Kort |
| --- | --- | --- |
| 1–2 | Generelt Linux-kendskab og shell | 14 |
| 5 og 7 | Programmeringsmiljø og et program til embedded Linux | 15 |
| 8 | Brug producent- og distributionsdokumentation | 14 |
| 9 | Dokumentér program, afhængigheder og driftsopsætning | 15 |

## Materialer til øvelserne

- Kort 03–11 kan kombineres i ét lab med styrekort, dokumenterede sensorer, sikre lavspændingskomponenter, målebånd, plan målflade og egnet måleudstyr. Kodeeksemplerne til GPIO bruger Arduino UNO R3. Databladet for det konkrete udstyr bestemmer forsyning, ben og signalniveauer.
- Kort 12–13 kan gennemføres med et isoleret øvenet eller en broker på loopback. Gemte sensordata kan bruges til at afprøve datakontrakten, køen og fejlscenarier.
- Kort 14–16 forudsætter en klargjort Linux-øveenhed eller virtuel maskine. Ved fysisk I/O, signeret firmware eller gendannelse skal udstyret understøtte det valgte scenarie.
