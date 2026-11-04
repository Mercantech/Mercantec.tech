using System.Text.Json;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Demo;

public sealed class DemoWorld
{
    public const int Level = DemoCatalog.Level;
    public static string Pressed(bool on) => on ? "true" : "false";
    public const string StorageKey = "lup-demo-world";

    private static readonly int[] Walls = [10, 11, 12, 18, 26, 27, 35];
    private static readonly int[] Runes = [5, 20, 37];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _js;
    private Task? _loading;
    private bool _hadSnapshot;
    private bool _legacyImported;

    public DemoWorld(IJSRuntime js) => _js = js;

    public event Action? Changed;

    public bool Ready { get; private set; }
    public string Skin { get; private set; } = "#c98d69";
    public string Outfit { get; private set; } = "#2452e8";
    public string Hair { get; private set; } = "#42302a";
    public string HairStyle { get; private set; } = "short";
    public string EquippedId { get; private set; } = "basic";
    public string ShopCategory { get; private set; } = "Tøj";
    public int Gold { get; private set; } = DemoCatalog.Gold;
    public int Player { get; private set; } = 40;
    public string? Notice { get; private set; }
    public string? NoticeArea { get; private set; }
    public int? SelectedCell { get; private set; }
    public int? MovingFrom { get; private set; }
    public string? SelectedItem { get; private set; }

    public HashSet<int> Collected { get; } = [];
    public HashSet<string> Done { get; } = [];
    public Dictionary<string, int> Votes { get; } = [];
    public Dictionary<int, string> Room { get; } = new() { [9] = "desk", [14] = "bed", [35] = "plant" };
    public HashSet<string> OwnedFurniture { get; } = ["desk", "bed", "chair", "plant", "board"];
    public HashSet<string> OwnedWear { get; } = ["basic"];

    public static IReadOnlyList<DemoGoods> Goods { get; } =
    [
        new("basic", "Tøj", "Basis-T-shirt", 1, 0, "basic", null, null, 0, "Dit startsæt. Vælg selv farven på basis-T-shirten."),
        new("striped", "Tøj", "Stribet T-shirt", 2, 45, "striped", "#376d8c", null, 0, "Rolige striber til værksted og prøvespil."),
        new("hoodie", "Tøj", "Grøn hoodie", 4, 90, "hoodie", "#46775e", null, 0, "Et nyt sæt til din fælles spilavatar."),
        new("blazer", "Tøj", "Blå blazer", 6, 120, "blazer", "#405a88", null, 0, "Klar til at vise dit næste projekt frem."),
        new("hawaii-shirt", "Tøj", "Hawaii-skjorte", 1, 120, "hawaii", "#2c8f83", "hawaii", 1, "Hawaii-badget åbner købet. Skjorten sælges ikke uden det."),
        new("friday-suit", "Tøj", "Fancy Friday-sæt", 1, 150, "friday", "#79547e", "friday", 1, "Et særligt sæt til en særlig fredag."),
        new("developer", "Tøj", "Developer-trøje", 8, 160, "developer", "#35455b", null, 0, "Næste level-belønning, med kode på brystet."),
        new("mentor", "Tøj", "Mentorjakke", 10, 210, "mentor", "#334e63", "loop", 3, "Kræver Core Loop på Stor."),
        new("space", "Tøj", "Stjernetrøje", 12, 260, "space", "#655386", null, 0, "Et udtryk længere fremme på level-stien."),
        new("desk", "Møbler", "Skrivebord", 1, 0, "desk", null, null, 0, "Dit første sted at bygge og skrive."),
        new("bed", "Møbler", "Seng", 1, 0, "bed", null, null, 0, "En del af startinventaret."),
        new("chair", "Møbler", "Stol", 1, 0, "chair", null, null, 0, "En stol til skrivebordet."),
        new("plant", "Møbler", "Plante", 1, 0, "plant", null, null, 0, "Lidt grønt i det første rum."),
        new("rug", "Møbler", "Vævet tæppe", 2, 50, "rug", null, null, 0, "Et varmt mønster til gulvet."),
        new("lamp", "Møbler", "Bordlampe", 3, 80, "lamp", null, null, 0, "Lys ved arbejdsbordet."),
        new("trophy", "Møbler", "Trofæ", 4, 140, "trophy", null, null, 0, "Et tegn på, at du er kommet videre. Det er ikke gave-møblet."),
        new("bookcase", "Møbler", "Bogreol", 5, 100, "bookcase", null, null, 0, "Plads til bøger og små projekter."),
        new("monstera", "Møbler", "Stor plante", 6, 85, "monstera", null, null, 0, "Mere grønt til rummet."),
        new("sofa", "Møbler", "Blå sofa", 8, 180, "sofa", null, null, 0, "Et sted at lande efter en arbejdsdag."),
        new("arcade", "Møbler", "Arkadekabinet", 10, 260, "arcade", null, "gdd", 2, "Game Designer på Mellem åbner købet."),
        new("workstation", "Møbler", "Dobbelt arbejdsstation", 12, 290, "workstation", null, "api", 2, "Kræver level og API-erfaring.")
    ];

