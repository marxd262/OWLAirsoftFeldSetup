using Microsoft.AspNetCore.Components;

namespace OWLServer.Components.Pages.AdminPages;

public partial class AdminStartPage : ComponentBase
{
    
    
    protected override void OnInitialized()
    {
        ExternalTriggerService.StateHasChangedAction += () => InvokeAsync(StateHasChanged);
        base.OnInitialized();
    }
}