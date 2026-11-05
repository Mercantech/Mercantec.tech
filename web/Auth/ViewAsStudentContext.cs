namespace Mercantec.Tech.Web.Auth;

/// <summary>
/// Lokal underviser-preview af elev-UI. Skifter ikke Auth-login / JWT —
/// kun session-kontekst i Blazor WASM.
/// </summary>
public sealed class ViewAsStudentContext
{
    public bool IsActive => !string.IsNullOrWhiteSpace(LectioId);
    public string? LectioId { get; private set; }
    public string? DisplayName { get; private set; }
    public string? ClassName { get; private set; }
    public Guid? AuthSub { get; private set; }
    public string? ReturnUrl { get; private set; }

    public event Action? Changed;

    public void Enter(string lectioId, string? displayName, string? className = null, Guid? authSub = null, string? returnUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lectioId);
        LectioId = lectioId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        ClassName = string.IsNullOrWhiteSpace(className) ? null : className.Trim();
        AuthSub = authSub is Guid g && g != Guid.Empty ? g : null;
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/lectio" : returnUrl.Trim();
        Changed?.Invoke();
    }

    public void Exit()
    {
        if (!IsActive)
            return;

        LectioId = null;
        DisplayName = null;
        ClassName = null;
        AuthSub = null;
        ReturnUrl = null;
        Changed?.Invoke();
    }

    public string BannerLabel
    {
        get
        {
            if (!IsActive)
                return "";
            var name = DisplayName ?? "elev";
            return ClassName is null ? name : $"{name} ({ClassName})";
        }
    }
}