    public static DemoGoods Gift { get; } = new("board", "Møbler", DemoCatalog.Gift.Name, DemoCatalog.Gift.Level, 0, "board", null, null, 0, DemoCatalog.Gift.Desc + " Den står ikke i butikken.");

    public DemoGoods Equipped => Goods.First(g => g.Id == EquippedId);
    public string OutfitColor => Equipped.Color ?? Outfit;
    public bool GameWon => Collected.Count == Runes.Length && Player == 7;
    public IEnumerable<DemoGoods> Furniture => Goods.Where(g => g.Kind == "Møbler").Append(Gift);
    public IEnumerable<DemoGoods> VisibleGoods => Goods.Where(g => ShopCategory == "Alle" || g.Kind == ShopCategory);

    public Task EnsureAsync() => _loading ??= LoadAsync();

    // Keep local appearance/room choices from the earlier design without replacing an existing world.
    public async Task ImportLegacyAsync(string? subject)
    {
        await EnsureAsync();
        if (_hadSnapshot || _legacyImported || string.IsNullOrWhiteSpace(subject)) return;
        _legacyImported = true;
        var imported = false;
        try
        {
            var appearanceJson = await _js.InvokeAsync<string?>("lupAppearance.get", subject);
            var appearance = appearanceJson is null ? null : JsonSerializer.Deserialize<string[]>(appearanceJson);
            if (appearance?.Length == 3)
            {
                if (IsHex(appearance[0])) Skin = appearance[0];
                if (IsHex(appearance[1])) Hair = appearance[1];
                if (appearance[2] is "short" or "long" or "cap") HairStyle = appearance[2];
                imported = true;
            }
            var roomJson = await _js.InvokeAsync<string?>("lupRoom.get", subject);
            var room = roomJson is null ? null : JsonSerializer.Deserialize<string?[]>(roomJson);
            if (room?.Length == 48
                && room.Where(id => id is not null).All(id => OwnedFurniture.Contains(id!))
                && room.Where(id => id is not null).Distinct().Count() == room.Count(id => id is not null))
            {
                Room.Clear();
                for (var cell = 0; cell < room.Length; cell++)
                    if (room[cell] is string id) Room[cell] = id;
                imported = true;
            }
        }
        catch (JsonException) { /* Ignore incomplete legacy JSON and keep the current world. */ }
        catch (JSException) { /* Storage can be disabled by the browser. */ }
        if (imported) Touch();
    }

    public bool Owned(DemoGoods item) => item.Kind == "Tøj" ? OwnedWear.Contains(item.Id) : OwnedFurniture.Contains(item.Id);

    public bool Unlocked(DemoGoods item) => Level >= item.Level && BadgeOk(item);

    public string LockText(DemoGoods item)
    {
        var parts = new List<string>();
        if (Level < item.Level)
            parts.Add($"Level {item.Level}");
        if (!BadgeOk(item))
            parts.Add(BadgeLabel(item));
        return parts.Count == 0
            ? item.Badge is null ? $"Åben fra level {item.Level}" : $"Åben via {BadgeLabel(item)}"
            : "Kræver " + string.Join(" og ", parts);
    }

    public void SetColor(string key, string value)
    {
        if (!IsHex(value))
            return;
        if (key == "skin") Skin = value;
        else if (key == "hair") Hair = value;
        else if (key == "outfit" && EquippedId == "basic") Outfit = value;
        else return;
        Touch();
    }

    public void SetHair(string style)
    {
        if (style is not ("short" or "long" or "cap"))
            return;
        HairStyle = style;
        Touch();
    }

    public void SetCategory(string category)
    {
        if (category is not ("Tøj" or "Møbler" or "Alle"))
            return;
        ShopCategory = category;
        Touch();
    }

    public void Equip(string id)
    {
        var item = Goods.FirstOrDefault(g => g.Id == id && g.Kind == "Tøj");
        if (item is null || !OwnedWear.Contains(id))
            return;
        EquippedId = id;
        Touch();
    }

    public bool Buy(string id)
    {
        var item = Goods.FirstOrDefault(g => g.Id == id);
        if (item is null || Owned(item) || !Unlocked(item) || Gold < item.Cost)
            return false;
        Gold -= item.Cost;
        if (item.Kind == "Tøj")
        {
            OwnedWear.Add(item.Id);
            EquippedId = item.Id;
        }
        else
        {
            OwnedFurniture.Add(item.Id);
        }

        NoticeArea = "shop";
        Notice = $"{item.Name} er købt og føjet til dit inventar.";
        Touch();
        return true;
    }

