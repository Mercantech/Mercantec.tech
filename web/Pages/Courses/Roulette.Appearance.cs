using Mercantec.Tech.Web.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Courses;

public partial class Roulette
{
    private string _appearanceTheme = "system";

    private async Task InitializeAppearance()
    {
        try { _appearanceTheme = ThemeCatalog.Find(await Js.InvokeAsync<string>("lupTheme.get")).Id; }
        catch (JSException) { }
    }

    private async Task AppearanceThemeChanged(ChangeEventArgs change)
    {
        _appearanceTheme = ThemeCatalog.Find(change.Value?.ToString()).Id;
        try { await Js.InvokeVoidAsync("lupTheme.set", _appearanceTheme); }
        catch (JSException) { _notice = "Temaet kunne ikke skiftes lige nu. Prøv igen."; }
    }
}
