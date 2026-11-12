using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Arena;

public sealed record ArenaCreateRequest(string StageId = "rooftops", int Stocks = 3, int Minutes = 5, bool IsPublic = false, string? AvatarId = null, ArenaAppearance? Appearance = null);
public sealed record ArenaJoinRequest(string? AvatarId = null, ArenaAppearance? Appearance = null);
public sealed record ArenaPlayerDto(string Id, string Name, string AvatarId, bool Ready, bool Connected, bool IsBot, string Color,
    ArenaAppearance? Appearance = null, bool CanRemoveAfk = false);
public sealed record ArenaRoomDto(string Code, string HostId, string Phase, string StageId, int Stocks, int Minutes, bool IsPublic, IReadOnlyList<ArenaPlayerDto> Players);
public sealed record ArenaAdmission(ArenaRoomDto Room, string PlayerId, string Ticket, string WebSocketPath, DateTimeOffset ExpiresAt);
public sealed record ArenaIdentity(Guid Subject, string Name, string AvatarId, IReadOnlySet<string> OwnedAvatars, ArenaAppearance? Appearance = null);

public sealed class ArenaException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