    public void ChooseFurniture(string id)
    {
        if (!OwnedFurniture.Contains(id) || Room.ContainsValue(id))
            return;
        SelectedItem = id;
        SelectedCell = null;
        MovingFrom = null;
        NoticeArea = "room";
        Notice = $"Valgt: {NameOf(id)}. Klik på et frit felt.";
        Touch();
    }

    public void ClickCell(int cell)
    {
        if (cell is < 0 or > 47)
            return;
        NoticeArea = "room";
        if (MovingFrom is int from)
        {
            if (Room.ContainsKey(cell) && cell != from)
                Notice = "Feltet er optaget. Vælg et frit felt.";
            else if (Room.TryGetValue(from, out var moving))
            {
                Room.Remove(from);
                Room[cell] = moving;
                SelectedCell = cell;
                MovingFrom = null;
                Notice = "Møblet er flyttet.";
            }
        }
        else if (Room.TryGetValue(cell, out var placed))
        {
            SelectedCell = cell;
            SelectedItem = null;
            Notice = $"{NameOf(placed)} står her. Flyt det, eller læg det tilbage i inventaret.";
        }
        else if (SelectedItem is not null)
        {
            if (Room.ContainsValue(SelectedItem))
                Notice = "Møblet er allerede placeret.";
            else
            {
                Room[cell] = SelectedItem;
                SelectedCell = cell;
                Notice = "Møblet er placeret.";
                SelectedItem = null;
            }
        }
        else
        {
            SelectedCell = cell;
            Notice = "Vælg et møbel fra inventaret først.";
        }

        Touch();
    }

    public void BeginMove()
    {
        if (SelectedCell is null || !Room.ContainsKey(SelectedCell.Value))
            return;
        MovingFrom = SelectedCell;
        SelectedItem = null;
        NoticeArea = "room";
        Notice = "Klik på et frit felt at flytte til.";
        Touch();
    }

    public void RemoveSelected()
    {
        if (SelectedCell is null)
            return;
        Room.Remove(SelectedCell.Value);
        SelectedCell = null;
        MovingFrom = null;
        NoticeArea = "room";
        Notice = "Møblet er tilbage i inventaret.";
        Touch();
    }

    public void MovePlayer(string direction)
    {
        if (GameWon) return;
        var delta = direction switch
        {
            "up" => -8,
            "down" => 8,
            "left" => -1,
            "right" => 1,
            _ => 0
        };
        if (delta == 0)
            return;
        var target = Player + delta;
        if (target is < 0 or >= 48 || Math.Abs(target % 8 - Player % 8) > 1 || Walls.Contains(target))
            return;
        Player = target;
        if (Runes.Contains(target))
            Collected.Add(target);
        Touch();
    }

    public void ResetGame()
    {
        Player = 40;
        Collected.Clear();
        Touch();
    }

    public bool IsDone(string id) => Done.Contains(id);

    public void ToggleDone(string id)
    {
        if (!Done.Add(id))
            Done.Remove(id);
        NoticeArea = "activity";
        Notice = Done.Contains(id)
            ? "Arbejdet er markeret udført. Det giver ikke XP."
            : "Markeringen er fjernet.";
        Touch();
    }

    public int? VoteOf(string key) => Votes.TryGetValue(key, out var index)
        && DemoCatalog.Polls.Any(p => p.Question == key && index >= 0 && index < p.Options.Length) ? index : null;

    public void Vote(string key, int index)
    {
        if (!DemoCatalog.Polls.Any(p => p.Question == key && index >= 0 && index < p.Options.Length)) return;
        Votes[key] = index;
        NoticeArea = "activity";
        Notice = "Dit svar er registreret.";
        Touch();
    }

    public bool IsWall(int cell) => Walls.Contains(cell);
    public bool ShowRune(int cell) => Runes.Contains(cell) && !Collected.Contains(cell);
    public string? RoomItem(int cell) => Room.TryGetValue(cell, out var id) ? id : null;
    public bool Placed(string id) => Room.ContainsValue(id);

    public static int BadgeRank(string? badgeId) =>
        DemoCatalog.Badges.FirstOrDefault(b => b.Id == badgeId)?.Rank ?? 0;

    private bool BadgeOk(DemoGoods item) => item.Badge is null || BadgeRank(item.Badge) >= item.Rank;

    private static string BadgeLabel(DemoGoods item)
    {
        var badge = DemoCatalog.Badges.FirstOrDefault(b => b.Id == item.Badge);
        if (badge is null)
            return item.Badge ?? "badge";
        return item.Rank > 1 ? $"{badge.Name} · niveau {item.Rank}" : badge.Name;
    }

