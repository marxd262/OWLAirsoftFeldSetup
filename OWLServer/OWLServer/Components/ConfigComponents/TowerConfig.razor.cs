using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services;
using Radzen.Blazor;

namespace OWLServer.Components.ConfigComponents;

public partial class TowerConfig : ComponentBase, IDisposable
{
    [Inject]
    public GameStateService GameStateService { get; set; } = null!;
    [Inject]
    public ExternalTriggerService ExternalTriggerService { get; set; } = null!;

    private RadzenDataGrid<Tower> rdGrid;
    
    async Task EditRow(Tower tower)
    {
        if (!rdGrid.IsValid) return;
        await rdGrid.EditRow(tower);
    }
    
    void CancelEdit(Tower order)
    {
        rdGrid.CancelEditRow(order);
    }
    
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