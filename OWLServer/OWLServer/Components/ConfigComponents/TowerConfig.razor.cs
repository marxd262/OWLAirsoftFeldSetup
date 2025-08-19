using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services;
using Radzen.Blazor;

namespace OWLServer.Components.ConfigComponents;

public partial class TowerConfig : ComponentBase
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
    
    protected override void OnInitialized()
    {
        ExternalTriggerService.StateHasChangedAction += () => InvokeAsync(StateHasChanged);
        base.OnInitialized();
    }
    
    
}