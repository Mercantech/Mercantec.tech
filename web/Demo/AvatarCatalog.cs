namespace Mercantec.Tech.Web.Demo;

public static class AvatarCatalog
{
    public const string StarterId = "starter";
    public const string Bip01Id = "bip01";
    public const string RaymondId = "raymond";
    public const string SantaId = "santa";

    private static readonly AvatarAction[] CommonActions =
    [
        new("idle", "Stå klar", "Sociale udtryk", "Et roligt øjeblik, klar til næste opgave.", 8, true),
        new("blink", "Blink", "Sociale udtryk", "Et lille blink og tilbage til det venlige blik."),
        new("wave", "Vink", "Sociale udtryk", "En venlig hilsen med hånden."),
        new("think", "Tænk", "Sociale udtryk", "Tid til at overveje den næste idé.", 8, true),
        new("talk", "Tal", "Sociale udtryk", "Figuren forklarer med ansigt og hænder.", 10, true),
        new("point", "Peg", "Sociale udtryk", "Se her — en tydelig markering med hånden."),
        new("celebrate", "Jubel", "Sociale udtryk", "En glad reaktion på et godt stykke arbejde."),
        new("encourage", "Opmuntre", "Sociale udtryk", "En varm opmuntring til næste forsøg."),
        new("surprised", "Overrasket", "Sociale udtryk", "Et overrasket blik, før figuren samler sig igen."),
        new("unlock", "Låst op", "Sociale udtryk", "Figuren hilser på sin nye samling."),
        new("walk", "Gå", "Bevægelse", "En sammenhængende gangcyklus.", 10, true),
        new("run", "Løb", "Bevægelse", "En hurtigere cyklus med længere skridt.", 12, true),
        new("jump", "Hop", "Bevægelse", "Afsæt, svæv og forberedelse til landing."),
        new("land", "Land", "Bevægelse", "Figuren tager imod landingen og finder balancen."),
        new("sit", "Sæt dig", "Bevægelse", "Figuren sætter sig og bliver siddende.", EndState: "hold-last"),
        new("sleep", "Sov", "Bevægelse", "Rolig vejrtrækning under en lille pause.", 6, true),
        new("pickup", "Saml op", "Handlinger", "Ræk ned, grib og ret dig op igen."),
        new("interact", "Interagér", "Handlinger", "En generisk bevægelse til at undersøge eller bruge noget."),
        new("hit", "Ramt", "Handlinger", "Figuren reagerer og genvinder balancen."),
        new("knockout", "Slået ud", "Handlinger", "Figuren er midlertidigt ude af spil og bliver liggende.", EndState: "hold-last"),
        new("respawn", "Tilbage igen", "Handlinger", "Figuren kommer tilbage og stiller sig klar."),
        new("ability_1_pulse", "Pulsbolt", "Evner", "En kort, fokuseret affyringsbevægelse.", 8),
        new("ability_2_magnet", "Magnetgreb", "Evner", "Figuren samler kræfterne foran sig.", 8),
        new("ability_3_shield", "Bobleskjold", "Evner", "En rolig, beskyttende stilling.", 8),
        new("ability_4_dash", "Raketryk", "Evner", "Et hurtigt fremadrettet afsæt.", 8),
        new("ability_5_repair", "Reparationsfelt", "Evner", "Figuren samler kræfterne og kommer sig igen.", 8),
        new("ultimate_charge", "Kerneoverload · opladning", "Evner", "Figurens store evne lades op.", 5, EndState: "ultimate_release"),
        new("ultimate_release", "Kerneoverload · udløsning", "Evner", "Den samlede kraft slippes fri.", 5)
    ];

