namespace Mercantec.Tech.Web.Demo;

public static class AvatarCatalog
{
    public const string StarterId = "starter";
    public const string Bip01Id = "bip01";

    public static IReadOnlyList<AvatarDefinition> All { get; } = Array.AsReadOnly<AvatarDefinition>(
    [
        new(StarterId, "Din første avatar", "Din startfigur. Klar til at følge dig rundt på LUP.", 1, 0,
            Array.AsReadOnly<AvatarCatchphrase>(
            [
                new("starter-greeting", "greeting", "Hej! Skal vi lære noget nyt?"),
                new("starter-start", "start", "Vi tager ét trin ad gangen."),
                new("starter-success", "success", "Sådan! Det var godt arbejde."),
                new("starter-retry", "retry", "Et nyt forsøg gør os klogere."),
                new("starter-unlock", "unlock", "En ny ven til samlingen!")
            ])),
        new(Bip01Id, "BIP-01", "En lille robot med stor nysgerrighed og et ansigt fuldt af følelser.", 2, 150,
            Array.AsReadOnly<AvatarCatchphrase>(
            [
                new("bip01-greeting", "greeting", "System klar. Lad os lære noget!"),
                new("bip01-start", "start", "Vi tager den én byte ad gangen."),
                new("bip01-success", "success", "Bip-bip! Den sad lige i kredsløbet!"),
                new("bip01-retry", "retry", "Fejl fundet. Ny idé på vej!"),
                new("bip01-unlock", "unlock", "Opgradering gennemført. Du rykker!")
            ]))
    ]);

    public static AvatarDefinition? Find(string? id) => All.FirstOrDefault(avatar => avatar.Id == id);
}

public sealed record AvatarDefinition(
    string Id,
    string Name,
    string Description,
    int Level,
    int Cost,
    IReadOnlyList<AvatarCatchphrase> Catchphrases);

public sealed record AvatarCatchphrase(string Id, string Context, string Text);
