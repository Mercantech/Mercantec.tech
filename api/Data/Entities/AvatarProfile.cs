using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Data.Entities;

public sealed class AvatarProfile
{
    public Guid UserSub { get; set; }
    public string SelectedAvatarId { get; set; } = AvatarEconomy.StarterId;
    public int Gold { get; set; } = AvatarEconomy.InitialGold;
    public int Level { get; set; } = AvatarEconomy.InitialLevel;
    public bool LegacyImportAvailable { get; set; } = true;
    public long Revision { get; set; }
    public ICollection<AvatarOwnership> Avatars { get; set; } = new List<AvatarOwnership>();
    public ICollection<AvatarItemOwnership> Items { get; set; } = new List<AvatarItemOwnership>();
}

public sealed class AvatarOwnership
{
    public Guid UserSub { get; set; }
    public string AvatarId { get; set; } = "";
    public AvatarProfile Profile { get; set; } = null!;
}

public sealed class AvatarItemOwnership
{
    public Guid UserSub { get; set; }
    public string ItemId { get; set; } = "";
    public AvatarProfile Profile { get; set; } = null!;
}
