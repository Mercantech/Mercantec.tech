namespace Mercantec.Tech.Web.Characters;

internal static class DuckMotionMaterials
{
    public static IReadOnlyList<CharacterIllustration> Scenes { get; } =
    [
        new("Detektivens sporvæg", "/images/characters/gummianden/scenes/detektivens-sporvaeg.png", "Spordetektiven bruger sin lup til at undersøge rækkefølgen af tre forsøg på en opslagstavle.", 1536, 1024)
        {
            Preview = "/images/characters/gummianden/scenes/detektivens-sporvaeg.webp"
        },
        new("Tænk højt hos Lyttemakkeren", "/images/characters/gummianden/scenes/lyttemakkerens-studie.png", "Gummianden i orange strik lytter opmærksomt ved et skrivebord med bærbar computer og notesbog.", 1536, 1024)
        {
            Preview = "/images/characters/gummianden/scenes/lyttemakkerens-studie.webp"
        },
        new("Testpilotens grænsebane", "/images/characters/gummianden/scenes/testpilotens-graensebane.png", "Gummianden i flyverhjelm afprøver en modelbane med forskellige mængder last.", 1536, 1024)
        {
            Preview = "/images/characters/gummianden/scenes/testpilotens-graensebane.webp"
        },
        new("Logvagtens natbord", "/images/characters/gummianden/scenes/logvagtens-natbord.png", "Logvagten følger en papirrulle frem til det første røde mærke i loggen i en rolig nattevagt.", 1536, 1024)
        {
            Preview = "/images/characters/gummianden/scenes/logvagtens-natbord.webp"
        }
    ];

    public static IReadOnlyList<CharacterAnimation> Animations { get; } =
    [
        new(
            "stop-ved-breakpoint", "Stop ved et breakpoint", "Breakpoint",
            "Sæt et breakpoint på en linje, der bliver kørt. Debuggeren pauser, når den når linjen, så du kan se de aktuelle værdier. Vælg Fortsæt for at køre videre.",
            "/images/characters/gummianden/animations/stop-ved-breakpoint.gif",
            "/images/characters/gummianden/animations/stop-ved-breakpoint.webp",
            "Spordetektiven holder øje med markeringen i en kodevisning. Koden standser ved det røde breakpoint, og anden undersøger værdierne, før markeringen fortsætter.",
            512, 512),
        new(
            "trin-for-trin", "Trin for trin i debuggeren", "Step Over",
            "Brug Step Over, når du vil udføre næste trin uden at gå ind i et metodekald. Se efter, hvilken linje debuggeren standser ved, og hvad der er sket med værdierne siden sidste stop.",
            "/images/characters/gummianden/animations/trin-for-trin.gif",
            "/images/characters/gummianden/animations/trin-for-trin.webp",
            "Værkstedsanden bruger et håndtag til at flytte den gule markering ned gennem tre trin, mens værdimålerens lamper ændrer sig.",
            512, 512),
        new(
            "goer-fejlen-mindre", "Gør fejlen mindre", "Minimal reproduktion",
            "Genskab fejlen. Fjern ét input ad gangen, og kør igen. Kan en mindre mængde input stadig udløse samme fejl, har du et kortere eksempel at undersøge.",
            "/images/characters/gummianden/animations/goer-fejlen-mindre.gif",
            "/images/characters/gummianden/animations/goer-fejlen-mindre.webp",
            "Grænsetestpiloten fjerner input fra sin prøvebænk. Det røde fejllys bliver ved med at lyse, selv når der kun ligger ét input tilbage.",
            512, 512)
    ];
}
