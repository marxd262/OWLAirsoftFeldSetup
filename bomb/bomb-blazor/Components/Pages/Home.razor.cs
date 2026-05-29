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
    bool _bombActivated;

    protected override void OnInitialized()
    {
        LoadingState.OnChange += UpdateBombState;
        Defuse.OnChange += OnDefuseStateChanged;
        UpdateBombState();
    }

    private void UpdateBombState()
    {
        if (_bombActivated)
            return;

        _bombState = LoadingState.Progress switch
        {
            0 => BombState.Standby,
            100 => BombState.Active,
            _ => BombState.Activating
        };

        if (LoadingState.Progress == 100)
        {
            _bombActivated = true;
            LoadingState.OnChange -= UpdateBombState;
        }

        InvokeAsync(StateHasChanged);
    }

    private void OnDefuseStateChanged()
    {
        if (Defuse.State == DefuseState.Defused && _bombState == BombState.Active)
        {
            _bombState = BombState.Defused;
            InvokeAsync(StateHasChanged);
        }
    }

    private void HandleDetonation()
    {
        _bombState = BombState.Detonated;
        InvokeAsync(StateHasChanged);
    }

    private void HandleDefused()
    {
        _bombState = BombState.Defused;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        LoadingState.OnChange -= UpdateBombState;
        Defuse.OnChange -= OnDefuseStateChanged;
    }
}
