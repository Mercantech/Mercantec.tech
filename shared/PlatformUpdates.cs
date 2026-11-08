namespace Mercantec.Tech.Contracts;

public sealed record PlatformUpdate(string Id, DateOnly Date, string Title, string Description,
    UpdateImage After, UpdateImage? Before = null);

public sealed record UpdateImage(string Id, string Alt);
