using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;
using Radzen;

namespace OWLServer.Components.Pages;

public partial class AdminPanel : ComponentBase
{
    [Inject]
    GameStateService GameStateService { get; set; } = null!;
    [Inject]
    ExternalTriggerService ExternalTriggerService { get; set; } = null!;
    
    private void GameModeChanged(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.None:
                GameStateService.CurrentGame = null;
                break;
            case GameMode.TeamDeathMatch:
                GameStateService.CurrentGame = new GameModeTeamDeathmatch(ExternalTriggerService);
                GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
                break;
            case GameMode.Conquest:
                GameStateService.CurrentGame = new GameModeConquest(ExternalTriggerService,GameStateService);
                GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    private ButtonStyle GetButtonStyle
    {
        get
        {
            if (GameStateService.CurrentGame == null)
            {
                return ButtonStyle.Info;
            }
            
            if(GameStateService.CurrentGame.StartTime == null) 
                return ButtonStyle.Success;
            
            return ButtonStyle.Danger;
        }
    }
    private string GetButtonText
    {
        get
        {if (GameStateService.CurrentGame == null)
            {
                return "Select Game";
            }
            
            if(GameStateService.CurrentGame.StartTime == null) 
                return "Start Game";
            
            return "Stop Game";
        }
    }

    private void ButtonClick()
    {
        if (GetButtonStyle == ButtonStyle.Success)
        {
            GameStateService.CurrentGame?.RunGame();
        }
    }
}