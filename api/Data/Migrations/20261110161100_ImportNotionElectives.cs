using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mercantec.Tech.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261110161100_ImportNotionElectives")]
public sealed class ImportNotionElectives : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // One-time import of Mercantec's public Notion catalogue, including the 2023 PDF timetable.
        migrationBuilder.Sql("""
            INSERT INTO "electives"
                ("Id", "Title", "Description", "Schedule", "StartsOn", "EndsOn", "Capacity", "ReservedSeats", "Status", "SourceUrl", "TemplateId", "Version")
            VALUES
            ('28bdab5c-a237-80a6-a76f-e538cef2e5d3', 'Deploy Or Die - Fra kode til internet', 'Deploy or Die er en Valgfags pakke, sammensat af følgende valgfag

            DevNet og Informationssikkerhed og awareness

            I kan finde de konkrete forløb her

            MAGS: Deploy or Die – Fra kode til internet', 'Varighed: 15 dage
            Anbefales efter: H2
            I rotation i den oprindelige oversigt', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/28bdab5ca23780a6a76fe538cef2e5d3', NULL, 1),
            ('b219ccf5-5b8a-48a3-8052-7c06216eadc4', 'Game Design', 'Game design består af 2 valgfag, vi anbefaler at tage det sammen!

            Målpinde for Game design 1 og 2

            Game design I - 15937

            1. Lærlingen kan redegøre for udviklingsfilosofien prototyping og usabilitytest
            2. Lærlingen kan ved hjælp af en game-engine som f.eks. Unity, JMonkeyEngine eller lign. implementere et simpelt 2D spil
            3. Lærlingen kan udarbejde flere level design
            4. Lærlingen kan implementere flere scener og skifte mellem disse
            5. Lærlingen kan implementere en "Player" som kan styres ved hjælp af input - som f.eks. tastetur
            6. Lærlingen kan håndtere event-styring
            7. Lærlingen kan implementere konfigurationsfiler og gemme data - fx en highscore-liste

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=207070

            Game design II - 15938

            1. Lærlingen kan beskrive og planlægge et simpelt multiplayer 3D spil
            2. Lærlingen kan implementere en simpel AI
            3. Lærlingen har kendskab til "flocking behaviour" og de 3 regler for flocking - Cohesion, alignment, Seperation
            4. Lærlingen kan implementere en eller flere tilstandsmaskiner.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=207071

            Forløb for hvert forløb

            KASC - Forestil dig, at du skal bygge noget helt fra bunden. Normalt er du nok vant til at kode løsninger til industrien, hvor alt skal være præcist og effektivt. Men hvad hvis det, du bygger, i stedet skal være sjovt? Hvad hvis “brugeren” ikke skal udføre en opgave – men underholdes? 🎮Det er dér, game design kommer ind i billedet. -', 'Varighed: 10 dage
            Anbefales efter: H3
            Fagnummer: 15937
            I rotation i den oprindelige oversigt', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/b219ccf55b8a48a380527c06216eadc4', NULL, 1),
            ('9a0e18a7-983b-499d-8e17-3a797ce0624c', 'Machine Learning', 'Målpinde for Machine Learning - 17348

            1. Lærlingen kan redegøre for, hvordan en maskine kan lære.
            2. Lærlingen kan anvende maskinlæring til at løse praktiske opgaver.
            3. Lærlingen kan samle og forberede data til maskinlæring.
            4. Lærlingen kan redegøre for forskellige maskinlæringsmetoder.
            5. Lærlingen kan træne og teste sin model.
            6. Lærlingen kan anvende klassificering og regression til at løse praktiske opgaver.
            7. Lærlingen kan anvende et maskinlærings-framework til at løse en praktisk opgave.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=218782

            Lige nu udbydes valgfaget af følgende

            MAGS - Machine Learning

            Beate Louise Jordening -', 'Varighed: 10 dage
            Anbefales efter: H3
            Fagnummer: 17348
            I rotation i den oprindelige oversigt', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/9a0e18a7983b499d8e173a797ce0624c', NULL, 1),
            ('32dd137e-3eea-4a94-b92e-5809c99a4e0e', 'User Interface Design', 'Målpinde for User Interface Design

            1. Lærlingen kan anvende relevante værktøjer i designudviklingen af et User Interface, udvikle wireframes, flowcharts, prototypes og mockups samt foretage opgaveanalyser og brugersegmenteringer.
            2. Lærlingen kan redegøre for faserne i et User Interface Design.
            3. Lærlingen kan brugersegmentere og på den baggrund definere funktionalitetskrav.
            4. Lærlingen kan udarbejde en opgaveanalyse af systemet og User Interfacet.
            5. Lærlingen opnår en grundlæggende viden om informationsarkitektur, herunder design af mobile apps og andre former for kommunikationsplatforme.
            6. Lærlingen kan udarbejde relevante wireframes af indholdsblokke, som beskrivelse og grundlæggende visualisering af et User Interface.
            7. Lærlingen kan udarbejde et relevant flowchart, dvs. et rutediagram for den visuelle repræsentation af f.eks. sekvensen i en navigationsproces.
            8. Lærlingen kan udarbejde en relevant prototype, og udføre en gennemgående usability test af prototypen.
            9. Lærlingen kan udarbejde et mockup, der i en statisk form demonstrerer projektets visuelle side i henhold til information og funktionalitet.
            10. Lærlingen får igennem et case forløb praktisk erfaring i udviklingen af wireframes, flowcharts, prototypes og mockups.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=219491

            Forløb for hvert forløb

            PEJO - User Interface Design', 'Varighed: 10 dage
            Anbefales efter: H1
            Fagnummer: 17548', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/32dd137e3eea4a94b92e5809c99a4e0e', NULL, 1),
            ('e7a404ad-39a4-4d11-abfe-b4ec8225b26c', 'Objekt Orienteret Analyse og Design', 'OOAD, har følgende målpinde

            Målpinde for OOAD 17334

            1. Lærlingen har grundlæggende viden om indhold og faser i objektorienteret analyse og design.
            2. Lærlingen kan analysere problemområdet, hvad der skal ændres og hvilke arbejdsgange, der berøres (ændres/fjernes/nye).
            3. Lærlingen kan identificere og definere klasser i problemområdet.
            4. Lærlingen kan identificere og definere hændelser/handlinger i problemområdet.
            5. Lærlingen har grundlæggende viden om kodekonventioner i forbindelse med navngivning af klasseobjekter m.m.
            6. Lærlingen kan udarbejde relevante diagrammer efter UML standarden.
            7. Lærlingen har kendskab til Rige Billeder, Use Case/User stories, tilstandsdiagrammer, flowchart/state diagrammer eller tilsvarende diagrammering af adfærdsmønstre/handlinger i problem- og/eller anvendelsesområdet.
            8. Lærlingen har kendskab til pseudokode og brug af Wireframe/navigationsdiagram i forbindelse med præsentation af design løsninger.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=217536

            Forløb for hvert forløb

            MAGS - Objekt Orienteret Analyse og Design', 'Varighed: 5 dage
            Anbefales efter: H1
            Fagnummer: 17334', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/e7a404ad39a44d11abfeb4ec8225b26c', NULL, 1),
            ('973e6901-e477-4d44-aa1d-356f9ed5681f', 'ASP.NET og XML', 'ASP.NET bruger vi allerede meget på hoved forløbene, men XML har i højst sandsynligt ikke arbejdet med før

            Målpindene for valgfaget findes her

            1. Lærlingen kan installere og tilpasse nødvendig software, der anvendes i forbindelse afviklingen af ASP.NET programmer.
            2. Lærlingen kan anvende ASP.NET sprogets klassehieraki, metoder, variable og datatyper.
            3. Lærlingen kan anvende ASP.NET sprogets objekthierarkiet.
            4. Lærlingen kan fremstille ASP:NET programmer der gør brug af databaser.
            5. Lærlingen kan anvende XML i forbindelse med udvikling af hjemmesider.
            6. Lærlingen kan anvende XML i forhold til databaser.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=158897

            Følgende underviser i valgfaget lige nu

            Azeem Umar - azum@mercantec.dk', 'Varighed: 5 dage
            Anbefales efter: H1
            Fagnummer: 1644', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/973e6901e4774d44aa1d356f9ed5681f', NULL, 1),
            ('934ef0eb-d654-49e6-813a-abe1fb2bcb88', 'DevNet', 'DevNet har følgende målpinde

            20738 - DevNet - 10 dage

            1. Lærlingen kan benytte basis Python programmeringscript
            2. Lærlingen kan benytte basis Linux shell-kommandoer
            3. Lærlingen kan implementere og udvikle simple DevNet-miljøer (API-kald).
            4. Lærlingen kan bruge gældende standarder til at udvikle DevNet-miljøer.
            5. Lærlingen kan oprette API-forespørgsler via sikre protokoller f.eks. HTTPs.
            6. Lærlingen kan forklare grundlæggende netværksbegreber.
            7. Lærlingen kan benytte værktøjer til at deploye og sikre data i et cloudmiljø

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=225258', 'Varighed: 10 dage
            Anbefales efter: H3
            Fagnummer: 20738', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/934ef0ebd65449e6813aabe1fb2bcb88', NULL, 1),
            ('1303930e-1077-49e2-a3a2-f0c7d02fddd7', 'Big Data analyse og databehandling', 'I har 2 big-data fag på jeres H5, nemlig Big Data begreber, teknikker og processer og Big Data modeller og datamodellering. Der er dog et 3. Big-Data fag som ikke obligatorisk men derimod et valgfag. Det kan i se målpindene her

            1. Lærlingen kan, ud fra en case-opgave, anvende gængse principper for dataanalyse og databehandling. Med udgangspunkt i eksisterende dataset kan lærlingen demonstrere viden, færdigheder i de i faget beskrevne mål og målniveauer.
            2. Lærlingen kan beskrive og anvende grundlæggende principper i data mining f.eks. detektere korrelationer og anomalier.
            3. Lærlingen kan forklare muligheder og problemstillinger ved at aggregere data, herunder grundlæggende statistiske begreber.
            4. Lærlingen kan udvælge det rette data til at løse et givent problem, og udtrække et datasæt fra multiple datakilder.
            5. Lærlingen kan udføre dataanalyser med værktøjer som f.eks. predictive analytics, f.eks. Pandas, SciPy, Scikit Learn, Tensorflow, Pytorch, for at finde eventuelle tendenser i data, samt vurdere kvaliteten deraf.
            6. Lærlingen har kendskab til, hvorfor det kan være vigtigt at designe efter parallelitet i krævende data analyser, f.eks Pandas, Dask, Tensorflow (tf.distribute.Strategy).
            7. Lærlingen har kendskab til, hvordan der skabes reproducerbare analyser f.eks. Dependency management, DVC og pachyderm.
            8. Lærlingen har kendskab til forskellige måder at visualisere og præsentere data, med f.eks. heatmaps, barplots, scatterplots, pieplot, swarmplots, relplots, boxplots, distributional summaries med værktøjer såsom Matplotlib, Seaborn, PowerBI.
            9. Lærlingen kan, ud fra en konkret opgave, designe, opbygge og anvende et mindre system, der kan opsamle, analysere, behandle og præsentere data fra forskellige datakilder.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=222703

            Følgende underviser i valgfaget lige nu', 'Varighed: 5 dage
            Anbefales efter: H5
            Fagnummer: 20557', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/1303930e107749e2a3a2f0c7d02fddd7', NULL, 1),
            ('f9ac4206-325b-42d3-95cd-2a0c7a5b3219', 'Core Applications', 'Core Applications strækker sig over 5 dage og dækker generelt over strukturen over en 3-lags  applikationsmodel. Det vil sige en clientside, en server og til sidste en database. Målpindene for faget kan findes her

            1. Lærlingen kan beskrive strukturen for en 3-lags applikationsmodel og dens fordele.
            2. Lærlingen kan implementere en database på op til 10 tabeller, på baggrund af et E/R-diagram.
            3. Lærlingen kan oprette Stored Procedures, som kan søge, indsætte, opdatere og slette data i databasen.
            4. Lærlingen kan implementere Data Access Layer klasserne og deres metoder/parametre.
            5. Lærlingen kan implementere Business Logic Layer klasserne og deres metoder/parametre.
            6. Lærlingen kan foretage logisk og struktureret fejlfinding på applikations datalag.
            7. Lærlingen kan betjene et version-styringsystem i forbindelse med et udviklingsteam.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=169993

            Følgende underviser i valgfaget lige nu

            MAGS - Core Applications', 'Varighed: 5 dage
            Anbefales efter: H1
            Fagnummer: 6295', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/f9ac4206325b42d395cd2a0c7a5b3219', NULL, 1),
            ('5b9982a8-060f-4f8a-bd23-737dabda90dd', 'Data og Machine Learning', 'Data og Machine Learning er en valgfagspakke som samler 3 enkelte valgfag, nemlig Introduktion til Python, Big Data analyse og databehandling og Machine Learning!

            Derfor er målpindende også de samme som for de enkelte

            Målpinde

            Målpinde fra Introduktion til Python

            1. Eleven kan installere en Python interpreter og bruge den på en lokale maskine
            2. Eleven kan installere pakker med PIP eller lignende pakkeværktøj
            3. Eleven kan opsætte et virtuelt miljø (VENV)
            4. Eleven kan hente data fra en database, API eller et lokalt dokument
            5. Eleven kan anvende løkker, funktioner og forskellige kontrolstrukturer i Python
            6. Eleven kan anvende et databehandlings værktøj såsom NumPy og et værktøj til at administrere arrays og matrixer, såsom Pandas med Dataframes
            7. Eleven kan anvende et værktøj til at lave grafer, diagram over data i Python, såsom MatPlotLib eller Seaborn

            Målpinde fra Big Data analyse og databehandling

            1. Lærlingen kan, ud fra en case-opgave, anvende gængse principper for dataanalyse og databehandling. Med udgangspunkt i eksisterende dataset kan lærlingen demonstrere viden, færdigheder i de i faget beskrevne mål og målniveauer.
            2. Lærlingen kan beskrive og anvende grundlæggende principper i data mining f.eks. detektere korrelationer og anomalier.
            3. Lærlingen kan forklare muligheder og problemstillinger ved at aggregere data, herunder grundlæggende statistiske begreber.
            4. Lærlingen kan udvælge det rette data til at løse et givent problem, og udtrække et datasæt fra multiple datakilder.
            5. Lærlingen kan udføre dataanalyser med værktøjer som f.eks. predictive analytics, f.eks. Pandas, SciPy, Scikit Learn, Tensorflow, Pytorch, for at finde eventuelle tendenser i data, samt vurdere kvaliteten deraf.
            6. Lærlingen har kendskab til, hvorfor det kan være vigtigt at designe efter parallelitet i krævende data analyser, f.eks Pandas, Dask, Tensorflow (tf.distribute.Strategy).
            7. Lærlingen har kendskab til, hvordan der skabes reproducerbare analyser f.eks. Dependency management, DVC og pachyderm.
            8. Lærlingen har kendskab til forskellige måder at visualisere og præsentere data, med f.eks. heatmaps, barplots, scatterplots, pieplot, swarmplots, relplots, boxplots, distributional summaries med værktøjer såsom Matplotlib, Seaborn, PowerBI.
            9. Lærlingen kan, ud fra en konkret opgave, designe, opbygge og anvende et mindre system, der kan opsamle, analysere, behandle og præsentere data fra forskellige datakilder.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=222703

            Målpinde fra Machine Learning

            1. Lærlingen kan redegøre for, hvordan en maskine kan lære.
            2. Lærlingen kan anvende maskinlæring til at løse praktiske opgaver.
            3. Lærlingen kan samle og forberede data til maskinlæring.
            4. Lærlingen kan redegøre for forskellige maskinlæringsmetoder.
            5. Lærlingen kan træne og teste sin model.
            6. Lærlingen kan anvende klassificering og regression til at løse praktiske opgaver.
            7. Lærlingen kan anvende et maskinlærings-framework til at løse en praktisk opgave.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=218782

            Følgende underviser i valgfaget lige nu

            MAGS -', 'Varighed: 20 dage
            Anbefales efter: H4 / H5
            Fagnummer: 17348', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/5b9982a8060f4f8abd23737dabda90dd', NULL, 1),
            ('c85d855d-c05d-4c83-9691-7d345fc3ac68', 'Grundlæggende Programmering', 'Grundlæggende Programming strækker sig over 10 dage. Programmør speciallet har det som standart på deres H1, men infrastruktur og IT-supporter har mulighed for at tage det som valgfag!

            Faget har 12 målpinde som findes her!

            1. Eleven kan anvende et programmeringssprog til at udarbejde mindre
            konsolprogrammer, herunder fejlfinde og teste dem.
            2. Eleven kan vælge en hensigtsmæssig datatype til en opgave.
            3. Eleven kan oprette og gøre brug af både enkelt- og multidimensionelle arrays.
            4. Eleven kan erklære og anvende simple metoder.
            5. Eleven kan diagrammere sit arbejde.
            6. Eleven kan benytte forskellige kontrolstrukturer.
            7. Eleven kan erklære og bruge variable samt konstanter.
            8. Eleven kan redegøre for brugen af kodekommentar.
            9. Eleven kan versionsstyre sit arbejde.
            10. Eleven kan benytte et debugging-værktøj.
            11. Eleven kan vælge hensigtsmæssige datatyper til variable og metoder.
            12. Eleven kan erklære og oprette metoder i et objekt med og uden returværdi og
            parametre, samt gøre brug af disse.

            Officielle fagmål: https://www.eud.uddannelsesadministration.dk/Soeg/EUDEnkeltfag/Detaljer.aspx?FAG_ID=210129

            Følgende underviser i valgfaget lige nu

            MAGS - Grundlæggende Programmering - C#

            MAGS - Grundlæggende Programmering - Python', 'Varighed: 10 dage
            Anbefales efter: GF2
            Fagnummer: 16471', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/c85d855dc05d4c8396917d345fc3ac68', NULL, 1),
            ('2638af74-b36d-4537-99f0-6e0f0cf46fe8', 'Introduktion til Python', 'Vi er i gang, sammen med Industriens Uddannelser - [iu.dk](http://iu.dk), at udvikle et valgfag som dækker nogle generelle koncepter under Python, såsom venv, pakker og meget mere. Det er stadig på udviklingsstadiet og udbydes derfor ikke.

            Målpinde

            1. Eleven kan installere en Python interpreter og bruge den på en lokale maskine
            2. Eleven kan installere pakker med PIP eller lignende pakkeværktøj
            3. Eleven kan opsætte et virtuelt miljø (VENV)
            4. Eleven kan hente data fra en database, API eller et lokalt dokument
            5. Eleven kan anvende løkker, funktioner og forskellige kontrolstrukturer i Python
            6. Eleven kan anvende et databehandlings værktøj såsom NumPy og et værktøj til at administrere arrays og matrixer, såsom Pandas med Dataframes
            7. Eleven kan anvende et værktøj til at lave grafer, diagram over data i Python, såsom MatPlotLib eller Seaborn

            Følgende underviser i valgfaget lige nu

            MAGS - Python - Valgfag', 'Varighed: 5 dage
            Anbefales efter: H2', NULL, NULL, NULL, 0, 'template', 'https://mercantec.notion.site/2638af74b36d453799f06e0f0cf46fe8', NULL, 1);
            """);

        migrationBuilder.Sql("""
            INSERT INTO "electives"
                ("Id", "Title", "Description", "Schedule", "StartsOn", "EndsOn", "Capacity", "ReservedSeats", "Status", "SourceUrl", "TemplateId", "Version")
            VALUES
            ('8b69f861-3b70-549b-9ecb-a36240122748', 'Deploy or Die – Fra kode til internet', 'Forløbet kombinerer DevNet og Informationssikkerhed og awareness.', 'Uger: 24, 25, 26
            Varighed: 3 Uger
            Undervisningsform: Online
            Underviser: Mathias (MAGS)
            Speciale: Programmering
            Tilmeldinger i det oprindelige katalog: 57', DATE '2026-06-08', DATE '2026-06-26', 100, 0, 'archived', 'https://mercantec.notion.site/1b1dab5ca2378052891efbc3e530a844', '28bdab5c-a237-80a6-a76f-e538cef2e5d3', 1),
            ('2e0e1d7e-7f6c-57a1-8ddd-9e3b39eb4ba8', 'Game Design', 'Game Design I og II udbydes samlet som et treugers hold.', 'Uger: 39, 40, 41
            Varighed: 3 Uger
            Undervisningsform: Online
            Underviser: Kasper (KASC)
            Speciale: Programmering
            Tilmeldinger i det oprindelige katalog: 9', DATE '2026-09-21', DATE '2026-10-09', 30, 0, 'archived', 'https://mercantec.notion.site/1b1dab5ca2378052891efbc3e530a844', 'b219ccf5-5b8a-48a3-8052-7c06216eadc4', 1),
            ('53127dea-6020-5b8d-92d9-690c2c53225e', 'Machine Learning', 'Onlinehold i Machine Learning for datateknikere med speciale i programmering.', 'Uger: 7,8,9
            Varighed: 3 Uger
            Undervisningsform: Online
            Underviser: Mathias (MAGS)
            Speciale: Programmering
            Fra valgfagspakken for 2026', DATE '2027-02-15', DATE '2027-03-05', 16, 8, 'closed', 'https://mercantec.notion.site/1b1dab5ca2378052891efbc3e530a844', '9a0e18a7-983b-499d-8e17-3a797ce0624c', 1),
            ('66d872e6-dd4d-52c9-a566-fafffbeafcda', 'Machine Learning', 'Forløbet arbejdede med dataanalyse og maskinlæring i Python. Eleverne prøvede blandt andet klassifikation og regression og afsluttede med et projekt.', 'Uger: 8-9
            Varighed: 10 dage
            Underviser: MAGS
            Speciale: Programmering
            Anbefales efter: H4 / H5', DATE '2025-02-17', DATE '2025-02-28', NULL, 0, 'archived', 'https://mercantec.notion.site/12bdab5ca23780a3bf91dbc5723ce863', '9a0e18a7-983b-499d-8e17-3a797ce0624c', 1),
            ('cd6904ab-eda5-5ccb-b414-94a9d5a1ce33', 'Machine Learning | Kun fysisk', '', 'Uger: 37-38
            Varighed: 10 Dage
            Undervisningsform: Fysisk
            Underviser: Beate (BELJ)
            Speciale: Programmering
            Anbefales efter: H2', DATE '2025-09-08', DATE '2025-09-19', NULL, 0, 'archived', 'https://mercantec.notion.site/12bdab5ca23780a3bf91dbc5723ce863', '9a0e18a7-983b-499d-8e17-3a797ce0624c', 1),
            ('c01f8467-ca1d-50ee-9938-962adb9ae7dd', 'Game Design (3 ugers)', '', 'Uger: 43, 44 & 45
            Varighed: 15 dage
            Underviser: Kasper (KASC)
            Speciale: Programmering
            Anbefales efter: H1', DATE '2025-10-20', DATE '2025-11-07', NULL, 0, 'archived', 'https://mercantec.notion.site/12bdab5ca23780a3bf91dbc5723ce863', 'b219ccf5-5b8a-48a3-8052-7c06216eadc4', 1),
            ('3f3ddbbf-8fb9-5962-8ae4-59d784d63d52', 'Machine Learning', '', 'Uger: 2-3
            Varighed: 10 dage
            Underviser: MAGS
            Speciale: Programmering', DATE '2024-01-08', DATE '2024-01-19', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', '9a0e18a7-983b-499d-8e17-3a797ce0624c', 1),
            ('52233665-a6e6-5a30-8506-4cd711b5a818', 'User Interface Design', '', 'Uger: 10-11
            Varighed: 10 dage
            Underviser: CHPE
            Speciale: Programmering', DATE '2024-03-04', DATE '2024-03-15', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', '32dd137e-3eea-4a94-b92e-5809c99a4e0e', 1),
            ('aa43a052-9723-53d4-b315-0023e6e62575', 'Gateway Sikkerhed', '', 'Uge: 11
            Varighed: 5 dage
            Underviser: OCHR
            Speciale: Infrastruktur / Programmering', DATE '2024-03-11', DATE '2024-03-15', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('e83aaa41-b532-5dbf-b8c7-c97eba34e682', 'Core Applications', '', 'Uge: 12
            Varighed: 5 dage
            Underviser: CHPE
            Speciale: Programmering', DATE '2024-03-16', DATE '2024-03-22', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', 'f9ac4206-325b-42d3-95cd-2a0c7a5b3219', 1),
            ('790f1cd8-a044-5cea-80a5-7dc996bb7eb7', 'Sikkerhed Firewall', '', 'Uge: 12
            Varighed: 5 dage
            Underviser: OCHR
            Speciale: Infrastruktur', DATE '2024-03-16', DATE '2024-03-22', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('ff8f6453-7094-599f-98a4-f0527363f58b', 'Grundlæggende Programmering', '', 'Uger: 23-25
            Varighed: 10 dage
            Underviser: MAGS
            Speciale: Infrastruktur', DATE '2024-06-07', DATE '2024-06-20', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', 'c85d855d-c05d-4c83-9691-7d345fc3ac68', 1),
            ('908e8e9b-e0aa-5b45-92f3-492f4ca51965', 'Software defined networking', '', 'Uge: 24
            Varighed: 5 dage
            Underviser: OCHR
            Speciale: Infrastruktur', DATE '2024-06-10', DATE '2024-06-14', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('044d863e-ad9e-5a90-bcbf-53cb9f53eeaa', 'Objekt Orienteret Analyse og Design', '', 'Uge: 24
            Varighed: 5 dage
            Underviser: CHPE
            Speciale: Programmering', DATE '2024-06-10', DATE '2024-06-14', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', 'e7a404ad-39a4-4d11-abfe-b4ec8225b26c', 1),
            ('75a77db9-3d1a-5ee3-bcfc-b2187cb15fa1', 'Core Applications', '', 'Uge: 32
            Varighed: 5 dage
            Underviser: MAGS
            Speciale: Programmering', DATE '2024-08-05', DATE '2024-08-09', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', 'f9ac4206-325b-42d3-95cd-2a0c7a5b3219', 1),
            ('e6a1451f-d618-5470-a305-723214103590', 'Cybersecurity operations', '', 'Uger: 32-33
            Varighed: 10 dage
            Underviser: Peter Berg
            Speciale: Infrastruktur', DATE '2024-08-05', DATE '2024-08-16', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('2a1ba0fd-76bf-5da3-a1cb-210b744a2dcb', 'Mailserver I Windows org.', '', 'Uge: 34
            Varighed: 5 dage
            Underviser: Azeem
            Speciale: Infrastruktur / Programmering', DATE '2024-08-19', DATE '2024-08-23', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('94f7558d-0856-51a7-8817-6a262d8e9685', 'Server Migration', '', 'Uger: 35-36
            Varighed: 10 dage
            Underviser: Azeem
            Speciale: Infrastruktur / Programmering', DATE '2024-08-26', DATE '2024-09-06', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('31afbac3-9f82-57a7-98fd-ee7f8496b5cd', 'Sikkerhed Firewall (efter H4)', '', 'Uge: 40
            Varighed: 5 dage
            Underviser: OCHR
            Speciale: Infrastruktur', DATE '2024-09-30', DATE '2024-10-04', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('60d342c0-57ad-557d-9543-a2c4e12bc67e', 'ASP.NET og XML', '', 'Uge: 44
            Varighed: 5 dage
            Underviser: Christian Lyllof
            Speciale: Infrastruktur / Programmering', DATE '2024-10-28', DATE '2024-11-01', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', '973e6901-e477-4d44-aa1d-356f9ed5681f', 1),
            ('3c2d8b52-b697-50bb-bda5-d80257706331', 'Design af datacenterløsninger', '', 'Uge: 45
            Varighed: 5 dage
            Underviser: Azeem
            Speciale: Infrastruktur', DATE '2024-11-04', DATE '2024-11-08', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', NULL, 1),
            ('b45c6e2e-7f92-5a68-aba5-212826c446f4', 'User Interface Design', '', 'Uger: 47-48
            Varighed: 10 dage
            Underviser: Peter Jones
            Speciale: Programmering', DATE '2024-11-18', DATE '2024-11-29', NULL, 0, 'archived', 'https://mercantec.notion.site/54d8024fb98243338494975671cd54c8', '32dd137e-3eea-4a94-b92e-5809c99a4e0e', 1),
            ('7c9ebab1-567c-5ce0-abf3-ac66deb85679', 'Cybersecurity operations (efter H2)', '', 'Uger: 4-5
            Varighed: 2 uger
            Speciale: Infrastruktur
            Fagnummer: 17509', DATE '2023-01-23', DATE '2023-02-03', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('33b298c5-7627-5c06-8cfb-9859dbcf59ff', 'Cloudteknologi (efter H2)', '', 'Uge: 6
            Varighed: 1 uge
            Speciale: IT-supporter / Programmering
            Fagnummer: 16875', DATE '2023-02-06', DATE '2023-02-10', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('a66dd2f9-04fb-5920-b043-1b3a2ad653aa', 'Design af datacenterløsning (efter H2)', '', 'Uge: 7
            Varighed: 1 uge
            Speciale: IT-supporter / Infrastruktur
            Fagnummer: 1600', DATE '2023-02-13', DATE '2023-02-17', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('885871ad-f4cb-5f14-8a99-c1d137eba32f', 'Sikkerhed IV (efter H4)', '', 'Uge: 8
            Varighed: 1 uge
            Speciale: Infrastruktur
            Fagnummer: 2149', DATE '2023-02-20', DATE '2023-02-24', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('121a92ad-b003-5666-8e97-99c8de139227', 'User Interface Design', '', 'Uger: 8-9
            Varighed: 2 uger
            Speciale: Programmering
            Fagnummer: 17548', DATE '2023-02-20', DATE '2023-03-03', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', '32dd137e-3eea-4a94-b92e-5809c99a4e0e', 1),
            ('edd591a2-ad55-5988-8888-01309d57a943', 'Gateway Sikkerhed', '', 'Uge: 10
            Varighed: 1 uge
            Speciale: IT-supporter / Infrastruktur / Programmering
            Fagnummer: 6245', DATE '2023-03-06', DATE '2023-03-10', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('b6c29712-12e1-55f4-ab93-46d552a3de3a', 'Core applications', '', 'Uge: 10
            Varighed: 1 uge
            Speciale: Programmering
            Fagnummer: 6295', DATE '2023-03-06', DATE '2023-03-10', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', 'f9ac4206-325b-42d3-95cd-2a0c7a5b3219', 1),
            ('f8e0b28f-4b30-5b27-97d9-6dd80c359ddc', 'Sikkerhed Firewall (efter H4)', '', 'Uge: 11
            Varighed: 1 uge
            Speciale: Infrastruktur
            Fagnummer: 17510', DATE '2023-03-13', DATE '2023-03-17', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('32251b1b-f6cb-5849-975f-f8f4b77219f5', 'Servermigration (efter H2)', '', 'Uger: 12-13
            Varighed: 2 uger
            Speciale: IT-supporter / Infrastruktur
            Fagnummer: 10540', DATE '2023-03-20', DATE '2023-03-31', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('8fe0db35-afd1-5367-9ac0-5b58bf4cdc81', 'Software defined networking (efter H2)', '', 'Uge: 17
            Varighed: 1 uge
            Speciale: Infrastruktur
            Fagnummer: 17640', DATE '2023-04-24', DATE '2023-04-28', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('e0867a67-55a3-5504-836a-fb02f87ef1a9', 'Objektorienteret analyse/design (eft. H2)', '', 'Uge: 24
            Varighed: 1 uge
            Speciale: Programmering
            Fagnummer: 17334', DATE '2023-06-12', DATE '2023-06-16', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('88526d55-19e7-5c3f-a05f-d6456941c19e', 'Fiberinstallation', '', 'Uge: 32
            Varighed: 1 uge
            Speciale: IT-supporter / Infrastruktur / Programmering
            Fagnummer: 1590', DATE '2023-08-07', DATE '2023-08-11', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('2b2b8f95-5b77-5fca-8a44-66896dd4e448', 'Netværksdesign II (efter H4)', '', 'Uger: 33-34
            Varighed: 2 uger
            Speciale: Infrastruktur
            Fagnummer: 1595', DATE '2023-08-14', DATE '2023-08-25', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('674fd2ec-7289-58fe-a712-ecad317ffbae', 'Gateway Sikkerhed', '', 'Uge: 36
            Varighed: 1 uge
            Speciale: IT-supporter / Infrastruktur / Programmering
            Fagnummer: 6245', DATE '2023-09-04', DATE '2023-09-08', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('c11f3f63-4b77-5215-a3c3-c4193ae8d65a', 'Network management', '', 'Uge: 40
            Varighed: 1 uge
            Speciale: Infrastruktur / Programmering
            Fagnummer: 2113', DATE '2023-10-02', DATE '2023-10-06', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('1815201e-afa4-5448-8a2b-cda5acb37f32', 'Machine Learning', '', 'Uger: 40-41
            Varighed: 2 uger
            Speciale: Programmering
            Fagnummer: 17348', DATE '2023-10-02', DATE '2023-10-13', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', '9a0e18a7-983b-499d-8e17-3a797ce0624c', 1),
            ('7942c46a-5062-5042-be9e-bdcef5971dea', 'ASP.NET og XML', '', 'Uge: 41
            Varighed: 1 uge
            Speciale: Infrastruktur / Programmering
            Fagnummer: 1644', DATE '2023-10-09', DATE '2023-10-13', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', '973e6901-e477-4d44-aa1d-356f9ed5681f', 1),
            ('0f6e8d84-2e14-58ca-ab2c-22c7edc5998f', 'Grundlæggende programmering', '', 'Uger: 42-43
            Varighed: 2 uger
            Speciale: IT-supporter / Infrastruktur
            Fagnummer: 16471', DATE '2023-10-16', DATE '2023-10-27', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', 'c85d855d-c05d-4c83-9691-7d345fc3ac68', 1),
            ('3f756e38-6110-56ec-bb2e-2e85058364ff', 'Software defined networking (efter H2)', '', 'Uge: 45
            Varighed: 1 uge
            Speciale: Infrastruktur
            Fagnummer: 17640', DATE '2023-11-06', DATE '2023-11-10', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('b2dae316-d86f-537c-b93e-7e177887eebd', 'Mailserver exchange (efter H2)', '', 'Uge: 45
            Varighed: 1 uge
            Speciale: IT-supporter / Infrastruktur / Programmering
            Fagnummer: 1598', DATE '2023-11-06', DATE '2023-11-10', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1),
            ('4479a9da-18c4-5b30-899a-15a93390ad84', 'Cloudteknologi (efter H2)', '', 'Uge: 46
            Varighed: 1 uge
            Speciale: IT-supporter / Programmering
            Fagnummer: 16875', DATE '2023-11-13', DATE '2023-11-17', NULL, 0, 'archived', 'https://mercantec.notion.site/786863e7a57b44b692c5c00050904101', NULL, 1);
            """);

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "electives"
            WHERE "Id" IN (
                '8b69f861-3b70-549b-9ecb-a36240122748',
                '2e0e1d7e-7f6c-57a1-8ddd-9e3b39eb4ba8',
                '53127dea-6020-5b8d-92d9-690c2c53225e',
                '66d872e6-dd4d-52c9-a566-fafffbeafcda',
                'cd6904ab-eda5-5ccb-b414-94a9d5a1ce33',
                'c01f8467-ca1d-50ee-9938-962adb9ae7dd',
                '3f3ddbbf-8fb9-5962-8ae4-59d784d63d52',
                '52233665-a6e6-5a30-8506-4cd711b5a818',
                'aa43a052-9723-53d4-b315-0023e6e62575',
                'e83aaa41-b532-5dbf-b8c7-c97eba34e682',
                '790f1cd8-a044-5cea-80a5-7dc996bb7eb7',
                'ff8f6453-7094-599f-98a4-f0527363f58b',
                '908e8e9b-e0aa-5b45-92f3-492f4ca51965',
                '044d863e-ad9e-5a90-bcbf-53cb9f53eeaa',
                '75a77db9-3d1a-5ee3-bcfc-b2187cb15fa1',
                'e6a1451f-d618-5470-a305-723214103590',
                '2a1ba0fd-76bf-5da3-a1cb-210b744a2dcb',
                '94f7558d-0856-51a7-8817-6a262d8e9685',
                '31afbac3-9f82-57a7-98fd-ee7f8496b5cd',
                '60d342c0-57ad-557d-9543-a2c4e12bc67e',
                '3c2d8b52-b697-50bb-bda5-d80257706331',
                'b45c6e2e-7f92-5a68-aba5-212826c446f4',
                '7c9ebab1-567c-5ce0-abf3-ac66deb85679',
                '33b298c5-7627-5c06-8cfb-9859dbcf59ff',
                'a66dd2f9-04fb-5920-b043-1b3a2ad653aa',
                '885871ad-f4cb-5f14-8a99-c1d137eba32f',
                '121a92ad-b003-5666-8e97-99c8de139227',
                'edd591a2-ad55-5988-8888-01309d57a943',
                'b6c29712-12e1-55f4-ab93-46d552a3de3a',
                'f8e0b28f-4b30-5b27-97d9-6dd80c359ddc',
                '32251b1b-f6cb-5849-975f-f8f4b77219f5',
                '8fe0db35-afd1-5367-9ac0-5b58bf4cdc81',
                'e0867a67-55a3-5504-836a-fb02f87ef1a9',
                '88526d55-19e7-5c3f-a05f-d6456941c19e',
                '2b2b8f95-5b77-5fca-8a44-66896dd4e448',
                '674fd2ec-7289-58fe-a712-ecad317ffbae',
                'c11f3f63-4b77-5215-a3c3-c4193ae8d65a',
                '1815201e-afa4-5448-8a2b-cda5acb37f32',
                '7942c46a-5062-5042-be9e-bdcef5971dea',
                '0f6e8d84-2e14-58ca-ab2c-22c7edc5998f',
                '3f756e38-6110-56ec-bb2e-2e85058364ff',
                'b2dae316-d86f-537c-b93e-7e177887eebd',
                '4479a9da-18c4-5b30-899a-15a93390ad84'
            );
            """);

        migrationBuilder.Sql("""
            DELETE FROM "electives"
            WHERE "Id" IN (
                '28bdab5c-a237-80a6-a76f-e538cef2e5d3',
                'b219ccf5-5b8a-48a3-8052-7c06216eadc4',
                '9a0e18a7-983b-499d-8e17-3a797ce0624c',
                '32dd137e-3eea-4a94-b92e-5809c99a4e0e',
                'e7a404ad-39a4-4d11-abfe-b4ec8225b26c',
                '973e6901-e477-4d44-aa1d-356f9ed5681f',
                '934ef0eb-d654-49e6-813a-abe1fb2bcb88',
                '1303930e-1077-49e2-a3a2-f0c7d02fddd7',
                'f9ac4206-325b-42d3-95cd-2a0c7a5b3219',
                '5b9982a8-060f-4f8a-bd23-737dabda90dd',
                'c85d855d-c05d-4c83-9691-7d345fc3ac68',
                '2638af74-b36d-4537-99f0-6e0f0cf46fe8'
            );
            """);

    }
}
