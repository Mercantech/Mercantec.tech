namespace Mercantec.Tech.Contracts;

public sealed record PlatformUpdate(string Id, DateOnly Date, string Title, string Description,
    UpdateImage? After = null, UpdateImage? Before = null)
{
    public IReadOnlyList<string> Authors { get; init; } = [];
}

public sealed record UpdateImage(string Id, string Alt);
