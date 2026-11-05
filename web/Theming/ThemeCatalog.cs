namespace Mercantec.Tech.Web.Theming;

/// <summary>The freely available platform themes, shared by both appearance menus.</summary>
public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeDefinition> All { get; } = Array.AsReadOnly<ThemeDefinition>(
    [
        new("system", "System", "screen", "Følger enhedens lyse eller mørke udseende."),
        new("light", "Lyst", "star", "Platformens lyse grundtema."),
        new("dark", "Mørkt", "star", "Platformens mørke grundtema."),
        new("christmas", "Jul", "snow", "Julelys, gaver og interaktiv sne."),
        new("newyear", "Nytår", "firework", "Den lilla figur, fyrværkeri og faldende mål med stigende fart."),
        new("hacker", "Hacker", "terminal", "Grønne terminalfarver og monospace-overskrifter.")
    ]);

    public static ThemeDefinition Find(string? id) =>
        All.FirstOrDefault(theme => theme.Id == id) ?? All[0];
}

public sealed record ThemeDefinition(string Id, string Name, string Icon, string Description);
