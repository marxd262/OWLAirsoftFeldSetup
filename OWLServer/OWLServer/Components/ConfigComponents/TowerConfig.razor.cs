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
    
    
    protected override void OnInitialized()
    {
        ExternalTriggerService.StateHasChangedAction += () => InvokeAsync(StateHasChanged);
        base.OnInitialized();
    }
    
    
}