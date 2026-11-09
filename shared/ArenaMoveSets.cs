namespace Mercantec.Tech.Contracts;

public sealed record ArenaMoveDefinition(string Id, string Key, string Name, string Effect);
public sealed record ArenaMoveSet(string Id, string Role, string Resource, IReadOnlyList<ArenaMoveDefinition> Moves);

/// <summary>Player-facing controls. Combat values remain authoritative in ArenaSimulation.</summary>
public static class ArenaMoveSets
{
    public static ArenaMoveSet? Find(string? avatarId) => avatarId switch
    {
        "bip01" => Bip01,
        "raymond" => Raymond,
        "santa" => Santa,
        _ => null
    };

    public static ArenaMoveSet Bip01 { get; } = new("bip01", "Tekniker", "Energi driver teknikken. Træffere lader kerneoverload.",
    [
        new("attack", "J", "Kredsløbsslag", "Hurtige slag og luftangreb."),
        new("pulse", "K", "Pulsbolt", "Hurtigt, lige energiprojektil."),
        new("grab", "U", "Magnetgreb", "Trækker fjender ind og absorberer deres projektiler."),
        new("shield", "L", "Bobleskjold", "Et præcist skjold kan sende projektiler tilbage."),
        new("dash", "A/D + K", "Raketryk", "Kort, hurtigt ryk med et beskyttet afsæt."),
        new("recovery", "W + K", "Rakethop", "Stejlt løft tilbage mod banen."),
        new("repair", "S + K", "Reparationsfelt", "Bruger energi til at reparere skade. Kan afbrydes."),
        new("ultimate", "Hold I", "Kerneoverload", "Fuld måler: eksplosion omkring BIP. Tømmer energien.")
    ]);

    public static ArenaMoveSet Raymond { get; } = new("raymond", "Kontrol", "Skjold bruges til gendannelse. Træffere og absorberede projektiler lader JOIN.",
    [
        new("attack", "J", "Snabelslag", "Langsommere, tungere slag med længere rækkevidde."),
        new("pulse", "K", "Snabelpuls", "Stort, langsomt projektil med kraftigt tilbageskub."),
        new("grab", "U", "Relationsgreb", "Fastholder én fjende. Vælg retning og kast med U eller J."),
        new("shield", "L", "Tryghedsskjold", "Absorberer projektiler og lader JOIN."),
        new("dash", "A/D + K", "Elefantstorm", "Vedvarende storm, der kan modstå én træffer."),
        new("recovery", "W + K", "Snabelløft", "Tungt løft med et bredt redningsangreb."),
        new("repair", "S + K", "Gendannelse", "Omsætter skjold til heling efter en sårbar opladning."),
        new("ultimate", "Hold I", "Den store JOIN", "Fuld måler: komet trækker fjender sammen og eksploderer.")
    ]);
    public static ArenaMoveSet Santa { get; } = new("santa", "Julemagi", "Julemagi driver evnerne. Julefred omsætter projektiler til magi. Træffere lader gavefesten.",
    [
        new("attack", "J", "Juleslag", "Et roligt slag med mellemstor rækkevidde."),
        new("pulse", "K", "Stjerneskud", "Et gyldent stjerneskud følger en let bue. Koster 14 magi."),
        new("grab", "U", "Gavegreb", "Løfter én nærliggende fjende gennem skjoldet. Koster 20 magi."),
        new("shield", "L", "Julefred", "Blokerer projektiler og gendanner 10 magi pr. blokering."),
        new("dash", "A/D + K", "Kanesus", "Et svævende ryk fremad. Koster 22 magi."),
        new("recovery", "W + K", "Skorstensspring", "Et højt løft med en kort beskyttende slagbevægelse. Koster 16 magi."),
        new("repair", "S + K", "Julevarme", "45 magi: heler 12 skade og gendanner 12 skjold efter en sårbar pause."),
        new("ultimate", "Hold I", "Den store gavefest", "Fuld måler: tre magiske snebolde i en vifte. Kan afbrydes under opladningen.")
    ]);
}
