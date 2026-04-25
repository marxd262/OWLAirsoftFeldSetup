using Microsoft.AspNetCore.Components;

namespace OWLServer.Components.Pages.AdminPages;

public partial class AdminStartPage : ComponentBase, IDisposable
{
    private Action _stateChangedHandler = null!;

    protected override void OnInitialized()
    {
        _stateChangedHandler = () => InvokeAsync(StateHasChanged);
        ExternalTriggerService.StateHasChangedAction += _stateChangedHandler;
        base.OnInitialized();
    }

    public void Dispose()
    {
        ExternalTriggerService.StateHasChangedAction -= _stateChangedHandler;
    }
}