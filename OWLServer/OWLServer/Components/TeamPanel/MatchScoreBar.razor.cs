using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services;
using Radzen.Blazor;

namespace OWLServer.Components.TeamPanel;

public partial class MatchScoreBar : ComponentBase
{
    [Inject]
    private GameStateService _gameStateService { get; set; } = null!;

    [Inject]
    private ExternalTriggerService _triggerService { get; set; } = null!;

    const int MIN = 0;
    const int MAX = 100;
    
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _triggerService.StateHasChangedAction += StateHasChangedAction;
        }
    }

    private void StateHasChangedAction()
    {
        InvokeAsync(StateHasChanged);
    }

    private int GetTeamScoreForProgressBar(TeamColor color)
    {
        if (_gameStateService.CurrentGame != null)
        {
            var points = _gameStateService.CurrentGame.GetDisplayPoints(color);
            var max = Convert.ToDouble(_gameStateService.CurrentGame.MaxTickets);

            if (max != 0)
            {
                return Convert.ToInt32(points / max * 100);
            }
        }

        return 100;
    }
}