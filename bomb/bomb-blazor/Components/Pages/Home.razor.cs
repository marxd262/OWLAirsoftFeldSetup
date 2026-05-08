using Bomb.Components.Shared;
using Bomb.Services;
using Microsoft.AspNetCore.Components;

namespace Bomb.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject]
    private LoadingStateService LoadingState { get; set; } = default!;

    BombState _bombState = BombState.Standby;

    protected override void OnInitialized()
    {
        LoadingState.OnChange += UpdateBombState;
        UpdateBombState();
    }

    private void UpdateBombState()
    {
        _bombState = LoadingState.Progress switch
        {
            0 => BombState.Standby,
            100 => BombState.Active,
            _ => BombState.Activating
        };

        if(LoadingState.Progress == 100)
        {
            LoadingState.OnChange -= UpdateBombState;
        }

        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        LoadingState.OnChange -= UpdateBombState;
    }
}
