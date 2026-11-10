namespace Mercantec.Tech.Web.Characters;

internal static class BatTeachingMaterials
{
    private const string ImageFolder = "/images/characters/bat/infographics/";
    private const string Design = "1. Design og integration";
    private const string Sensors = "2. Sensorer og signaler";
    private const string Firmware = "3. Firmware og fejlfinding";
    private const string Network = "4. Netværk og edge";
    private const string Linux = "5. Embedded Linux og sikkerhed";

    private static readonly CharacterLink Networks = Source("NIST · Networks of Things",
        "https://csrc.nist.gov/pubs/sp/800/183/final");
    private static readonly CharacterLink Ot = Source("NIST · Sikkerhed i operationel teknologi",
        "https://csrc.nist.gov/pubs/sp/800/82/r3/final");
    private static readonly CharacterLink Adc = Source("Arduino · analogRead",
        "https://docs.arduino.cc/language-reference/en/functions/analog-io/analogRead/");
    private static readonly CharacterLink Uno = Source("Arduino · UNO R3",
        "https://docs.arduino.cc/hardware/uno-rev3/");
    private static readonly CharacterLink Ping = Source("Arduino · Ultralydeksempel med PING)))",
        "https://docs.arduino.cc/built-in-examples/sensors/Ping/");
    private static readonly CharacterLink Sound = Source("NASA · Akustik og lydens hastighed",
        "https://ntrs.nasa.gov/api/citations/19880019246/downloads/19880019246.pdf");
    private static readonly CharacterLink Smoothing = Source("Arduino · Glatning af måleserier",
        "https://docs.arduino.cc/built-in-examples/analog/Smoothing/");
    private static readonly CharacterLink Pullup = Source("Arduino · Knap med INPUT_PULLUP",
        "https://docs.arduino.cc/built-in-examples/digital/InputPullupSerial/");
    private static readonly CharacterLink I2c = Source("NXP · I²C-specifikation",
        "https://community.nxp.com/pwmxy87654/attachments/pwmxy87654/other/16345/1/UM10204.pdf");
    private static readonly CharacterLink Spi = Source("Arduino · SPI",
        "https://docs.arduino.cc/language-reference/en/functions/communication/SPI/");
    private static readonly CharacterLink Serial = Source("Arduino · Seriel kommunikation",
        "https://docs.arduino.cc/language-reference/en/functions/communication/serial/");
    private static readonly CharacterLink Timing = Source("Arduino · micros()",
        "https://docs.arduino.cc/language-reference/en/functions/time/micros/");
    private static readonly CharacterLink Nonblocking = Source("Arduino · Blink Without Delay",
        "https://docs.arduino.cc/built-in-examples/digital/BlinkWithoutDelay/");
    private static readonly CharacterLink Mqtt = Source("OASIS · MQTT 5.0",
        "https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html");
    private static readonly CharacterLink Mosquitto = Source("Debian · Manual til mosquitto_pub",
        "https://manpages.debian.org/stable/mosquitto-clients/mosquitto_pub.1.en.html");
    private static readonly CharacterLink OpcUa = Source("OPC Foundation · OPC UA",
        "https://reference.opcfoundation.org/specs/OPC-10000-1/full");
    private static readonly CharacterLink Debian = Source("Debian · Pakkestyring",
        "https://www.debian.org/doc/manuals/debian-reference/ch02.en.html");
    private static readonly CharacterLink Fhs = Source("Debian · Filsystemet og shell",
        "https://www.debian.org/doc/manuals/debian-reference/ch01.en.html");
    private static readonly CharacterLink Ssh = Source("OpenBSD · SSH-klientens manual",
        "https://man.openbsd.org/ssh");
    private static readonly CharacterLink Pi = Source("Raspberry Pi · Installation af operativsystem",
        "https://www.raspberrypi.com/documentation/computers/getting-started.html#installing-the-operating-system");
    private static readonly CharacterLink Systemd = Source("Debian · systemd-tjenester",
        "https://manpages.debian.org/stable/systemd/systemd.service.5.en.html");
    private static readonly CharacterLink SystemdExec = Source("Debian · Rettigheder for systemd-tjenester",
        "https://manpages.debian.org/stable/systemd/systemd.exec.5.en.html");
    private static readonly CharacterLink Journal = Source("Debian · journalctl",
        "https://manpages.debian.org/stable/systemd/journalctl.1.en.html");
    private static readonly CharacterLink IotSecurity = Source("NIST · Sikkerhedsfunktioner i IoT-enheder",
        "https://csrc.nist.gov/pubs/ir/8259/a/final");

    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        Card("01-sensor-til-handling", "Fra sensor til handling",
            "Flagermusen forbinder en afstandssensor, et styrekort og et lyssignal i sin parkeringskælder.",
            Design,
            "Følg en måling fra parkeringskælderens sensor til styrekort, lokalt lys og en eventuel central visning.",
            [
                "Flagermusen optager den samme teske i forskellige parkeringskældre for at høre, hvad rummet gør ved signalet. I en IoT-løsning sender sensoren målinger til kode, som kan tænde et lys lokalt og dele en status med et andet system over et netværk.",
                "Et lokalt termometer med display er et embedded system. Det bliver en IoT-løsning, når data eller kommandoer udveksles som del af en sammenhængende netværksløsning. Ved Industrial IoT (IIoT) indgår enheden i fx overvågning eller styring af produktion, hvor tilgængelighed og fysisk sikkerhed kan være afgørende.",
                "Prøv på papir. Tegn kæden for en parkeringsplads, en plantekasse eller et produktionsbånd. Markér sensor, behandling, aktuator og netværk. Beskriv den lokale handling, mens forbindelsen er nede.",
                "Tilknyttede målpinde · 17682 nr. 1–3 og 8."
            ],
            null, Networks, Ot),
        Card("02-tegn-din-iot-loesning", "Tegn din IoT-løsning",
            "Flagermusen samler behov, komponenter og test på tre tavler i sit værksted.",
            Design,
            "Omsæt en parkeringscase til krav, et forbindelsesdiagram og konkrete accepttests.",
            [
                "Aftal formål, måleenhed, interval, strømforsyning, tolerancer, og hvad der skal ske ved fejl. Dokumentér komponenter, pinout, signalniveauer, firmwareversion og forbindelser. Beskriv også, hvem der ejer data, og hvem der må ændre indstillinger.",
                "Brug databladet til el og timing. I en IIoT-case kan fx OPC UA være relevant for integrationen og NISTs OT-vejledning for designet. Vurder krav til miljø, driftsstop, netværksadgang og sikker drift før hardwarevalg.",
                "Udstyrslab. Brug styrekort, dokumenteret afstandssensor og indikator. Tegn og byg en prototype, der viser optaget og fri. Test en genstand på kendt afstand, et bortfaldet ekko og afbrudt netværk. Gem diagram, kode og forventet/faktisk resultat.",
                "Tilknyttede målpinde · 17682 nr. 4–6, 9, 16 og 18."
            ],
            null, Uno, Ot, OpcUa),
        Card("03-analog-og-digital", "Analog eller digital?",
            "Flagermusen sammenligner en glidende analog kurve med et digitalt signal i to niveauer.",
            Sensors,
            "Skeln mellem et analogt målesignal og et digitalt signal, og undersøg hvor meget information målingen bevarer.",
            [
                "En analog spænding kan variere løbende. En ADC måler spændingen på bestemte tidspunkter og omsætter den til diskrete tal. Ved Arduino UNO R3 giver analogRead som udgangspunkt en 10-bit værdi fra 0 til 1023. Kontrollér boardets reference og tilladte spænding.",
                "En knap kan give to digitale niveauer. En digital sensor kan også sende måletal som bits over en bus. I begge tilfælde skal du kontrollere signalniveau, timing og om værdierne giver mening for det, du måler.",
                "Udstyrslab. Tilslut et potentiometer som vist i Arduino-dokumentationen. Log A0 ved drejning, og sammenlign måleserien med en knap på en digital indgang. Registrér variation ved en fast position. Hvilket signal egner sig til en glidende indstilling, og hvilket egner sig til et tryk?",
                "Tilknyttede målpinde · 17682 nr. 10–12 og 23."
            ],
            null, Adc, Uno),
        Card("04-ekko-og-afstand", "Mål afstanden med ekko",
            "Flagermusens teske og optager ligger ved en tavle, hvor en ultralydspuls løber fra sensoren til en væg og tilbage.",
            Sensors,
            "Mål tiden for en lydpuls frem og tilbage og brug den til at beregne afstanden til en flade.",
            [
                "Flagermusens teskeekko er hørbart. En ultralydssensor udsender sin egen puls og måler et reflekteret signal. Ved omkring 20 °C bevæger lyd sig ca. 343 meter pr. sekund i luft. Pulsens rundtur er dobbelt så lang som afstanden til fladen.",
                "En målt rundtur på 11.662 mikrosekunder giver cirka 200 cm. Temperatur, skrå eller bløde flader og flere refleksioner kan påvirke målingen. Hvis et brugbart svar udebliver, skal koden kunne melde ukendt afstand efter en timeout.",
                "Udstyrslab. Brug den afstandssensor, I har datablad til, og mål en plan flade ved fx 50, 100 og 200 cm. Drej fladen og gentag. Gem pulstider og beregn afvigelsen fra målebåndet.",
                "Tilknyttede målpinde · 17682 nr. 10–11 og 22."
            ],
            """
            double afstandCm(unsigned long rundturUs)
            {
                return rundturUs * 0.0343 / 2.0;
            }
            """, Ping, Sound),
        Card("05-kalibrering-og-filter", "Stol på målingen",
            "Flagermusen sammenholder målebånd, gentagne målinger og en afvigende observation på sin tavle.",
            Sensors,
            "Brug en kendt afstand, gentag målingen og vælg et filter, der passer til måleserien.",
            [
                "En reference fortæller, hvor stor afvigelsen er. En serie gentagelser viser spredningen. Mål ved flere kendte afstande, og undersøg om fejlen er omtrent konstant eller ændrer sig med afstanden.",
                "For serien 98, 210 og 102 cm giver medianen 102 cm, hvis alle tre er mulige afstande i opstillingen. Medianen dæmper enkelte afvigelser. Timeout eller værdier uden for sensorens dokumenterede område skal registreres som ugyldige før filtrering.",
                "Prøv med gemte data eller i udstyrslab. Gem både rå og filtrerede målinger samt en status for ugyldige prøver. Test, hvor hurtigt filtret følger en rigtig ændring i afstanden, og om filterforsinkelsen er acceptabel.",
                "Tilknyttede målpinde · 17682 nr. 10–11, 22–23."
            ],
            null, Ping, Smoothing),
        Card("06-mikrocontroller-og-linux", "Vælg den rette computer",
            "Flagermusen står mellem et lille styrekort til sensorer og en Linux-enhed til lagring og tjenester.",
            Firmware,
            "Fordel opgaven mellem en mikrocontroller og en Linux-enhed efter timing, strømforbrug og softwarebehov.",
            [
                "En mikrocontroller kan have hardwaretimere og direkte GPIO til måling og styring. En embedded Linux-enhed kan køre flere processer, lagre målinger og tilbyde netværkstjenester. Tjek konkrete boarddata og krav til opstart, vedligeholdelse og strøm.",
                "I ekkocasen tager styrekortet tid på pulser og styrer indikatoren. En Linux-gateway kan modtage resultater, gemme dem og sende dem videre. Til udvikling kan du bruge Arduino IDE og Serial Monitor på styrekortet samt SSH og shell på gatewayen.",
                "Prøv på papir eller med kortene foran dig. Find pinout, logikspænding, kommunikationsport og strømkrav i dokumentationen. Placér én opgave, der kræver præcis timing, og én, der kræver filsystem eller flere samtidige processer.",
                "Tilknyttede målpinde · 17682 nr. 4, 17 og 20."
            ],
            null, Uno, Pi),
        Card("07-gpio-og-aktuatorer", "Fra GPIO til bevægelse",
            "Flagermusen viser et digitalt input, et styrekort og en motor med sin egen driver.",
            Firmware,
            "Læs et digitalt input, tag en beslutning i koden, og styr en udgang via de korrekte elektriske trin.",
            [
                "Sæt retningen med pinMode, læs med digitalRead, og skriv med digitalWrite. I UNO R3-eksemplet forbindes en knap mellem pin 2 og GND. INPUT_PULLUP holder pinnen høj i hvile, og tryk giver lav. Den indbyggede LED er forsøgets aktuatorindikator.",
                "En motor kræver en egnet driver og en forsyning, der passer til belastningen. Følg dokumentationen for strøm, signalniveauer, fælles reference og beskyttelse. Figuren illustrerer kæden. Kodeøvelsen bruger kun en lavspændingsknap og kortets LED.",
                "Udstyrslab. Kør sketchen på UNO R3, og test tryk, slip og ledningsbrud ved knappen. Tilføj først derefter en godkendt driver fra jeres udstyrslab, og beskriv hvad udgangen gør ved opstart og fejl.",
                "Tilknyttede målpinde · 17682 nr. 9, 17–19 og 23."
            ],
            """
            void setup()
            {
                pinMode(2, INPUT_PULLUP);
                pinMode(LED_BUILTIN, OUTPUT);
            }

            void loop()
            {
                digitalWrite(LED_BUILTIN, digitalRead(2) == LOW ? HIGH : LOW);
            }
            """, Pullup, Uno),
        Card("08-i2c-spi-uart", "Vælg den rette bus",
            "Flagermusen undersøger tre adskilte ledningsdiagrammer for I²C, SPI og UART.",
            Firmware,
            "Sammenlign signaler, adressering og ledningsføring, før du forbinder to enheder.",
            [
                "I²C bruger fælles SDA og SCL, normalt med pull-up-modstande, og enheder vælges med adresser. SPI har clock, datalinjer og typisk et chip-select pr. periferienhed. UART kan forbinde to enheder med TX til RX og RX til TX, hvis baudrate, format og signalniveauer passer.",
                "Læs databladene for begge enheder, og find reference, pinout, tilladt spænding og højeste hastighed. Kontrollér også, om en boardpin er delt med USB eller en indbygget komponent. Hold øvelsen inden for udstyrets dokumenterede ledningslængde og spændingsniveau.",
                "Udstyrslab. Vælg en sensor med dokumenteret bus. Læs én værdi, log den via Serial Monitor, og sammenlign en optagelse fra en logikanalysator med det, biblioteket returnerer.",
                "Tilknyttede målpinde · 17682 nr. 4, 12–13, 17, 19–20."
            ],
            null, I2c, Spi, Serial),
        Card("09-tilstand-og-timeout", "Vent på ekko uden at blokere",
            "Flagermusen viser, hvordan en puls følges af ventetid, et måleresultat eller en timeout.",
            Firmware,
            "Del målingen op i tilstande, så programmet stadig kan passe indikator, input og netværk.",
            [
                "Send pulsen, gem starttiden, og skift til ventetilstand. Registrér de relevante signalkanter efter sensorens datablad. Ved et gyldigt ekko beregner du målingen. Når tidsgrænsen nås, går målingen til ukendt. Planlæg næste puls med den minimumspause, databladet kræver.",
                "Udregn forløbet tid ved at trække to værdier af samme unsigned type fra hinanden. Arduino UNO R3's 32-bit mikrotæller ruller rundt efter ca. 70 minutter. Differencen fungerer også hen over rundtællingen, når du måler korte, afgrænsede intervaller og tjekker løbende.",
                "Kodeøvelse. Test 1199 og 1200 µs med en grænse på 1200. Test derefter en starttid tæt på tællerens maksimum. Udstyrslab. Kontrollér med logikanalysator, at indikator og andre opgaver stadig får tid, og at korte pulser opfanges. Brug hardwarecapture eller interrupt, hvis opstillingen kræver det.",
                "Tilknyttede målpinde · 17682 nr. 21–22 og 24."
            ],
            """
            #include <stdint.h>

            bool erUdlobet(uint32_t nuUs, uint32_t sendtUs, uint32_t timeoutUs)
            {
                return static_cast<uint32_t>(nuUs - sendtUs) >= timeoutUs;
            }
            """, Timing, Nonblocking, Ping),
        Card("10-maal-vaerdi-handling", "Brug målingen i koden",
            "Flagermusen validerer et måletal, sammenligner det med en grænse og vælger indikatorens tilstand.",
            Firmware,
            "Brug en tilstandsregel, der holder indikatoren stabil, når målingen ligger tæt på grænsen.",
            [
                "Gennemløb en serie målinger i et array. Kodeeksemplet bruger et øveområde på 2–400 cm og −1 som markør for timeout. I en virkelig prototype hentes grænserne fra datablad og kalibrering, og ugyldige prøver meldes som ukendt.",
                "Skift til optaget ved højst 95 cm og til fri ved mindst 105 cm. Behold sidste gyldige tilstand imellem grænserne. Den afstand mellem skiftegrænserne hedder hysterese. Efter en timeout kan lyset fx vise fejlstatus, mens den sidst kendte tilstand bevares separat.",
                "Kodeøvelse. Prøv serien nedenfor. Forvent fri, optaget, optaget, fri og til sidst ukendt. Tilføj derefter præcis 95 og 105 cm som grænsetest. Udstyrslab. Flyt en plade omkring grænsen, og kontrollér tilstandsforløbet i seriel log.",
                "Tilknyttede målpinde · 17682 nr. 18, 21–24."
            ],
            """
            const int maalCm[] = {110, 93, 99, 106, -1};
            bool optaget = false;

            for (int cm : maalCm)
            {
                if (cm < 2 || cm > 400)
                {
                    Serial.println("ukendt");
                    continue;
                }

                if (cm <= 95) optaget = true;
                if (cm >= 105) optaget = false;
                Serial.println(optaget ? "optaget" : "fri");
            }
            """, Adc, Serial),
        Card("11-fejlfinding", "Find fejlen lag for lag",
            "Flagermusen følger et målesignal gennem måleudstyr, seriel log og netværksforbindelse.",
            Firmware,
            "Følg den samme testmåling fra sensor til app, og find det første lag, hvor resultatet afviger.",
            [
                "Gør en fejl gentagelig. Notér firmwareversion, input, forventet værdi og faktisk resultat. Test kendt afstand, grænseværdi, manglende ekko og afbrudt forbindelse. Ændr én ting ad gangen og gentag den samme test efter en rettelse.",
                "Mål analog spænding med passende måleudstyr og se på digitale flanker med oscilloskop eller logikanalysator. Brug en UART-dekoder til at sammenholde ledningssignal og seriel log. Ved Wi-Fi kan du måle signalstyrke på jeres eget øvenet med en Wi-Fi-analysator eller fx sammenligne linkmålinger fra iw dev wlan0 link, hvis dit Linux-interface hedder wlan0.",
                "Udstyrslab. Fremprovokér en aftalt fejl i egen opstilling. Kontrollér kabel eller Wi-Fi, dernæst adresse og adgang til den lokale gateway. Ping kan vise IP-rækkevidde, mens en succesfuld MQTT-publicering kræver kontrol på applikationslaget.",
                "Tilknyttede målpinde · 17682 nr. 12, 15, 20 og 24."
            ],
            """
            ip link
            ip addr
            # Udskift med IP-adressen på jeres egen godkendte gateway.
            ping -c 3 192.168.10.1
            """, Serial, Networks),
        Card("12-mqtt-og-protokoller", "Fra enhed til besked",
            "Flagermusen viser en måleenhed, der sender til en MQTT-broker med to abonnenter.",
            Network,
            "Vælg en transport og en protokol, som passer til målinger, forbindelser og modtagere.",
            [
                "Ethernet og Wi-Fi kan bære IP-trafikken. HTTP passer fx til at hente en status på forespørgsel. MQTT bruger emner og en broker, så flere abonnenter kan modtage samme hændelse. QoS 1 leverer med mulighed for dubletter, så en modtager kan genkende en måling på dens ID.",
                "I en fabrik kan OPC UA passe, når eksisterende systemer forventer et standardiseret informationsformat. Vælg ud fra udstyr, datamodel, forsinkelse, sikkerhed og drift. Angiv emner, rettigheder og datakontrakt i integrationsdiagrammet.",
                "Softwarelab. Brug en allerede oprettet lokal Mosquitto-broker på loopback. Start abonnenten i én terminal og publicér i en anden. Ændr værdien og kontrollér modtagelsen. Gentag med et andet emne og forklar, hvorfor den første abonnent ikke ser det.",
                "Tilknyttede målpinde · 17682 nr. 2, 7, 9, 13 og 16."
            ],
            """
            # Terminal 1
            mosquitto_sub -h 127.0.0.1 -t 'oeve/parkering/plads1' -q 1

            # Terminal 2
            mosquitto_pub -h 127.0.0.1 -t 'oeve/parkering/plads1' -q 1 -m '{"occupied":true}'
            """, Mqtt, Mosquitto, OpcUa),
        Card("13-edge-og-gateway", "Bearbejd data tæt på sensoren",
            "Flagermusen sender behandlede sensordata gennem en lokal gateway til et centralt system.",
            Network,
            "Behandl signalet lokalt, og aftal, hvilke data der sendes videre til et centralt system.",
            [
                "En sensor kan levere rå måletal eller selv udføre analyse. En intelligent sensor eller visionsenhed kan fx sende en optaget-status. En gateway kan læse UART-data fra et styrekort, kontrollere enhed og kvalitet og publicere et resultat med måle-ID og tidsstempel til en MQTT-broker.",
                "Beslut hvad der sker, hvis forbindelsen forsvinder. Bevar den lokale indikator, sæt en grænse på køen og aftal, om gamle data bliver gemt eller kasseret. Angiv tidsgrundlag, og lad modtageren skelne nye målinger fra forsinkede eller gentagne beskeder.",
                "Softwarelab med gemte data eller udstyrslab med egen sensor. Indlæs linjer på gatewayen, kassér ugyldige linjer, og send status til en lokal modtager. Afbryd brokerforbindelsen i 30 sekunder. Dokumentér, hvad der sker lokalt og hvilke målinger modtageren får bagefter.",
                "Tilknyttede målpinde · 17682 nr. 7, 9 og 22 · 17683 nr. 6–7."
            ],
            """
            {
              "sampleId": "plads1-42",
              "measuredAt": "2026-01-01T12:00:00Z",
              "valueCm": 98,
              "quality": "valid"
            }
            """, Networks, Mqtt),
        Card("14-embedded-linux", "Kom i gang med embedded Linux",
            "Flagermusen gør en lille Linux-enhed klar og undersøger den fra en anden terminal.",
            Linux,
            "Installér et understøttet image, forbind til dit lab-board, og find rundt i filer og pakker.",
            [
                "Brug et officielt image til dit board. Kontrollér dets hash eller signatur, når producenten tilbyder den, og følg producentens installationstrin. Aftal en labbruger og netværksopsætning. Ved første SSH-forbindelse sammenlignes værtsnøglens fingeraftryk med boardet eller en pålidelig kilde.",
                "Åbn en terminal med fx OpenSSH eller PuTTY. Brug en SFTP-klient som WinSCP til at overføre en øvefil via SSH. Find konfiguration under /etc, software installeret lokalt under /opt, og se hvordan din distribution gør driftslogs tilgængelige, fx i /var/log eller via journalctl.",
                "Linux-lab. På jeres Debian-baserede øveenhed finder du den aktuelle mappe og læser pakkens version. Opdatér pakkelisten, installér øvepakken, opgradér den hvis en nyere version findes, og fjern den til sidst. Notér, hvad der blev ændret, og hvilke trin der krævede forhøjede rettigheder.",
                "Tilknyttede målpinde · 17683 nr. 1–4 · 16481 nr. 1–2 og 8."
            ],
            """
            pwd
            ls -ld /etc /opt /var/log
            apt-cache policy mosquitto-clients
            sudo apt update
            sudo apt install mosquitto-clients
            sudo apt install --only-upgrade mosquitto-clients
            sudo apt remove mosquitto-clients
            """, Pi, Debian, Fhs, Ssh),
        Card("15-linux-bruger-og-tjeneste", "Lad gatewayen køre som tjeneste",
            "Flagermusen forbinder en begrænset bruger, en kørende gatewaytjeneste og systemets log.",
            Linux,
            "Kør gatewaykoden med en afgrænset bruger, og undersøg dens status og fejl i journalen.",
            [
                "Udvikl og test fx collector.py i en lokal Python-venv, før appen installeres på boardet. Lad den læse en øvefil eller seriel sensor, validere input og rapportere hændelser. Dokumentér Python-version, afhængigheder og opsætning, så en makker kan gentage forsøget.",
                "På en systemd-baseret labenhed kræver eksemplet, at den særskilte bruger lup-iot og scriptet på den viste sti findes. Tildel kun adgang til de filer, porte eller enheder, appen bruger. Ved brug af en venv med tredjepartspakker skal ExecStart pege på dens Python.",
                "Linux-lab. Kør scriptet manuelt med tjenestens bruger, og lad derefter systemd starte det. Brug systemctl status lup-iot.service og journalctl -u lup-iot.service -n 20. Giv tjenesten en ugyldig sensorlinje, og sammenhold loggen med en forventet reaktion. Test også genstart og at appen genopretter brokerforbindelsen.",
                "Tilknyttede målpinde · 17683 nr. 5–8 · 16481 nr. 5, 7 og 9."
            ],
            """
            [Unit]
            Description=LUP IoT-gateway

            [Service]
            User=lup-iot
            ExecStart=/usr/bin/python3 /opt/lup-iot/collector.py
            NoNewPrivileges=true
            Restart=on-failure

            [Install]
            WantedBy=multi-user.target
            """, Systemd, SystemdExec, Journal),
        Card("16-sikker-firmware", "Opdatér enheden sikkert",
            "Flagermusen viser en enhedsidentitet, kontrol af en firmwarepakke og en vej til gendannelse.",
            Linux,
            "Beskyt enheden med tydelig identitet, få rettigheder og en opdateringsproces, der er afprøvet i lab.",
            [
                "Forestil dig, at nogen sender en falsk fri-status til parkeringsdisplayet. Aftal særskilt identitet til enheder, få rettigheder pr. brokeremne, og adskil enhederne i netværket. Brug krypteret, autentificeret transport, når målinger sendes mellem enheder.",
                "Brug godkendte pakkekilder og verificér firmwareopdateringens signatur med en betroet nøgle, når platformen understøtter det. Registrér version, test på én enhed før udrulning, og hav en understøttet vej til at gendanne en godkendt version eller erstatte en fejlet enhed.",
                "Bordøvelse eller godkendt udstyrslab. Skriv hvad der sker, hvis signaturen er forkert, strømmen forsvinder midt i processen, eller sensoren svarer forkert bagefter. Definér hvem der godkender udrulningen, og vis hvordan du kontrollerer version og funktion efter genstart.",
                "Tilknyttede målpinde · 17682 nr. 5, 14 og 16 · 17683 nr. 8."
            ],
            null, IotSecurity, Ot, Debian)
    ];

    private static CharacterIllustration Card(string file, string title, string alt, string stage, string summary,
        IReadOnlyList<string> explanation, string? code, params CharacterLink[] sources) =>
        new(title, ImageFolder + file + ".png", alt, 1536, 1024)
        {
            Preview = ImageFolder + file + ".webp",
            Stage = stage,
            Summary = summary,
            Explanation = explanation,
            Code = code,
            Sources = sources
        };

    private static CharacterLink Source(string label, string href) => new(label, href, true, false);
}
