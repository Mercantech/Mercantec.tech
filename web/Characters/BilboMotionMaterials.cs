namespace Mercantec.Tech.Web.Characters;

internal static class BilboMotionMaterials
{
    private const string SceneFolder = "/images/characters/bilbo/scenes/";

    public static IReadOnlyList<CharacterIllustration> Scenes { get; } =
    [
        Scene("bodmesteren", "Bodmesteren", "Bilbo står barfodet i sin grønne vest og holder to bestillingskort foran en tavle om POST /orders.", 1254, 1254),
        Scene("marmeladeordren", "Fra klik til syltetøjsordre", "Bilbo holder et bestillingskort ved en bærbar computer og et glas brombærsyltetøj ved bodens disk."),
        Scene("ordre-og-lagerbogen", "Ordren og lagerbogen", "Bilbo sammenholder en bestilling og en lagerbog med syltetøjsglassene i bodens baglokale."),
        Scene("spor-i-loggen", "Spor i loggen", "Bilbo undersøger et afvigende signal på skærmen i den lukkede bod, mens natten falder på."),
        Scene("fancy-friday", "Fancy Friday", "Bilbo i grøn fløjlsvest og blommefarvet butterfly skåler ved et bord i en varm kro."),
        Scene("hawaii-onsdag", "Hawaii-onsdag", "Bilbo byder på brombærsyltetøj på en terrasse ved havet. Han har en koralfarvet hawaiiskjorte på under sin grønne vest.")
    ];

    public static IReadOnlyList<CharacterAnimation> Animations { get; } =
    [
        new(
            "tjek-bestillingen", "Tjek bestillingen", "Validering",
            "Bilbo kontrollerer inputtet i sit Minimal API-endpoint. Er bestillingen ugyldig, svarer han med HTTP 400, før lageret ændres.",
            "/images/characters/bilbo/animations/tjek-bestillingen.gif",
            "/images/characters/bilbo/animations/tjek-bestillingen.webp",
            "Bilbo finder et tomt felt på et bestillingskort og afviser ordren. Et nyt kort med alle tre felter udfyldt bliver accepteret.",
            512, 512),
        new(
            "rul-ordren-tilbage", "Rul ordren tilbage", "EF Core og transaktioner",
            "Gem ordren og lagerændringen i samme databasetransaktion. Fejler en del før commit, ruller transaktionen begge ændringer tilbage.",
            "/images/characters/bilbo/animations/rul-ordren-tilbage.gif",
            "/images/characters/bilbo/animations/rul-ordren-tilbage.webp",
            "Ordresedlen lægges i bakken, og et glas fjernes fra hylden. Da ordren fejler, bliver bakken tom igen, og begge glas er tilbage på hylden.",
            512, 512),
        new(
            "samme-noegle-samme-ordre", "Samme nøgle, samme ordre", "Idempotens",
            "Hvis svaret går tabt, kan klienten sende samme bestilling og idempotensnøgle igen. Et endpoint, der har gemt nøglen og resultatet, kan returnere den eksisterende ordre uden at oprette en ny.",
            "/images/characters/bilbo/animations/samme-noegle-samme-ordre.gif",
            "/images/characters/bilbo/animations/samme-noegle-samme-ordre.webp",
            "Bilbo får samme bestillingskort med en blå nøgle igen, da svaret går tabt. Den samme kvittering og det samme glas bliver på disken.",
            512, 512)
    ];

    private static CharacterIllustration Scene(string file, string title, string alt, int width = 1536, int height = 1024) =>
        new(title, SceneFolder + file + ".png", alt, width, height)
        {
            Preview = SceneFolder + file + ".webp"
        };
}
