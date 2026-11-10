namespace Mercantec.Tech.Web.Characters;

internal static class BeeTeachingMaterials
{
    private const string ImageFolder = "/images/characters/bee/infographics/";
    private const string Risk = "Risici og ansvar";
    private const string Identity = "Identitet og data";
    private const string Operations = "Netværk og drift";
    private const string Response = "Overvågning og beredskab";
    private const string Code = "Sikker kode og test";

    private static readonly CharacterLink Csf = Source("NIST · Cybersecurity Framework",
        "https://www.nist.gov/cyberframework");
    private static readonly CharacterLink Privacy = Source("Datatilsynet · Pseudonymisering og anonymisering",
        "https://www.datatilsynet.dk/regler-og-vejledning/behandlingssikkerhed/katalog-over-foranstaltninger/pseudonymisering-og-anonymisering");
    private static readonly CharacterLink Phishing = Source("Sikkerdigital · Falske mails, beskeder og opkald",
        "https://www.sikkerdigital.dk/borger/digital-svindel/falske-mails-beskeder-og-opkald");
    private static readonly CharacterLink DigitalIdentity = Source("NIST · Authentication and Authenticator Management",
        "https://csrc.nist.gov/pubs/sp/800/63/b/4/final");
    private static readonly CharacterLink Ransomware = Source("CISA · StopRansomware Guide",
        "https://www.cisa.gov/stopransomware/ransomware-guide");
    private static readonly CharacterLink IncidentResponse = Source("NIST · Incident Response Recommendations",
        "https://csrc.nist.gov/pubs/sp/800/61/r3/final");
    private static readonly CharacterLink ActiveDirectory = Source("Microsoft Learn · Sikring af Active Directory",
        "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/best-practices-for-securing-active-directory");
    private static readonly CharacterLink Radius = Source("Microsoft Learn · RADIUS og AAA",
        "https://learn.microsoft.com/en-us/windows/win32/nps/ias-radius-authentication-and-accounting");
    private static readonly CharacterLink Authentication = Source("OWASP · Authentication",
        "https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html");
    private static readonly CharacterLink Authorization = Source("OWASP · Authorization",
        "https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html");
    private static readonly CharacterLink WindowsBaseline = Source("Microsoft Learn · Windows Server-sikkerhedsbaselines",
        "https://learn.microsoft.com/en-us/windows-server/security/osconfig/osconfig-how-to-configure-security-baselines");
    private static readonly CharacterLink GroupPolicy = Source("Microsoft Learn · Group Policy",
        "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/group-policy/group-policy-overview");
    private static readonly CharacterLink SwitchSecurity = Source("Cisco · Sikkerhedsfunktioner på switche",
        "https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst2960l/software/15-2_7_e/configuration_guide/sec/b_1527e_security_2960l_cg/security_features_overview.html");
    private static readonly CharacterLink Cis = Source("CIS · Critical Security Controls",
        "https://www.cisecurity.org/controls");
    private static readonly CharacterLink Ipsec = Source("NIST · IPsec VPNs",
        "https://csrc.nist.gov/pubs/sp/800/77/r1/final");
    private static readonly CharacterLink OpenVpn = Source("OpenVPN · Community-dokumentation",
        "https://openvpn.net/community-docs/");
    private static readonly CharacterLink DenialOfService = Source("OWASP · Denial of Service",
        "https://cheatsheetseries.owasp.org/cheatsheets/Denial_of_Service_Cheat_Sheet.html");
    private static readonly CharacterLink Idps = Source("NIST · Intrusion Detection and Prevention Systems",
        "https://csrc.nist.gov/pubs/sp/800/94/final");
    private static readonly CharacterLink Suricata = Source("Suricata · IPS-drift",
        "https://docs.suricata.io/en/latest/ips/index.html");
    private static readonly CharacterLink Logging = Source("OWASP · Logging",
        "https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html");
    private static readonly CharacterLink Syslog = Source("IETF · TLS til Syslog (RFC 5425)",
        "https://www.rfc-editor.org/rfc/rfc5425");
    private static readonly CharacterLink Nts = Source("IETF · Sikker tidssynkronisering (RFC 8915)",
        "https://datatracker.ietf.org/doc/rfc8915/");
    private static readonly CharacterLink Honeypot = Source("ENISA · Honeypots og proaktiv detektion",
        "https://www.enisa.europa.eu/publications/proactive-detection-of-security-incidents-II-honeypots");
    private static readonly CharacterLink Tls = Source("OWASP · Transport Layer Security",
        "https://cheatsheetseries.owasp.org/cheatsheets/Transport_Layer_Security_Cheat_Sheet.html");
    private static readonly CharacterLink OpenSsl = Source("OpenSSL · Undersøg en TLS-forbindelse",
        "https://docs.openssl.org/3.6/man1/openssl-s_client/");
    private static readonly CharacterLink PasswordStorage = Source("OWASP · Password Storage",
        "https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html");
    private static readonly CharacterLink CryptoStorage = Source("OWASP · Cryptographic Storage",
        "https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html");
    private static readonly CharacterLink Secrets = Source("OWASP · Secrets Management",
        "https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html");
    private static readonly CharacterLink Vault = Source("HashiCorp · Vault Database Secrets Engine",
        "https://developer.hashicorp.com/vault/docs/secrets/databases");
    private static readonly CharacterLink Sql = Source("OWASP · Forebyg SQL-injektion",
        "https://cheatsheetseries.owasp.org/cheatsheets/SQL_Injection_Prevention_Cheat_Sheet.html");
    private static readonly CharacterLink Grant = Source("PostgreSQL · GRANT",
        "https://www.postgresql.org/docs/current/sql-grant.html");
    private static readonly CharacterLink Database = Source("OWASP · Database Security",
        "https://cheatsheetseries.owasp.org/cheatsheets/Database_Security_Cheat_Sheet.html");
    private static readonly CharacterLink Xss = Source("OWASP · Forebyg XSS",
        "https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html");
    private static readonly CharacterLink Csrf = Source("OWASP · Forebyg CSRF",
        "https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html");
    private static readonly CharacterLink ApiSecurity = Source("OWASP · API Security Top 10",
        "https://api-security.owasp.org/editions/2023/en/0x11-t10/");
    private static readonly CharacterLink Asvs = Source("OWASP · Application Security Verification Standard",
        "https://asvs.owasp.org/");
    private static readonly CharacterLink Zap = Source("ZAP · Dokumentation",
        "https://www.zaproxy.org/docs/");
    private static readonly CharacterLink Ssf = Source("NIST · Secure Software Development Framework",
        "https://csrc.nist.gov/pubs/sp/800/218/final");
    private static readonly CharacterLink Sbom = Source("OWASP · CycloneDX SBOM",
        "https://cyclonedx.org/capabilities/sbom/");
    private static readonly CharacterLink Kev = Source("CISA · Known Exploited Vulnerabilities",
        "https://github.com/cisagov/kev-data");
    private static readonly CharacterLink SharedResponsibility = Source("Microsoft Learn · Delt ansvar i cloud",
        "https://learn.microsoft.com/en-us/azure/security/fundamentals/shared-responsibility");
    private static readonly CharacterLink Iot = Source("NIST · IoT Device Cybersecurity Core Baseline",
        "https://csrc.nist.gov/pubs/ir/8259/a/final");
    private static readonly CharacterLink Mqtt = Source("OASIS · MQTT 5.0",
        "https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html");
    private static readonly CharacterLink Nis = Source("Retsinformation · NIS 2-loven",
        "https://www.retsinformation.dk/eli/lta/2025/434");
    private static readonly CharacterLink Cra = Source("EUR-Lex · Cyber Resilience Act",
        "https://eur-lex.europa.eu/eli/reg/2024/2847/oj?locale=da");
    private static readonly CharacterLink Iso = Source("ISO · ISO/IEC 27001",
        "https://www.iso.org/standard/27001");
    private static readonly CharacterLink IsoControls = Source("ISO · ISO/IEC 27002",
        "https://www.iso.org/standard/75652.html");
    private static readonly CharacterLink Gdpr = Source("EUR-Lex · Databeskyttelsesforordningen",
        "https://eur-lex.europa.eu/eli/reg/2016/679/oj?locale=da");
    private static readonly CharacterLink PrivacyAct = Source("Retsinformation · Databeskyttelsesloven",
        "https://www.retsinformation.dk/eli/lta/2024/289");
    private static readonly CharacterLink SecurityControls = Source("NIST · Security and Privacy Controls (PE)",
        "https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final");

    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        Card("01-trusselsmodel", "Tegn trusselsmodellen",
            "Cyberbien tegner en systemgrænse omkring en værdifuld fil og markerer en trussel og et værn.",
            Risk,
            "Find det, der skal beskyttes, og vælg et værn, der passer til risikoen.",
            [
                "Tegn et simpelt dataflow for en fiktiv skoleportal. Markér, hvor data krydser en tillidsgrænse, og hvem der kan læse eller ændre dem. Fortrolighed handler om, hvem der må se data, integritet om, at ændringer er korrekte og tilladte, og tilgængelighed om, at tjenesten virker, når den skal.",
                "Beskriv én trussel som en konkret hændelse. En glemt offentlig deling kan gøre elevdata synlige. Vurdér sandsynlighed og konsekvens, vælg privat standardadgang og prøv derefter at åbne filen med en anden testkonto. Hvis en overførsel kan undværes, så fjern den. Begræns ellers felter og adgang. Pseudonymiserede data kan stadig være personoplysninger.",
                "Prøv selv. Tegn en pil ind og ud af en klasses fiktive filarkiv. Skriv ved hver pil, hvem der har adgang, og hvad der skal ske, hvis det går galt.",
                "Tilknyttede målpinde · 22253 nr. 3, 5 · 22848 nr. 1 · 22999 nr. 1–3 · 16867 nr. 1 · 1595 nr. 4 · 17682 nr. 5, 14 · 20555 nr. 8–9, 11."
            ],
            Csf, Privacy),
        Card("02-phishing", "Stop phishing",
            "Cyberbien undersøger en mistænkelig mail og bruger en særskilt telefon til at bekræfte afsenderen.",
            Risk,
            "Opdag et forsøg på at få en kollega til at afgive en kode, åbne en fil eller handle for hurtigt.",
            [
                "Phishing bruger tillid, travlhed og pres til at få en modtager til at handle. Afsendernavn, logo og korrekt sprog kan være kopieret. En uventet besked om betaling eller login kan undersøges i en kanal, du selv åbner, f.eks. det kendte intranet eller telefonnummeret i adressebogen.",
                "Rapportér beskeden via den aftalte kanal, og giv besked hurtigt, hvis nogen allerede har afgivet oplysninger eller godkendt en anmodning. En phishingøvelse skal bruge fiktive beskeder og en aftalt ramme. Den bør gøre det lettere at rapportere fejl uden at udskamme nogen.",
                "Prøv selv. Skriv en fiktiv besked, der spiller på tidspres. Lad en makker udpege, hvordan den kan verificeres, og hvilken vej der bruges til rapportering.",
                "Tilknyttede målpinde · 22253 nr. 1, 4 · 22848 nr. 1."
            ],
            Phishing, DigitalIdentity),
        Card("03-ransomware-backup", "Gendan efter ransomware",
            "Cyberbien isolerer en låst arbejdsstation og gendanner rene filer fra en beskyttet sikkerhedskopi.",
            Risk,
            "Øv, hvordan en tjeneste kommer tilbage efter et ransomwareangreb.",
            [
                "Malware er skadelig software. Ransomware kan kryptere filer og bruges til afpresning, også når data er blevet kopieret. Følg beredskabsplanen, afgræns de ramte systemer, og koordinér isolering med drift og hændelsesansvarlige. Et sikkert gendannelsesmiljø forudsætter, at adgang og årsag er håndteret.",
                "Behold sikkerhedskopier, der er adskilt fra driftskonti, og prøv at gendanne dem. AD-replikering eller et databassnapshot i samme sikkerhedszone kan kopiere fejl eller miste beskyttelsen sammen med driften. Aftal acceptabelt datatab og nedetid, og kontrollér dataintegritet før genåbning.",
                "Prøv selv. Gendan et fiktivt datasæt til et separat miljø. Dokumentér kopiens alder, tiden til gendannelse, og hvad du kontrollerede før genåbning.",
                "Tilknyttede målpinde · 22253 nr. 1 · 16858 nr. 6 · 16857 nr. 8 · 16856 nr. 18, 22 · 20556 nr. 8."
            ],
            Ransomware, IncidentResponse),
        Card("04-adgangskoder-mfa", "Beskyt dit login",
            "Cyberbien kombinerer kodeord og fysisk sikkerhedsnøgle ved en adgangsport.",
            Identity,
            "Beskyt konti med unikke adgangskoder og en uafhængig ekstra faktor.",
            [
                "Godkendelsesfaktorer er noget, du ved, har eller er. To kodeord er samme type faktor. Et kodeord og en sikkerhedsnøgle kan være to faktorer. En FIDO-baseret løsning kan også bindes til det rigtige websted og dermed modstå mange phishingforsøg.",
                "Brug en adgangskodepolitik med lange, unikke koder og kontrol mod kendte kompromitterede koder. Begræns gentagne loginforsøg, f.eks. med ventetid eller kontolås efter den aftalte politik. Planlæg genoprettelse og registrering af nye faktorer med samme omtanke som login. Et mistet kodeord eller en mistet nøgle kræver en kendt og sikker vej videre.",
                "Prøv selv. Sammenlign to loginforløb. Hvilke faktorer indgår, og hvad kan ske, hvis en bruger sender sin engangskode til en falsk supportmedarbejder?",
                "Tilknyttede målpinde · 16484 nr. 12 · 22253 nr. 4 · 22848 nr. 2 · 22849 nr. 3 · 22999 nr. 4 · 16858 nr. 8–10 · 16477 nr. 11."
            ],
            DigitalIdentity, Authentication),
        Card("05-iam-rbac-aaa", "Giv mindst mulig adgang",
            "Cyberbien giver en læser og en redaktør hver deres rollekort ved en adgangskontrol.",
            Identity,
            "Skil identitet fra rolle og giv kun de tilladelser, opgaven kræver.",
            [
                "I rollebaseret adgangskontrol, RBAC, tildeles rettigheder til roller. Brugeren får en rolle, der passer til opgaven. En vikar kan have læseadgang til sit hold i en uge. Administratoradgang gives særskilt og kontrolleres ved ændringer i job og ansvar.",
                "En katalogtjeneste som Active Directory, FreeIPA eller OpenLDAP kan samle identiteter og grupper. LDAP er protokollen til at tilgå kataloget. I netværk med AAA handler authentication om identitet, authorization om tilladelse og accounting om registrering af anvendelsen. RADIUS kan indgå i den løsning. Begræns, hvilke konti og tillidsforhold der kan føre til privilegeret adgang.",
                "Prøv selv. Lav en adgangsmatrix for elev, underviser og drift med læse- og skriveadgang. Angiv ejer, udløb og en test for hver adgang, der skal afvises.",
                "Tilknyttede målpinde · 22848 nr. 2 · 22999 nr. 4, 5 · 16867 nr. 8 · 16858 nr. 2–5, 8–10 · 16857 nr. 4 · 16474 nr. 23 · 16856 nr. 14–16 · 17683 nr. 5, 8 · 16477 nr. 11 · 20555 nr. 11."
            ],
            ActiveDirectory, Radius, Authorization),
        Card("06-haerd-serveren", "Hærd serveren",
            "Cyberbien lukker unødvendige serverporte og kontrollerer serverens sikkerhedsopsætning.",
            Operations,
            "Start fra en kendt sikker opsætning og kontrollér, hvad der faktisk gælder på værten.",
            [
                "Luk ubrugte tjenester og konti. Afgræns administration til godkendte enheder og særskilte driftkonti. Indstil værtsfirewall, audit og sikkerhedsopdateringer efter serverens rolle. Brug f.eks. en Microsoft-sikkerhedsbaseline som udgangspunkt, og prøv ændringerne i et øvemiljø.",
                "Gruppepolitik i Active Directory har typisk rækkefølgen lokal, site, domæne og organisationsenhed, men nedarvning og håndhævelse kan ændre resultatet. Kontrollér derfor effektiv politik på værten. Kør automatisering med de nødvendige rettigheder, undgå nøgler i PowerShell-scripts, og gem en plan for at tilbageføre en fejlbehæftet ændring.",
                "Prøv selv. Vælg en fiktiv serverrolle. Angiv én tjeneste, der skal være tilgængelig, én der skal lukkes, og hvordan du vil kontrollere audit og netværksadgang bagefter.",
                "Tilknyttede målpinde · 16858 nr. 2–5, 8–10, 11, 12, 13 · 16857 nr. 1, 3 · 16862 nr. 2 · 16876 nr. 8 · 17683 nr. 5, 8."
            ],
            WindowsBaseline, GroupPolicy, ActiveDirectory),
        Card("07-segmentering", "Del netværket op",
            "Cyberbien holder gæster, drift og servere i hver sin netværkszone med begrænsede forbindelser.",
            Operations,
            "Del netværket i zoner og test, at forbindelser kun kan gå ad de aftalte veje.",
            [
                "En VLAN-inddeling skaber logiske lag 2-net. Beslut derudover, hvilken trafik der må passere mellem zoner, og håndhæv det ved en firewall eller en passende adgangsliste. En gæst skal f.eks. kunne få internetadgang, mens serverens administrationsport kun er tilgængelig fra driftens zone.",
                "På switchen skal porte og trunkforbindelser have en tydelig rolle. Brug muligheder som port security eller DHCP snooping, hvor udstyret og designet passer til dem. På trådløse net skal du vælge tidssvarende kryptering og kontrolleret adgang. LEAP og TKIP er historiske løsninger. Ved PEAP skal klienten validere den forventede autentificeringsserver.",
                "Prøv selv. Tegn en matrix for gæstenet, elevnet og drift. Sæt en test på to forbindelser, der skal virke, og to, der skal blive afvist.",
                "Tilknyttede målpinde · 22999 nr. 6 · 16867 nr. 1, 2 · 17678 nr. 1, 4–5 · 17679 nr. 5 · 1595 nr. 4 · 6287 nr. 5, 7."
            ],
            SwitchSecurity, Cis, Radius),
        Card("08-firewall-vpn-ddos", "Styr trafikken",
            "Cyberbien adskiller firewallregler, en krypteret VPN-tunnel og et særskilt DDoS-værn.",
            Operations,
            "Skeln mellem at begrænse trafik, beskytte forbindelsen og holde tjenesten tilgængelig.",
            [
                "En firewall håndhæver trafikregler, f.eks. kilde, destination og port. Lad kun nødvendige forbindelser være tilladt. En VPN beskytter trafik mellem godkendte endepunkter. IPsec kan f.eks. forbinde to netværk eller en klient med et netværk. OpenVPN er en anden VPN-løsning. Adgangen inden i en tunnel skal stadig begrænses efter behov.",
                "DDoS kan udtømme forbindelsens kapacitet eller tjenestens ressourcer. Angreb, der fylder internetforbindelsen, kræver ofte hjælp længere oppe i netværket. Rate limits og robust kapacitet kan begrænse belastningen på appen. Brug separate målinger af trafik, svartid og fejlrater.",
                "Prøv selv. Vælg værn til tre fiktive hændelser. En ukendt port bliver tilgået, en driftsforbindelse går over internettet, og en skoleportal drukner i forespørgsler.",
                "Tilknyttede målpinde · 16484 nr. 7–8 · 22999 nr. 6, 7 · 16867 nr. 1, 5, 7 · 16858 nr. 11 · 17679 nr. 5 · 1595 nr. 4, 8 · 16857 nr. 1, 3, 4 · 16875 nr. 1."
            ],
            Ipsec, OpenVpn, DenialOfService, Cis),
        Card("09-ids-ips", "Alarm eller blokering?",
            "Cyberbien sammenligner et IDS, der alarmerer, med et IPS, der blokerer i trafikvejen.",
            Response,
            "Skeln mellem observation og blokering, og prøv om alarmen kan forklares.",
            [
                "Et IDS registrerer og alarmerer om tegn på en hændelse. Et netværks-IPS kan være placeret direkte i trafikvejen og blokere trafik efter regler. Placering og driftstilstand er afgørende. Suricata er et eksempel på et værktøj, der kan bruges i begge roller.",
                "En signatur matcher et kendt mønster, mens en tærskelregel f.eks. alarmerer ved over 100 afvisninger på et minut. En model trænet med machine learning kan lære et normalmønster af historiske hændelser som trafikmængde, tidspunkt og antal afvisninger. En afvigelse kan skyldes legitim drift. Mål falske alarmer, og afprøv blokering i et øvemiljø før ændringer i drift.",
                "Prøv selv. Sortér fem fiktive observationer i normal, undersøg nærmere og bør blokeres. Forklar, hvilken log der kan afgøre de usikre tilfælde.",
                "Tilknyttede målpinde · 22848 nr. 5 · 22999 nr. 6, 7 · 16867 nr. 6."
            ],
            Idps, Suricata),
        Card("10-logs-og-tid", "Følg sporet i loggen",
            "Cyberbien samler hændelser fra flere enheder på en tidslinje med fælles tid.",
            Response,
            "Byg en tidslinje på tværs af udstyr uden at gemme flere oplysninger end nødvendigt.",
            [
                "Syslog kan sende hændelser til en central server. SNMP bruges til statusmålinger og notifikationer fra netudstyr. NTP hjælper enheder med fælles tid. Beskyt indsamlingen, f.eks. med SNMPv3 med godkendelse og kryptering, hvor det er muligt, og en sikker transport til logserveren.",
                "Et analyseværktøj kan sammenkæde logs fra firewall, identitet og app. Gem tid, enhed, hændelsestype, resultat og et korrelations-id, hvor det er nødvendigt. Undlad adgangskoder, sessionshemmeligheder og unødvendige persondata. Aftal adgang, opbevaringstid og beskyttelse mod ændringer.",
                "Prøv selv. Kæd tre fiktive loglinjer sammen. Et login bliver afvist, en ny rolle bliver tildelt, og en fil bliver hentet. Hvilken ekstra oplysning mangler, før de kan forbindes?",
                "Tilknyttede målpinde · 22848 nr. 5 · 22999 nr. 10 · 16867 nr. 3–4 · 16858 nr. 12 · 2113 nr. 1, 5 · 16857 nr. 8 · 20555 nr. 11."
            ],
            Logging, Syslog, Nts),
        Card("11-honeypot", "En lokkedåse til angreb",
            "Cyberbien placerer et falsk lokkemål i et isoleret øvenet og sender hændelser til en log.",
            Response,
            "Brug en honeypot til at se, at nogen rører ved et lokkemål uden legitime brugere.",
            [
                "En honeypot efterligner et aktiv, der ser interessant ud, f.eks. en tom tjeneste. Den skal kunne observeres, når en aftalt testklient rører ved den. Brug en særskilt zone, syntetiske data, ingen rute til produktion og ingen almindelig udgående trafik. Et afgrænset logflow kan bringe hændelser videre til forsvareren.",
                "Aftal formål, tilladte testkilder, opbevaring og kriterier for en alarm. En hændelse kan skyldes inventarscanning, en fejl eller et forsøg på adgang. Sammenhold den med andre signaler. Hold lokkemålet ude af skolens almindelige net og få miljøet kontrolleret, før øvelsen går i gang.",
                "Prøv selv. Tegn en isoleret honeypot og én tilladt vej til logning. Lav en forventning for et fiktivt testkald og for en forbindelse, der skal blive afvist.",
                "Tilknyttede målpinde · 22253 nr. 3."
            ],
            Honeypot, Logging),
        Card("12-beredskab", "Når alarmen går",
            "Cyberbien håndterer en alarm, afgrænser et ramt system og planlægger gendannelse.",
            Response,
            "Øv en fælles reaktion fra første observation til kontrolleret tilbagevenden.",
            [
                "Forbered kontaktveje, ansvar, kommunikation og kriterier for eskalering før en hændelse. Kontrollér alarmen, afgræns dens omfang og dokumentér observationer. Inddæm sammen med drift og beskyt relevant evidens. Gendan fra en kontrolleret tilstand og efterprøv, at årsagen er håndteret.",
                "Drift kan ville genåbne hurtigt, mens sikkerhed har brug for at undersøge spor. Aftal, hvem der beslutter, hvad der dokumenteres, og hvornår konflikten eskaleres. Del kun oplysninger, som modtageren har brug for. Brug læringen til at ændre både beredskab og tekniske værn.",
                "Prøv selv. Spil tre roller i en skrivebordsøvelse. Ved et mistænkeligt login skal I aftale, hvem der vurderer, hvad der skal bevares, og hvordan eleverne får besked.",
                "Tilknyttede målpinde · 22253 nr. 3 · 22848 nr. 3–4 · 22849 nr. 2 · 22999 nr. 10."
            ],
            IncidentResponse, Privacy, Csf),
        Card("13-tls-og-certifikater", "TLS sikrer forbindelsen",
            "Cyberbien kontrollerer serverens certifikat før en beskyttet TLS-forbindelse oprettes.",
            Identity,
            "Brug TLS med certifikatvalidering til at beskytte data under transport.",
            [
                "HTTPS er HTTP over TLS. Den normale validering kontrollerer bl.a., at certifikatet er gyldigt, passer til servernavnet og kæder til en betroet udsteder. Brug tidssvarende TLS og rammeværkets normale validering. SSL i fagteksten henviser til en historisk protokolfamilie.",
                "PKI omfatter certifikater, betroede udstedere og håndtering af nøgler. Når TLS afsluttes ved en proxy, skal strækningen videre til tjenesten også vurderes. Et certifikat bekræfter den forventede serveridentitet, mens appen stadig skal godkende brugere og kontrollere rettigheder.",
                "Prøv selv. Tjek, hvad en klient gør med korrekt navn og med et certifikat, der gælder et andet navn, i en lokal testtjeneste. OpenSSL kan bruges til at undersøge certifikat, TLS-version og valideringsresultat.",
                "Tilknyttede målpinde · 16484 nr. 10 · 22999 nr. 8 · 16857 nr. 4 · 16474 nr. 23 · 16856 nr. 20 · 20738 nr. 5 · 16479 nr. 16 · 16480 nr. 8 · 20555 nr. 8–9, 11."
            ],
            Tls, OpenSsl),
        Card("14-hash-og-kryptering", "Hash eller kryptering?",
            "Cyberbien sammenligner et envejsfingeraftryk med data, der kan låses op med en krypteringsnøgle.",
            Identity,
            "Vælg hash til kontrol eller adgangskodeverifikation og kryptering til data, der skal læses igen.",
            [
                "En hash afleder et fingeraftryk fra input. En hashværdi kan bruges til integritetskontrol, hvis den forventede værdi kommer fra en betroet kilde. Til adgangskoder bruges en langsom, egnet funktion som Argon2id med en unik saltværdi og en valgt omkostning. MD5 er et historisk eksempel, som er uegnet til adgangskodelagring.",
                "Kryptering er vendbar med den rigtige nøgle. LUKS kan beskytte data på en disk i hvile, og EFS kan kryptere filer på Windows. TLS beskytter data under transport. Planlæg, hvem der kan hente nøglen, hvordan den opbevares, og hvad der sker, hvis den lækker. Diskkryptering har begrænset effekt, når systemet er åbent og kontoen har adgang.",
                "Prøv selv. Vælg passende værn for en adgangskode, en fil, der skal sendes, og en databasekopi på en slukket maskine. Forklar, hvilke nøgler eller saltværdier du behøver.",
                "Tilknyttede målpinde · 16484 nr. 10, 13 · 22999 nr. 8 · 16858 nr. 12 · 16474 nr. 23 · 16875 nr. 1."
            ],
            PasswordStorage, CryptoStorage, Tls),
        Card("15-noegler-og-secrets", "Hold nøgler ude af koden",
            "Cyberbien flytter en nøgle ud af en kodefolder og ind i en adgangsbegrænset nøgleboks.",
            Identity,
            "Giv tjenester egne legitimationsoplysninger og planlæg rotation, udløb og tilbagekaldelse.",
            [
                "Lad tjenestens identitet hente de nødvendige hemmeligheder fra en godkendt løsning. En nøgleboks som Vault kan f.eks. udstede kortlivede databasebrugere til bestemte roller. Begræns adgang til nøgleboksen og log opslag uden at logge værdien. Hold hemmeligheder væk fra kildekode, containerimages, scripts og logudskrifter.",
                "Aftal udløb, rotation, tilbagekaldelse og gendannelse af nøgler. Hvis en nøgle er havnet i en commit, så tilbagekald og erstat den. Undersøg derefter hvor den kan være brugt eller kopieret, og ryd historikken i samarbejde med ejeren.",
                "Prøv selv. Tegn et øveflow fra en app til en database. Beskriv, hvem der udsteder legitimationsoplysninger, og hvordan du kan bevise, at en udløbet adgang bliver afvist.",
                "Tilknyttede målpinde · 22999 nr. 9 · 16862 nr. 2 · 16474 nr. 23 · 20738 nr. 7."
            ],
            Secrets, Vault),
        Card("16-sql-injection", "Hold data ude af SQL-koden",
            "Cyberbien holder SQL-skabelon og brugerens parameter adskilt på vej til databasen.",
            Code,
            "Brug parametre til brugerdata og begræns, hvad appens databasekonto må gøre.",
            [
                "En parameteriseret forespørgsel sender SQL-skabelonen og dataværdierne hver for sig til driveren. Brug parametre til værdier, og vælg dynamiske feltnavne fra en fast liste af kendte navne. Tjek også rå SQL i et ORM og dynamisk SQL i gemte procedurer.",
                "Giv appens konto de nødvendige rettigheder. Brug f.eks. et view med et afgrænset sæt kolonner og giv kontoen adgang til viewet, mens direkte tabeladgang er lukket. Kontrollér, at rettigheder og viewets udførelseskontekst faktisk begrænser læsning og skrivning.",
                "Prøv selv. Søg i en øvedatabase efter en værdi med en apostrof. Bekræft, at den behandles som en dataværdi, og at appens konto får afslag på at læse en tabel uden for sin rolle.",
                "Tilknyttede målpinde · 16484 nr. 14 · 16474 nr. 9–10, 14, 23 · 16856 nr. 14–16."
            ],
            Sql, Grant, Database),
        Card("17-xss-csrf", "Beskyt browseren",
            "Cyberbien sammenligner tekst, der vises sikkert i browseren, med en ændringsanmodning, der bliver valideret.",
            Code,
            "Håndtér brugerinput sikkert ved visning og kontrollér anmodninger, der ændrer data.",
            [
                "XSS opstår, når utroværdigt indhold ender som kode i browseren. Brug passende outputkodning for den kontekst, indholdet vises i, og anvend sikre DOM-metoder som textContent til almindelig tekst. Hvis brugere må indsende HTML, kræver det en vedligeholdt sanitiseringsløsning. En CSP kan begrænse skade som ekstra værn.",
                "CSRF udnytter typisk en eksisterende cookiesession til at få browseren til at udføre en handling. Brug rammeværkets CSRF-beskyttelse ved relevante ændringskald. SameSite kan hjælpe, og et GET-kald bør ikke ændre data. En SPA skal også lade serveren håndhæve adgang til data.",
                "Prøv selv. Vis teksten <b>Hej</b> som almindelig tekst i en lokal øveapp. Prøv dernæst et aftalt ændringskald uden korrekt CSRF-token i den cookie-baserede øveapp, og tjek at det afvises.",
                "Tilknyttede målpinde · 16484 nr. 14 · 16479 nr. 16 · 16476 nr. 17–18 · 16475 nr. 16."
            ],
            Xss, Csrf),
        Card("18-api-adgang", "Hvem må se objektet?",
            "Cyberbien sammenholder to indloggede identiteter med ejerskab til hver deres API-objekt.",
            Code,
            "Kontrollér den ønskede handling og det konkrete objekt ved hvert API-kald.",
            [
                "Authentication etablerer identiteten. Authorization afgør, hvilke handlinger den identitet må udføre. En gyldig session er første trin. Serveren skal også se, om brugeren har lov til at læse eller ændre netop det dokument, kaldet peger på. En skjult knap i appen kan gøre den lettere at bruge. Rettigheden håndhæves stadig i API'et.",
                "Brug en vedligeholdt identitetsløsning, beskyt sessioner eller tokens, og kontrollér for appens relevante krav til udløb, udsteder og målgruppe. Mobilapps skal bruge enhedens beskyttede lager til nødvendige legitimationsoplysninger. Ved ændring af roller skal adgang prøves igen.",
                "Prøv selv. I en lokal øveapp har testbruger A og B ét dokument hver. Bekræft, at A kan åbne sit eget, og at serveren afviser samme handling på B's dokument.",
                "Tilknyttede målpinde · 16484 nr. 12 · 16479 nr. 16 · 16480 nr. 8 · 16475 nr. 16 · 16477 nr. 11."
            ],
            ApiSecurity, Authorization, Authentication),
        Card("19-sikkerhedstest", "Test i et aftalt miljø",
            "Cyberbien afgrænser et testmiljø, finder en fejl og gentager testen efter rettelsen.",
            Code,
            "Lav en testplan, der både viser, hvad systemet tillader, og hvad det skal afvise.",
            [
                "Aftal system, testdata, tidspunkt, tilladte værktøjer og stopbetingelser. Byg testplanen fra funktionelle krav og sikkerhedskrav, f.eks. et relevant ASVS-krav om adgangskontrol. Kombinér kodegennemgang, automatiserede test af kendte grænser og en aftalt, skånsom webscanning i eget miljø.",
                "Dokumentér forventet og faktisk resultat med reproduktionstrin og betydning. Vurdér fund før en release, og gentag testen efter rettelsen. En automatisk scanning kan give både falske alarmer og overse fejl. Integrér en afgrænset regressionspakke i udviklingscyklussen og brug manuelle checks, hvor de giver ny viden.",
                "Prøv selv. Kør den scriptede test i jeres lokale C#-øveapp. Testens fixture opretter dokumenter til hver sin testbruger og logger clientA ind som A. Her svarer API'et med 403 ved manglende adgang. Brug 404 i forventningen, hvis det er jeres aftalte kontrakt.",
                "Tilknyttede målpinde · 16484 nr. 1–6, 11, 15 · 22253 nr. 3."
            ],
            Asvs, Zap, Ssf) with
        {
            Code = """
                // clientA er indlogget som A på den lokale testserver.
                var eget = await clientA.GetAsync("/documents/test-a");
                var andet = await clientA.GetAsync("/documents/test-b");

                Assert.Equal(HttpStatusCode.OK, eget.StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, andet.StatusCode);
                """
        },
        Card("20-opdateringer-og-sbom", "Opdatér med overblik",
            "Cyberbien finder en udsat komponent i en materialeliste og kontrollerer den opdaterede version.",
            Operations,
            "Brug komponentoversigt, aktuelle advisories og en afprøvet vej til opdatering.",
            [
                "En SBOM er en oversigt over komponenter og versioner i et produkt. Et CVE-id identificerer en offentlig registreret sårbarhed. Undersøg, om den berørte kode og version er i brug og kan nås, hvor alvorlig konsekvensen er, og om sårbarheden bliver udnyttet. Brug f.eks. CISA's KEV-katalog som ét signal til prioritering.",
                "Byg artefakter fra kontrollerede kilder. Brug passende signatur- og provenienskontrol, små rettigheder i CI og ændringer gennem review. Afprøv rettelsen i et testmiljø og brug en plan for udrulning og tilbagerulning. Dokumentér verificeret version efter deploy.",
                "Prøv selv. Find en komponent i et fiktivt projekts inventarliste. Skriv, hvad du skal vide for at vurdere en advisory, og hvordan du vil kontrollere, at produktet bruger den rettede version.",
                "Tilknyttede målpinde · 16484 nr. 11 · 22253 nr. 4 · 16857 nr. 8 · 16876 nr. 8 · 20738 nr. 7."
            ],
            Ssf, Sbom, Kev),
        Card("21-cloud-ansvar", "Hvem sikrer hvad i cloud?",
            "Cyberbien viser forskellen på IaaS, PaaS og SaaS som tre stakke med forskellig ansvarsdeling.",
            Operations,
            "Placér ansvaret for hver sikkerhedskontrol, før du sætter en tjeneste i drift.",
            [
                "I IaaS styrer kunden ofte operativsystemet i sin virtuelle maskine. I PaaS driver leverandøren typisk platformen, mens kunden styrer app og opsætning. I SaaS leverer leverandøren selve applikationen. Kontrollér den konkrete aftale og tjeneste. Kundens identiteter, adgang til data og indstillinger kræver opmærksomhed i alle tre modeller.",
                "Start med private ressourcer, adgang efter roller, MFA for relevante konti, netværksgrænser, passende kryptering og central logning. Giv hvert værn en ejer og en kontrol, der kan bekræfte, at det virker.",
                "Prøv selv. Sammenlign en fiktiv virtuel maskine med en administreret database. Sæt navn på ejeren af patching, roller, netværksadgang, backup og hændelseslogning i begge tilfælde.",
                "Tilknyttede målpinde · 22999 nr. 1–3, 4, 6, 10 · 20738 nr. 7 · 16480 nr. 8 · 16875 nr. 1."
            ],
            SharedResponsibility, Cis),
        Card("22-iot-og-firmware", "Beskyt IoT-enheden",
            "Cyberbien forbinder en identificerbar sensor i et særskilt net og godkender en verificeret firmwareopdatering.",
            Operations,
            "Giv IoT-enheder entydig identitet, begrænset adgang og en plan for opdatering.",
            [
                "Start med en enhedsfortegnelse og et trusselsbillede. Skift fælles standardadgange, begræns tjenester og grænseflader, og sæt enheder i et segment med få tilladte forbindelser. Brug individuelle identiteter og adgangspolitikker. En sensor behøver f.eks. kun kunne publicere egne målinger til en aftalt MQTT-broker.",
                "Tillad kun godkendte og verificerede software- eller firmwareopdateringer, og planlæg fallback ved fejl og forældelse. Afklar, hvad enheden gør, hvis netværket forsvinder, og hvordan driften kan se dens sikkerhedstilstand.",
                "Prøv selv. Lav en plan for tre fiktive sensorer. Hvem ejer dem, hvordan tilbagekaldes én sensors adgang, og hvordan beviser I, at den opdaterede version kører?",
                "Tilknyttede målpinde · 17682 nr. 5, 14 · 17683 nr. 5, 8."
            ],
            Iot, Mqtt, Cis),
        Card("23-standarder-og-regler", "Styr sikkerheden",
            "Cyberbien adskiller regler for organisationen, krav til digitale produkter og en standard for sikkerhedsledelse.",
            Risk,
            "Skeln mellem lovkrav, risikostyring og konkrete kontroller.",
            [
                "NIS2 og den danske NIS 2-lov retter sig mod omfattede enheders cybersikkerhedsforanstaltninger og hændelsesrapportering. CRA stiller cybersikkerhedskrav til omfattede produkter med digitale elementer gennem deres livscyklus. GDPR og databeskyttelsesloven er relevante, når man behandler personoplysninger. Afklar omfang i den officielle tekst og hos den ansvarlige i organisationen.",
                "ISO/IEC 27001 angiver krav til et ledelsessystem for informationssikkerhed, og ISO/IEC 27002 giver vejledning om kontroller. CIS Controls er en prioriteret samling af sikkerhedstiltag. Brug officielle vejledninger, dokumentér ejerskab, og planlæg hvordan I beviser, at kontroller virker. Etik i praksis betyder bl.a. aftalt test, mindst muligt databehov og en ansvarlig vej til rapportering.",
                "Prøv selv. I en fiktiv skoleportal skal I finde én kontrol, dens ejer og et stykke bevis. Diskutér derefter, hvilke fakta der skal afklares, før I ved, hvilke regler der gælder.",
                "Tilknyttede målpinde · 22253 nr. 2, 3 · 22848 nr. 1 · 22849 nr. 1, 2 · 16867 nr. 9 · 16858 nr. 14 · 16857 nr. 11 · 20555 nr. 8–9 · 16875 nr. 6."
            ],
            Nis, Cra, Iso, IsoControls, Cis, Gdpr, PrivacyAct),
        Card("24-fysisk-sikkerhed", "Beskyt det fysiske rum",
            "Cyberbien kontrollerer et gæstekort ved en udstyrszone og registrerer kun det nødvendige.",
            Risk,
            "Før adgangen til udstyr, medier og serverrum ind i risikovurderingen.",
            [
                "Lav fysiske zoner for besøgende, arbejdspladser og kritisk udstyr. Aftal, hvem der må udstede og tilbagekalde adgang, hvordan gæster ledsages, og hvordan medier opbevares og bortskaffes. Følg op på, om døre, alarmer og nødrutiner virker sammen.",
                "Overvej også oversvømmelse, strømudfald, tyveri og adgang til netværksporte. Registrér det nødvendige for at kunne undersøge en hændelse, og planlæg en slettefrist. Kontrol skal afvejes mod arbejdsmiljø, privatliv og sikker evakuering.",
                "Prøv selv. Tegn en plan for et fiktivt serverrum. Angiv adgang for drift, rengøring og gæster. Forklar, hvordan en mistet adgangsbrik håndteres, og hvordan I kan afprøve det.",
                "Tilknyttede målpinde · 22253 nr. 5 · 22848 nr. 3–4 · 22849 nr. 4."
            ],
            SecurityControls, Privacy)
    ];

    private static CharacterIllustration Card(string file, string title, string alt, string stage, string summary,
        IReadOnlyList<string> explanation, params CharacterLink[] sources) =>
        new(title, ImageFolder + file + ".png", alt, 1536, 1024)
        {
            Preview = ImageFolder + file + ".webp",
            Stage = stage,
            Summary = summary,
            Explanation = explanation,
            Sources = sources
        };

    private static CharacterLink Source(string label, string href) => new(label, href, true, false);
}
