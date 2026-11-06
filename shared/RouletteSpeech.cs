namespace Mercantec.Tech.Contracts;

public sealed record RouletteSpeechStatus(bool Available, string Provider, string? Message = null);
public sealed record CreateRouletteSpeech(string GroupName, Guid[] MembershipIds, bool IncludeMembers = false);
public sealed record RouletteSpeechAudio(string AudioBase64, string ContentType, string Provider);
