using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;
using Radzen;

namespace OWLServer.Components.Pages.AdminPages;

public partial class AdminPanel : ComponentBase, IDisposable
{
    [Inject]
    GameStateService GameStateService { get; set; } = null!;
    [Inject]
    ExternalTriggerService ExternalTriggerService { get; set; } = null!;

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
    private void GameModeChanged(GameMode? mode)
    {
        if (mode == null) return;

        var old = GameStateService.CurrentGame;
        if (old?.IsRunning == true) old.EndGame();
        (old as IDisposable)?.Dispose();

        switch (mode)
        {
            case GameMode.None:
                GameStateService.CurrentGame = null;
                break;
            case GameMode.TeamDeathMatch:
                GameStateService.CurrentGame = new GameModeTeamDeathmatch(ExternalTriggerService, GameStateService);
                GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
                break;
            case GameMode.Conquest:
                GameStateService.CurrentGame = new GameModeConquest(ExternalTriggerService,GameStateService);
                GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
                break;
            case GameMode.Timer:
                GameStateService.CurrentGame = new GameModeTimer(ExternalTriggerService,GameStateService);
                break;
            case GameMode.ChainBreak:
                GameStateService.CurrentGame = new GameModeChainBreak(ExternalTriggerService, GameStateService);
                GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
                break;
            default:
                //throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                break;
        }
    }

    private ButtonStyle GetButtonStyle
    {
        get
        {
            if (GameStateService.CurrentGame == null)
                return ButtonStyle.Info;
            
            if(!GameStateService.CurrentGame.IsRunning) 
                return ButtonStyle.Success;
            
            return ButtonStyle.Danger;
        }
    }
    private string GetButtonText
    {
        get
        {
            if (GameStateService.CurrentGame == null)
            {
                return "Select Game";
            }
            
            if(!GameStateService.CurrentGame.IsRunning) 
                return "Start Game";
            
            return "Stop Game";
        }
    }

    public void ToggleAutoStart(bool newValue)
    {
        if (newValue)
        {
            GameStateService.AutoStartCancellationTokenSrc.TryReset();
            Task.Run(GameStateService.AutoStartGame, GameStateService.AutoStartCancellationTokenSrc.Token);
        }
        else
        {
            GameStateService.AutoStartCancellationTokenSrc.Cancel();
            GameStateService.StadtSpawnReady = false;
            GameStateService.WaldSpawnReady = false;
        }
    }

    private void ToggleButtonClick()
    {
        ExternalTriggerService.StateHasChangedAction();
    }

    public void ResetClick()
    {
        //var gm = GameStateService.CurrentGame.GameMode;
        //
        //if(GameStateService.CurrentGame.IsRunning)
        //    GameStateService.StopGame();
        //GameModeChanged(GameMode.None);
        //ameModeChanged(gm);
        
        GameStateService.Reset();

        Task.Run(() => {
            try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
            catch { }
        });
    }
    private void ButtonClick()
    {
        if (GetButtonStyle == ButtonStyle.Success)
        {
            GameStateService.StartGame();
        }
        else
        {
            GameStateService.StopGame();
        }
    }
}