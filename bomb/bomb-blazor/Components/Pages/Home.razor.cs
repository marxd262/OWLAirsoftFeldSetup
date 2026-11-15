using Bomb.Components.Shared;
using Bomb.Services;
using Microsoft.AspNetCore.Components;

namespace Bomb.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject]
    private LoadingStateService LoadingState { get; set; } = default!;

    [Inject]
    private DefuseService Defuse { get; set; } = default!;

    BombState _bombState = BombState.Standby;

    protected override void OnInitialized()
    {
        LoadingState.OnChange += UpdateBombState;
        Defuse.OnChange += UpdateBombState;
        UpdateBombState();
    }

    // Derived from the shared services, so a reload or an admin reset always shows the current round.
    private void UpdateBombState()
    {
        _bombState = Defuse.State switch
        {
            DefuseState.Defused => BombState.Defused,
            DefuseState.Detonated => BombState.Detonated,
            _ => LoadingState.Progress switch
            {
                0 => BombState.Standby,
                100 => BombState.Active,
                _ => BombState.Activating
            }
        };

        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        LoadingState.OnChange -= UpdateBombState;
        Defuse.OnChange -= UpdateBombState;
    }
}
