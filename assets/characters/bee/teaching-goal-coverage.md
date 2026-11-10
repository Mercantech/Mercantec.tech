# Cyberbien og målpindene

Gennemgået 10. november 2026 for datatekniker med speciale i programmering. Fagkoder og numre følger [Målpindes offentlige fagdata](https://maalpinde.lup.dk/api/uddannelser/1205/programmering?ikraft=latest). Tabellen samler de faglige emner, Cyberbiens kort introducerer eller bruger i en kort øvelse. Praktiske krav som at konfigurere et fysisk udstyr, gennemføre en hel release eller administrere en produktionsløsning kræver selvstændige laboratorieøvelser.

Den lokale plan placerer Softwaretest og -sikkerhed (16484) på H3/H4, Serveradministration og sikkerhed (16858) på H2 og Informationssikkerhed og awareness (22253) i VUF-forløbet Deploy or die. Informations- og cybersikkerhed 1 og 2 (22848 og 22849), Cloud-sikkerhed I (22999) og Netværkssikkerhed (16867) er valgfrie fag uden for den viste lokale H1–H6- og VUF-plan. Deres sikkerhedsmål er med som faglig udvidelse. [Lokal plan](https://maalpinde.lup.dk/) · [Deploy or die](https://maalpinde.lup.dk/vuf/#deploy-or-die)

## Sikkerhedsfag og Softwaretest og -sikkerhed

| Fag | Nr. | Kort | Emner i kortene |
|---|---|---|---|
| 16484 · Softwaretest og -sikkerhed | 1–6 | [19](#kort-19) | Websikkerhed, testplan, testmetode, releasevurdering, scriptet test og automatisering i en sikkerhedscase. |
|  | 7–8 | [08](#kort-08) | Overbelastning, svartid og flaskehalse som del af en tilgængelighedsvurdering. |
|  | 10 | [13](#kort-13), [14](#kort-14) | Kryptering i webapps og tidssvarende TLS. |
|  | 11 | [19](#kort-19), [20](#kort-20) | Krav og verifikation med ASVS samt advisories og sårbarhedsprioritering. |
|  | 12 | [04](#kort-04), [18](#kort-18) | Godkendelse og autorisation i en app. |
|  | 13 | [14](#kort-14) | Hashing og sikker lagring af adgangskoder. MD5 behandles som historisk eksempel. |
|  | 14 | [16](#kort-16), [17](#kort-17) | SQL-injektion og XSS med modforanstaltninger. |
|  | 15 | [19](#kort-19) | Aftalt brug af webscanning og gentest. |
| 22253 · Informationssikkerhed og awareness | 1 | [02](#kort-02), [03](#kort-03) | Phishing, malware og ransomware. |
|  | 2 | [23](#kort-23) | NIS2 og CRA i organisationer og produktudvikling. |
|  | 3 | [01](#kort-01), [11](#kort-11), [12](#kort-12), [19](#kort-19), [23](#kort-23) | Etik, afgrænset test, dataminimering og håndtering af fund. |
|  | 4 | [02](#kort-02), [04](#kort-04), [20](#kort-20) | Adgangskoder, MFA, rapportering og opdateringer. |
|  | 5 | [01](#kort-01), [24](#kort-24) | Grundlæggende risikovurdering i en konkret situation. |
| 22848 · Informations- og cybersikkerhed 1 | 1 | [01](#kort-01), [02](#kort-02), [23](#kort-23) | Databeskyttelse, trusler og risikovurdering. |
|  | 2 | [04](#kort-04), [05](#kort-05) | Adgangskontrolpolitik og adgangskodepolitik. |
|  | 3–4 | [12](#kort-12), [24](#kort-24) | Interessekonflikter mellem drift, sikkerhed og privatliv samt eskalering. |
|  | 5 | [09](#kort-09), [10](#kort-10) | Afvigelser, falske alarmer og modelbaseret trusselsdetektion. |
| 22849 · Informations- og cybersikkerhed 2 | 1 | [23](#kort-23) | ISO 27001/27002 og CIS Controls samt myndighedsvejledning. |
|  | 2 | [12](#kort-12), [23](#kort-23) | Compliance, regulering og beredskab. |
|  | 3 | [04](#kort-04) | MFA og de tre typer faktorer. |
|  | 4 | [24](#kort-24) | Fysiske zoner, gæsteadgang og implementeringsforslag. |
| 22999 · Cloud-sikkerhed I | 1–3 | [01](#kort-01), [21](#kort-21) | IaaS/PaaS/SaaS, risici og delt ansvar. |
|  | 4 | [04](#kort-04), [05](#kort-05), [21](#kort-21) | RBAC og MFA i cloud. |
|  | 5 | [05](#kort-05) | Katalogtjenester, FreeIPA/OpenLDAP og begrænsede roller. |
|  | 6 | [07](#kort-07), [08](#kort-08), [09](#kort-09), [21](#kort-21) | Virtuelle net, firewall, VPN og IDS/IPS. |
|  | 7 | [08](#kort-08), [09](#kort-09) | DDoS, VPN og indbrudsdetektion/-forebyggelse. |
|  | 8 | [13](#kort-13), [14](#kort-14) | Kryptering under transport og i hvile, herunder LUKS. |
|  | 9 | [15](#kort-15) | Vault, udstedelse, rotation og tilbagekaldelse. |
|  | 10 | [10](#kort-10), [12](#kort-12), [21](#kort-21) | Central logning, monitorering og håndtering af hændelser. |
| 16867 · Netværkssikkerhed | 1 | [01](#kort-01), [07](#kort-07), [08](#kort-08) | Trusler og sårbarheder i et netværk. |
|  | 2 | [07](#kort-07) | Lag 2/3-adskillelse og switchkontroller. |
|  | 3–4 | [10](#kort-10) | Syslog, SNMP, NTP og central logserver. |
|  | 5 | [08](#kort-08) | Firewallregler og kontrolleret netværksadgang. |
|  | 6 | [09](#kort-09) | Netværksbaseret indbrudsforebyggelse. |
|  | 7 | [08](#kort-08) | IPsec til site-to-site og klientadgang. |
|  | 8 | [05](#kort-05) | Authentication, authorization og accounting. |
|  | 9 | [23](#kort-23) | Regler og relevante sikkerhedsstandarder. |

Softwaretest 16484 nr. 2–6 bruges i en sikkerhedscase. Nr. 7–8 har overlap med tilgængelighed og overbelastning. Almindelig UX-test (nr. 9) hører til fagets øvrige materiale. [Bæver og Ræv](https://lup.dk/characters) kan bruges til supplerende test- og forsvarscases.

## Overlap med andre fag

| Fag | Nr. | Kort | Emner i kortene |
|---|---|---|---|
| 16858 · Serveradministration og sikkerhed | 2–5 | [05](#kort-05), [06](#kort-06) | Katalog, grupper, rettigheder og tillidsforhold. |
|  | 6 | [03](#kort-03) | Beskyttet backup og testet gendannelse af AD. |
|  | 8–10 | [04](#kort-04), [05](#kort-05), [06](#kort-06) | Serverhærdning, lokale konti, adgangskoder og lockout. |
|  | 11 | [06](#kort-06), [08](#kort-08) | Værtsfirewall og begrænset eksponering. |
|  | 12 | [06](#kort-06), [10](#kort-10), [14](#kort-14) | Gruppepolitik, auditering, sikkerhedslog og krypteret lagring. |
|  | 13 | [06](#kort-06) | Gruppepolitikkers rækkevidde og virkning. |
|  | 14 | [23](#kort-23) | Regler og standarder i serverdrift. |
| 17678 · Netværk I | 1 | [07](#kort-07) | VLAN som grundlag for segmentering med kontroller mellem zoner. |
|  | 4–5 | [07](#kort-07) | WLAN-adgang, lag 2-sikkerhed og switchbeskyttelse. |
| 17679 · Netværk II | 5 | [07](#kort-07), [08](#kort-08) | ACL og netværkssikkerhed. |
| 1595 · Netværksdesign II | 4 | [01](#kort-01), [07](#kort-07), [08](#kort-08) | Sikkerhedsstrategi for zoner og netudstyr. |
|  | 8 | [08](#kort-08) | Beskyttet adgang over VPN. |
| 6287 · Trådløst netværk (WLAN) | 5, 7 | [07](#kort-07) | WLAN-sikkerhed, adgang, kryptering og historiske metoder. |
| 2113 · Network management | 1, 5 | [10](#kort-10) | SNMP og sikkerhedsrelateret netværksovervågning. |
| 16857 · Serverteknologi | 1, 3 | [06](#kort-06), [08](#kort-08) | Værtsfirewall og fjernadgang over VPN. |
|  | 4 | [05](#kort-05), [08](#kort-08), [13](#kort-13) | RADIUS, IPsec og PKI. |
|  | 8 | [03](#kort-03), [10](#kort-10), [20](#kort-20) | Backup, overvågning og opdateringer. |
|  | 11 | [23](#kort-23) | Regler og standarder i serverdrift. |
| 16862 · Serverautomatisering I | 2 | [06](#kort-06), [15](#kort-15) | Begrænsede rettigheder og håndtering af hemmeligheder i PowerShell. |
| 16876 · Deployment Service | 8 | [06](#kort-06), [20](#kort-20) | Sikkerhedspolitik og kontrolleret udrulning på liveinstallationer. |
| 16474 · Databaseprogrammering | 9–10, 14 | [16](#kort-16) | Views, parametre, rettigheder og SQL-injektion. |
|  | 23 | [05](#kort-05), [13](#kort-13), [14](#kort-14), [15](#kort-15), [16](#kort-16) | Adgang og kryptering ved databasedesign. |
| 16856 · Serverteknologi - Databaseserver | 14–16 | [05](#kort-05), [16](#kort-16) | Databasekonti, views, tilladelser og adgang til objekter. |
|  | 18, 22 | [03](#kort-03) | Backupstrategi, restore og gendannelse efter skade. |
|  | 20 | [13](#kort-13) | TLS og serveridentitet ved klientforbindelse. |
| 17682 · IoT og Embeddede systemer | 5, 14 | [01](#kort-01), [22](#kort-22) | IoT-risiko, forebyggelse og plan for software- og firmwareopdatering. |
| 17683 · IoT og Embeddede Linux systemer | 5, 8 | [05](#kort-05), [06](#kort-06), [22](#kort-22) | Rettigheder, sikker opsætning og løbende opdatering. |
| 20738 · DevNet | 5 | [13](#kort-13) | Sikre API-kald med HTTPS. |
|  | 7 | [15](#kort-15), [20](#kort-20), [21](#kort-21) | Sikker udrulning og beskyttelse af clouddata. |
| 16479 · App programmering II | 16 | [13](#kort-13), [17](#kort-17), [18](#kort-18) | Sikkerhed ved appdesign, forbindelser og adgange. |
| 16480 · App programmering III | 8 | [13](#kort-13), [18](#kort-18), [21](#kort-21) | Kryptering, godkendelse og autorisation ved eksterne data og cloudtjenester. |
| 16476 · Clientsideprogrammering | 17–18 | [17](#kort-17) | JavaScript-sikkerhed og XSS i klienten. |
| 16475 · GUI-programmering | 16 | [17](#kort-17), [18](#kort-18) | Session, klientoutput og API-adgang i en SPA. |
| 16477 · Serversideprogrammering | 11 | [04](#kort-04), [05](#kort-05), [18](#kort-18) | Applikationssikkerhed og brugeradministration. |
| 20555 · Big Data begreber, teknikker og processer | 8–9 | [01](#kort-01), [13](#kort-13), [23](#kort-23) | Anonymisering, transportkryptering og databeskyttelsesregler. |
|  | 11 | [01](#kort-01), [05](#kort-05), [10](#kort-10), [13](#kort-13) | Sikkerhedskontroller i en datapipeline. |
| 20556 · Big Data modeller og datamodellering | 8 | [03](#kort-03) | Versionering og backup som grundlag for gendannelse. |
| 16875 · Cloudteknologi | 1 | [08](#kort-08), [14](#kort-14), [21](#kort-21) | Cloudrisici, datasikkerhed og tilgængelighed. |
|  | 6 | [23](#kort-23) | Undersøg lov og standarder for cloudtjenester. |

## Undervisningskort

- <a id="kort-01"></a> **01** [Tegn trusselsmodellen](../../../web/wwwroot/images/characters/bee/infographics/01-trusselsmodel.png) · Risici og ansvar
- <a id="kort-02"></a> **02** [Stop phishing](../../../web/wwwroot/images/characters/bee/infographics/02-phishing.png) · Risici og ansvar
- <a id="kort-03"></a> **03** [Gendan efter ransomware](../../../web/wwwroot/images/characters/bee/infographics/03-ransomware-backup.png) · Risici og ansvar
- <a id="kort-04"></a> **04** [Beskyt dit login](../../../web/wwwroot/images/characters/bee/infographics/04-adgangskoder-mfa.png) · Identitet og data
- <a id="kort-05"></a> **05** [Giv mindst mulig adgang](../../../web/wwwroot/images/characters/bee/infographics/05-iam-rbac-aaa.png) · Identitet og data
- <a id="kort-06"></a> **06** [Hærd serveren](../../../web/wwwroot/images/characters/bee/infographics/06-haerd-serveren.png) · Netværk og drift
- <a id="kort-07"></a> **07** [Del netværket op](../../../web/wwwroot/images/characters/bee/infographics/07-segmentering.png) · Netværk og drift
- <a id="kort-08"></a> **08** [Styr trafikken](../../../web/wwwroot/images/characters/bee/infographics/08-firewall-vpn-ddos.png) · Netværk og drift
- <a id="kort-09"></a> **09** [Alarm eller blokering?](../../../web/wwwroot/images/characters/bee/infographics/09-ids-ips.png) · Overvågning og beredskab
- <a id="kort-10"></a> **10** [Følg sporet i loggen](../../../web/wwwroot/images/characters/bee/infographics/10-logs-og-tid.png) · Overvågning og beredskab
- <a id="kort-11"></a> **11** [En lokkedåse til angreb](../../../web/wwwroot/images/characters/bee/infographics/11-honeypot.png) · Overvågning og beredskab
- <a id="kort-12"></a> **12** [Når alarmen går](../../../web/wwwroot/images/characters/bee/infographics/12-beredskab.png) · Overvågning og beredskab
- <a id="kort-13"></a> **13** [TLS sikrer forbindelsen](../../../web/wwwroot/images/characters/bee/infographics/13-tls-og-certifikater.png) · Identitet og data
- <a id="kort-14"></a> **14** [Hash eller kryptering?](../../../web/wwwroot/images/characters/bee/infographics/14-hash-og-kryptering.png) · Identitet og data
- <a id="kort-15"></a> **15** [Hold nøgler ude af koden](../../../web/wwwroot/images/characters/bee/infographics/15-noegler-og-secrets.png) · Identitet og data
- <a id="kort-16"></a> **16** [Hold data ude af SQL-koden](../../../web/wwwroot/images/characters/bee/infographics/16-sql-injection.png) · Sikker kode og test
- <a id="kort-17"></a> **17** [Beskyt browseren](../../../web/wwwroot/images/characters/bee/infographics/17-xss-csrf.png) · Sikker kode og test
- <a id="kort-18"></a> **18** [Hvem må se objektet?](../../../web/wwwroot/images/characters/bee/infographics/18-api-adgang.png) · Sikker kode og test
- <a id="kort-19"></a> **19** [Test i et aftalt miljø](../../../web/wwwroot/images/characters/bee/infographics/19-sikkerhedstest.png) · Sikker kode og test
- <a id="kort-20"></a> **20** [Opdatér med overblik](../../../web/wwwroot/images/characters/bee/infographics/20-opdateringer-og-sbom.png) · Netværk og drift
- <a id="kort-21"></a> **21** [Hvem sikrer hvad i cloud?](../../../web/wwwroot/images/characters/bee/infographics/21-cloud-ansvar.png) · Netværk og drift
- <a id="kort-22"></a> **22** [Beskyt IoT-enheden](../../../web/wwwroot/images/characters/bee/infographics/22-iot-og-firmware.png) · Netværk og drift
- <a id="kort-23"></a> **23** [Styr sikkerheden](../../../web/wwwroot/images/characters/bee/infographics/23-standarder-og-regler.png) · Risici og ansvar
- <a id="kort-24"></a> **24** [Beskyt det fysiske rum](../../../web/wwwroot/images/characters/bee/infographics/24-fysisk-sikkerhed.png) · Risici og ansvar

Honeypot er en ekstra case til detektion, segmentering, logging og ansvarlig test. Øvelsen bruger et aftalt, isoleret miljø med fiktive data.
