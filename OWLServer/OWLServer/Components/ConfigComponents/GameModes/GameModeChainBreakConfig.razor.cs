using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class GameModeChainBreakConfig : ComponentBase
{
    [Parameter] public GameModeChainBreak CurrentGame { get; set; } = null!;

    [Inject] public IGameStateService GameStateService { get; set; } = null!;
    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private List<ChainLayout> _layouts = new();
    private int? _activeLayoutId;

    protected override async Task OnInitializedAsync()
    {
        await LoadLayouts();
        if (CurrentGame.ActiveChainLayout != null)
            _activeLayoutId = CurrentGame.ActiveChainLayout.Id;
    }

    private async Task LoadLayouts()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _layouts = await db.ChainLayouts.Include(cl => cl.Links).ToListAsync();
    }

    private async Task ActivateSelected()
    {
        if (_activeLayoutId == null) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = await db.ChainLayouts.Include(cl => cl.Links)
            .FirstOrDefaultAsync(cl => cl.Id == _activeLayoutId);
        if (layout != null)
            CurrentGame.ActiveChainLayout = layout;
    }

    private void OpenEditor() => Navigation.NavigateTo("/Admin/ChainEditor");
}