    private static string NameOf(string id) =>
        Goods.FirstOrDefault(g => g.Id == id)?.Name ?? Gift.Name;

    private async Task LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("lupDemo.get");
            if (!string.IsNullOrWhiteSpace(json))
            {
                var snapshot = JsonSerializer.Deserialize<Snapshot>(json, Json);
                Apply(snapshot);
                _hadSnapshot = snapshot is not null;
            }
        }
        catch (Exception)
        {
            // Demoen virker uden gemt tilstand.
        }

        Ready = true;
        Changed?.Invoke();
    }

    private void Apply(Snapshot? snap)
    {
        if (snap is null)
            return;
        if (snap.Skin is string skin && IsHex(skin)) Skin = skin;
        if (snap.Outfit is string outfit && IsHex(outfit)) Outfit = outfit;
        if (snap.Hair is string hair && IsHex(hair)) Hair = hair;
        if (snap.HairStyle is "short" or "long" or "cap") HairStyle = snap.HairStyle;
        if (Goods.Any(g => g.Id == snap.EquippedId && g.Kind == "Tøj")) EquippedId = snap.EquippedId!;
        if (snap.ShopCategory is "Tøj" or "Møbler" or "Alle") ShopCategory = snap.ShopCategory;
        Gold = Math.Clamp(snap.Gold, 0, 100000);
        Player = Math.Clamp(snap.Player, 0, 47);
        Collected.Clear();
        foreach (var cell in snap.Collected ?? [])
            if (Runes.Contains(cell)) Collected.Add(cell);
        Done.Clear();
        foreach (var id in snap.Done ?? [])
            if (!string.IsNullOrWhiteSpace(id)) Done.Add(id);
        Votes.Clear();
        foreach (var pair in snap.Votes ?? [])
            if (pair.Value >= 0) Votes[pair.Key] = pair.Value;
        Room.Clear();
        foreach (var pair in snap.Room ?? [])
            if (int.TryParse(pair.Key, out var cell) && cell is >= 0 and < 48 && Furniture.Any(f => f.Id == pair.Value))
                Room[cell] = pair.Value;
        OwnedWear.Clear();
        OwnedWear.Add("basic");
        foreach (var id in snap.OwnedWear ?? [])
            if (Goods.Any(g => g.Id == id && g.Kind == "Tøj")) OwnedWear.Add(id);
        OwnedFurniture.Clear();
        OwnedFurniture.UnionWith(["desk", "bed", "chair", "plant", "board"]);
        foreach (var id in snap.OwnedFurniture ?? [])
            if (Goods.Any(g => g.Id == id && g.Kind == "Møbler")) OwnedFurniture.Add(id);
        if (!OwnedWear.Contains(EquippedId))
            EquippedId = "basic";
    }

    private void Touch()
    {
        Changed?.Invoke();
        _ = SaveAsync();
    }

    private async Task SaveAsync()
    {
        var snap = new Snapshot
        {
            Skin = Skin,
            Outfit = Outfit,
            Hair = Hair,
            HairStyle = HairStyle,
            EquippedId = EquippedId,
            ShopCategory = ShopCategory,
            Gold = Gold,
            Player = Player,
            Collected = Collected.ToArray(),
            Done = Done.ToArray(),
            Votes = Votes.ToDictionary(pair => pair.Key, pair => pair.Value),
            Room = Room.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            OwnedWear = OwnedWear.ToArray(),
            OwnedFurniture = OwnedFurniture.ToArray()
        };
        try
        {
            await _js.InvokeVoidAsync("lupDemo.set", JsonSerializer.Serialize(snap, Json));
        }
        catch (Exception)
        {
            // Gemning er kun en demo i browseren.
        }
    }

    private static bool IsHex(string? value) =>
        value is not null && value.Length == 7 && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private sealed class Snapshot
    {
        public string? Skin { get; set; }
        public string? Outfit { get; set; }
        public string? Hair { get; set; }
        public string? HairStyle { get; set; }
        public string? EquippedId { get; set; }
        public string? ShopCategory { get; set; }
        public int Gold { get; set; }
        public int Player { get; set; }
        public int[]? Collected { get; set; }
        public string[]? Done { get; set; }
        public Dictionary<string, int>? Votes { get; set; }
        public Dictionary<string, string>? Room { get; set; }
        public string[]? OwnedWear { get; set; }
        public string[]? OwnedFurniture { get; set; }
    }
}

public sealed record DemoGoods(
    string Id,
    string Kind,
    string Name,
    int Level,
    int Cost,
    string Style,
    string? Color,
    string? Badge,
    int Rank,
    string Desc);
