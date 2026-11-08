namespace Mercantec.Tech.Web.Demo;

public static class AvatarCatalog
{
    public const string StarterId = "starter";
    public const string Bip01Id = "bip01";
    public const string RaymondId = "raymond";

    private static readonly string[] RobotAnimations = ["idle", "blink", "wave", "think", "talk", "point", "celebrate", "encourage", "surprised", "unlock", "walk", "run", "jump", "land", "sit", "sleep", "pickup", "interact", "hit", "knockout", "respawn", "ability_1_pulse", "ability_2_magnet", "ability_3_shield", "ability_4_dash", "ability_5_repair", "ultimate_charge", "ultimate_release"];
    private static readonly string[] RaymondAnimations = ["idle", "walk", "run", "jump", "wave", "celebrate", "think", "talk"];

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
        {
            Edition = "Den første robot",
            SpriteAssetRoot = "/assets/avatars/bip-01",
            FaceImage = "/assets/avatars/bip-01/face.png",
            Animations = Array.AsReadOnly(RobotAnimations),
            Emojis = Array.AsReadOnly<AvatarEmoji>([
                new("happy", "Glad", "/assets/avatars/bip-01/emoji-happy.png"),
                new("wink", "Blink", "/assets/avatars/bip-01/emoji-wink.png"),
                new("think", "Tænksom", "/assets/avatars/bip-01/emoji-think.png"),
                new("celebrate", "Jubel", "/assets/avatars/bip-01/emoji-celebrate.png")]),
            AudioFiles = Array.AsReadOnly<string>(["phrase_1_welcome.wav", "phrase_2_start.wav", "phrase_3_success.wav", "phrase_4_retry.wav", "phrase_5_milestone.wav"])
        },
        new(RaymondId, "Raymond", "Professor Codd tager med på eventyr. Den venlige elefantprofessor hjælper dig videre med ro, nysgerrighed og et glimt i øjet.", 3, 150,
            Array.AsReadOnly<AvatarCatchphrase>(
            [
                new("raymond-greeting", "greeting", "Jeg er Professor Codd, men du må gerne kalde mig Raymond."),
                new("raymond-start", "start", "Lad os finde forbindelsen — én række ad gangen."),
                new("raymond-success", "success", "Sådan! Nu hænger tabellerne sammen."),
                new("raymond-retry", "retry", "En lille fejl er bare et godt sted at blive klogere."),
                new("raymond-unlock", "unlock", "Tak for pladsen i din samling. Hvad skal vi undersøge først?")
            ]))
        {
            Edition = "Professor Codd",
            SpriteAssetRoot = "/assets/avatars/raymond",
            SpriteRows = 1,
            FaceImage = "/images/characters/raymond/neutral.webp",
            Animations = Array.AsReadOnly(RaymondAnimations),
            CharacterProfilePath = "/characters/raymond",
            Emojis = Array.AsReadOnly<AvatarEmoji>([
                new("happy", "Glad", "/images/characters/raymond/reactions/success.png"),
                new("laugh", "Griner", "/images/characters/raymond/reactions/laughing.png?v=097b5eeb"),
                new("think", "Tænksom", "/images/characters/raymond/reactions/thinking.png"),
                new("celebrate", "Jubel", "/images/characters/raymond/reactions/celebration.png")])
        }
    ]);

    public static AvatarDefinition? Find(string? id) => All.FirstOrDefault(avatar => avatar.Id == id);
}

public sealed record AvatarDefinition(
    string Id,
    string Name,
    string Description,
    int Level,
    int Cost,
    IReadOnlyList<AvatarCatchphrase> Catchphrases)
{
    public string Edition { get; init; } = "Din startfigur";
    public string? SpriteAssetRoot { get; init; }
    public int SpriteRows { get; init; } = 2;
    public string? FaceImage { get; init; }
    public IReadOnlyList<string> Animations { get; init; } = [];
    public IReadOnlyList<AvatarEmoji> Emojis { get; init; } = [];
    public IReadOnlyList<string> AudioFiles { get; init; } = [];
    public string? CharacterProfilePath { get; init; }
    public string FaceExtension => Path.GetExtension(FaceImage ?? "").TrimStart('.');
}

public sealed record AvatarEmoji(string Id, string Label, string Image)
{
    public string Extension => Path.GetExtension(Image.Split('?')[0]).TrimStart('.');
}

public sealed record AvatarCatchphrase(string Id, string Context, string Text);
