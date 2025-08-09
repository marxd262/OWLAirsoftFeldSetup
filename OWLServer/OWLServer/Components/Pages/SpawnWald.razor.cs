using Microsoft.AspNetCore.Components;

namespace OWLServer.Components.Pages;

public partial class SpawnWald : ComponentBase
{
    protected override void OnInitialized()
    {
        ExternalTriggerService.StateHasChangedAction += () => InvokeAsync(StateHasChanged);
        base.OnInitialized();
    }
    
    private void ToggleButtonClick()
    {
        ExternalTriggerService.StateHasChangedAction();
    }
}