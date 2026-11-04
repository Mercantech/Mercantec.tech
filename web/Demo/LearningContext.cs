namespace Mercantec.Tech.Web.Demo;

// UI context for the design catalog. API membership still controls access to real courses.
public sealed class LearningContext
{
    public string CourseId { get; private set; } = "h3";
    public event Action? Changed;
    public void Select(string? id)
    {
        var next = id is "h3" or "game" ? id : "";
        if (next == CourseId) return;
        CourseId = next;
        Changed?.Invoke();
    }
}
