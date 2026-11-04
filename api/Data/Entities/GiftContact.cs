namespace Mercantec.Tech.Api.Data.Entities;

// Display information comes from the authenticated account, never from a send request.
public sealed class GiftContact
{
    public Guid UserSub { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsTeacher { get; set; }
}
