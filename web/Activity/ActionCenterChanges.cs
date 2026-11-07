namespace Mercantec.Tech.Web.Activity;

/// <summary>Invalidates personal action status after a successful API mutation.</summary>
public sealed class ActionCenterChanges
{
    public event Action? Changed;
    public void Notify() => Changed?.Invoke();
}
