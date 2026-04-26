using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services;

namespace OWLServer.Components.ConfigComponents;

public partial class TowerConfig : ComponentBase, IDisposable
{
    [Inject]
    public GameStateService GameStateService { get; set; } = null!;
    [Inject]
    public ExternalTriggerService ExternalTriggerService { get; set; } = null!;

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

    private void ControllingTowerChanged(Tower tower, string newControllingTowerID)
    {
        if (tower.IsControlled)
        {
            var previousController = tower.IsControlledByID;
            tower.IsControlled = false;
            tower.IsControlledByID = null;
            if (previousController != null && GameStateService.TowerManagerService.Towers.ContainsKey(previousController))
                GameStateService.TowerManagerService.Towers[previousController].ControllsTowerID.Remove(tower.MacAddress);
        }

        if (!string.IsNullOrEmpty(newControllingTowerID))
        {
            tower.IsControlled = true;
            tower.IsControlledByID = newControllingTowerID;
            if (GameStateService.TowerManagerService.Towers.ContainsKey(newControllingTowerID))
                GameStateService.TowerManagerService.Towers[newControllingTowerID].ControllsTowerID.Add(tower.MacAddress);
        }

        tower.SetToStartColor();
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }
}
