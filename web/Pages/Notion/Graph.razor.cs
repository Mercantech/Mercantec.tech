using Mercantec.Tech.Web.Auth;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages.Notion;

// Existing collection and hold graph URLs remain valid entry points.
public partial class Graph
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Parameter] public Guid? CollectionId { get; set; }
    [Parameter] public Guid? HoldId { get; set; }
    [SupplyParameterFromQuery(Name = "hold")] public Guid? HoldQuery { get; set; }
    [SupplyParameterFromQuery(Name = "published")] public bool PublishedPreview { get; set; }
    private bool ReadOnly => PublishedPreview || ViewAs.IsActive;
    private string? GraphScopeKind => CollectionId is not null ? "collection" : HoldId is not null || HoldQuery is not null ? "hold" : null;
    private string? GraphScopeId => (CollectionId ?? HoldId ?? HoldQuery)?.ToString();
    protected override void OnInitialized() { Auth.AuthStateChanged += Changed; ViewAs.Changed += Changed; }
    private void Changed() => _ = InvokeAsync(StateHasChanged);
    public void Dispose() { Auth.AuthStateChanged -= Changed; ViewAs.Changed -= Changed; }
    public override Task SetParametersAsync(ParameterView parameters)
    {
        CollectionId = HoldId = HoldQuery = null; PublishedPreview = false;
        return base.SetParametersAsync(parameters);
    }
}
