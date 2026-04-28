using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.ConfigComponents;

public partial class TowerConfig : ComponentBase, IDisposable
{
    [Inject]
    public IGameStateService GameStateService { get; set; } = null!;
    [Inject]
    public IExternalTriggerService ExternalTriggerService { get; set; } = null!;

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

    // Control relationships are now managed via TowerControlLayout in GameModeConquest.
    // UI for editing them at runtime is removed — configure layouts via the database/seeding.
}
