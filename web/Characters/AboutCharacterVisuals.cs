namespace Mercantec.Tech.Web.Characters;

internal static class AboutCharacterVisuals
{
    public static string Image(CharacterProfile character) => character.Slug switch
    {
        "lupp" or "beaver" or "fox" => $"/images/about/{character.Slug}.webp",
        _ => character.Appearances[0].Image
    };

    public static string Alt(CharacterProfile character) => character.Appearances[0].Alt;
}