    private static readonly IReadOnlyList<AvatarAction> RobotActions = Array.AsReadOnly(CommonActions);
    private static readonly IReadOnlyList<AvatarAction> RaymondActions = Array.AsReadOnly(CommonActions.Select(action => action.Id switch
    {
        "ability_1_pulse" => action with { Label = "Snabelpuls", Description = "Raymond læner sig frem og sender et kort, fokuseret pust." },
        "ability_2_magnet" => action with { Label = "Relationsgreb", Description = "Han samler hænderne og fører forbindelserne mod et fælles punkt." },
        "ability_3_shield" => action with { Label = "Tryghedsskjold", Description = "Raymond stiller sig roligt og beskytter området foran sig." },
        "ability_4_dash" => action with { Label = "Elefantstorm", Description = "Et tungt, hurtigt afsæt med snablen og kroppen fremad." },
        "ability_5_repair" => action with { Label = "Gendannelse", Description = "Han vender håndfladerne op og samler kræfterne igen." },
        "ultimate_charge" => action with { Label = "Den store JOIN · opladning", Description = "Raymond samler al sin koncentration i én stor forbindelse." },
        "ultimate_release" => action with { Label = "Den store JOIN · udløsning", Description = "Den store forbindelse åbner sig, og den samlede kraft slippes fri." },
        _ => action
    }).ToArray());
    private static readonly IReadOnlyList<AvatarAction> SantaActions = Array.AsReadOnly(CommonActions.Select(action => action.Id switch
    {
        "ability_1_pulse" => action with { Label = "Stjerneskud", Description = "Julemanden sender et stjerneskud afsted fra den åbne hånd." },
        "ability_2_magnet" => action with { Label = "Gavegreb", Description = "Et præcist greb løfter modstanderen med et lille strejf af julemagi." },
        "ability_3_shield" => action with { Label = "Julefred", Description = "Han åbner armene og beskytter sig med rolig julemagi." },
        "ability_4_dash" => action with { Label = "Kanesus", Description = "Julemanden glider fremad med et hurtigt, svævende afsæt." },
        "ability_5_repair" => action with { Label = "Julevarme", Description = "En varm, rolig gestus samler kræfterne igen." },
        "ultimate_charge" => action with { Label = "Den store gavefest · opladning", Description = "Han samler al julemagien mellem sine hænder.", Fps = 5 },
        "ultimate_release" => action with { Label = "Den store gavefest · udløsning", Description = "Julemagien slippes fri i en fest af magiske snebolde.", Fps = 5 },
        _ => action
    }).ToArray());
    private static readonly IReadOnlyList<string> AnimationIds = Array.AsReadOnly(CommonActions.Select(action => action.Id).ToArray());
    private static readonly IReadOnlyList<string> PhraseFiles = Array.AsReadOnly<string>(["phrase_1_welcome.wav", "phrase_2_start.wav", "phrase_3_success.wav", "phrase_4_retry.wav", "phrase_5_milestone.wav"]);

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
            Animations = AnimationIds,
            Actions = RobotActions,
            Emojis = Array.AsReadOnly<AvatarEmoji>([
                new("happy", "Glad", "/assets/avatars/bip-01/emoji-happy.png"),
                new("wink", "Blink", "/assets/avatars/bip-01/emoji-wink.png"),
                new("think", "Tænksom", "/assets/avatars/bip-01/emoji-think.png"),
                new("celebrate", "Jubel", "/assets/avatars/bip-01/emoji-celebrate.png")]),
            AudioFiles = PhraseFiles
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
            SupportsWorldSprites = true,
            FaceImage = "/assets/avatars/raymond/face.png",
            Animations = AnimationIds,
            Actions = RaymondActions,
            AudioFiles = PhraseFiles,
            CharacterProfilePath = "/characters/raymond",
            Emojis = Array.AsReadOnly<AvatarEmoji>([
                new("happy", "Glad", "/assets/avatars/raymond/emoji-happy.png"),
                new("laugh", "Griner", "/assets/avatars/raymond/emoji-laugh.png"),
                new("think", "Tænksom", "/assets/avatars/raymond/emoji-think.png"),
                new("celebrate", "Jubel", "/assets/avatars/raymond/emoji-celebrate.png")])
        },
        new(SantaId, "Julemanden", "En varm og eventyrlysten julemand med julemagi i hænderne, glimt i øjet og plads til alle på den gode liste.", 4, 150,
            Array.AsReadOnly<AvatarCatchphrase>(
            [
                new("santa-greeting", "greeting", "Ho ho ho! Er du klar til lidt julemagi?"),
                new("santa-start", "start", "Selv den største opgave løses ét skridt ad gangen."),
                new("santa-success", "success", "Godt klaret! Det fortjener en plads på den gode liste."),
                new("santa-retry", "retry", "En lille pause, og så prøver vi igen."),
                new("santa-unlock", "unlock", "Sammen får vi magien til at virke!")
            ]))
        {
            Edition = "Julemagi",
            SpriteAssetRoot = "/assets/avatars/santa",
            SupportsWorldSprites = true,
            FaceImage = "/assets/avatars/santa/face.png",
            Animations = AnimationIds,
            Actions = SantaActions,
            AudioFiles = PhraseFiles,
            Emojis = Array.AsReadOnly<AvatarEmoji>([
                new("happy", "Glad", "/assets/avatars/santa/emoji-happy.png"),
                new("laugh", "Griner", "/assets/avatars/santa/emoji-laugh.png"),
                new("think", "Tænksom", "/assets/avatars/santa/emoji-think.png"),
                new("celebrate", "Jubel", "/assets/avatars/santa/emoji-celebrate.png")])
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
    public bool SupportsWorldSprites { get; init; }
    public int SpriteRows { get; init; } = 2;
    public string? FaceImage { get; init; }
    public IReadOnlyList<string> Animations { get; init; } = [];
    public IReadOnlyList<AvatarAction> Actions { get; init; } = [];
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

public sealed record AvatarAction(string Id, string Label, string Group, string Description,
    int Fps = 10, bool Loop = false, string EndState = "idle")
{
    public int Frames => 8;
    public double DurationSeconds => (double)Frames / Fps;
    public string SoundFile => Id + ".wav";
}
