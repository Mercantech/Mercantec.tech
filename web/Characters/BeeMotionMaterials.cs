namespace Mercantec.Tech.Web.Characters;

internal static class BeeMotionMaterials
{
    public static IReadOnlyList<CharacterIllustration> Scenes { get; } =
    [
        new("Lokkehivens drivhus", "/images/characters/bee/scenes/lokkehivens-drivhus.png", "Cyberbien undersøger en ravfarvet lokkeserver i et afgrænset drivhuslaboratorium.", 1536, 1024)
        {
            Preview = "/images/characters/bee/scenes/lokkehivens-drivhus.webp"
        },
        new("Nattevagt i sikkerhedscentret", "/images/characters/bee/scenes/nattevagt-i-soc.png", "Cyberbien sammenligner tidspunkterne for to signaler på en skærm, mens regnen falder uden for sikkerhedscentret.", 1536, 1024)
        {
            Preview = "/images/characters/bee/scenes/nattevagt-i-soc.webp"
        },
        new("Testlaboratoriet", "/images/characters/bee/scenes/afgraenset-testlaboratorium.png", "Cyberbien sætter en tilladelsesbrik i pulten foran laboratoriets afgrænsede øvemiljø.", 1536, 1024)
        {
            Preview = "/images/characters/bee/scenes/afgraenset-testlaboratorium.webp"
        },
        new("Øvelse ved hændelsesbordet", "/images/characters/bee/scenes/oevelse-ved-haendelsesbordet.png", "Cyberbien øver en hændelse på en model, hvor en rød enhed er isoleret, mens de blå enheder stadig er forbundet.", 1536, 1024)
        {
            Preview = "/images/characters/bee/scenes/oevelse-ved-haendelsesbordet.webp"
        }
    ];

    public static IReadOnlyList<CharacterAnimation> Animations { get; } =
    [
        new(
            "honeypot-fra-signal-til-alarm", "Fra lokkemål til alarm", "Honeypot",
            "En lokkeserver i et afgrænset miljø registrerer et uventet forsøg på kontakt. En alarm giver Cyberbien anledning til at undersøge hændelsen i loggen.",
            "/images/characters/bee/animations/honeypot-fra-signal-til-alarm.gif",
            "/images/characters/bee/animations/honeypot-fra-signal-til-alarm.webp",
            "Et lyssignal når lokkeserveren. Der kommer mærker i loggen, og til sidst tændes alarmlampen.",
            512, 512),
        new(
            "stop-tjek-rapport", "Stop, tjek og rapportér", "Phishing",
            "Lad linket være. Kontakt afsenderen via et nummer eller en kanal, du kender i forvejen, og rapportér den mistænkelige besked gennem din organisations kanal.",
            "/images/characters/bee/animations/stop-tjek-rapport.gif",
            "/images/characters/bee/animations/stop-tjek-rapport.webp",
            "Cyberbien standser ved en mistænkelig mail, finder sin telefon frem og vælger til sidst den blå rapporteringsknap.",
            512, 512),
        new(
            "isoler-den-ramte-enhed", "Isolér den ramte enhed", "Hændelseshåndtering",
            "Når beredskabsplanen kalder på isolation, afskærer Cyberbien den ramte enheds netværksadgang. De raske enheder kan stadig tale sammen, mens holdet undersøger hændelsen.",
            "/images/characters/bee/animations/isoler-den-ramte-enhed.gif",
            "/images/characters/bee/animations/isoler-den-ramte-enhed.webp",
            "Cyberbien opdager en rød advarselslampe. Forbindelsen til den ramte enhed afbrydes, mens to blå enheder bliver ved med at være forbundet.",
            512, 512)
    ];
}
