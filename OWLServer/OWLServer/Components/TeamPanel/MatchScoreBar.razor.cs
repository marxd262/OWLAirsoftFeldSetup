using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OWLServer.Models;
using OWLServer.Services;
using Radzen.Blazor;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

namespace OWLServer.Components.TeamPanel;

public partial class MatchScoreBar : ComponentBase
{
    [Inject]
    private GameStateService _gameStateService { get; set; } = null!;

    [Inject]
    private ExternalTriggerService _triggerService { get; set; } = null!;

    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = null!;

    const int MIN = 0;
    const int MAX = 100;

    private Dictionary<TeamColor, string> _currentScores = new() { { TeamColor.BLUE, "0%" },  { TeamColor.RED, "0%" } };
    
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _triggerService.StateHasChangedAction += StateHasChangedAction;
            StateHasChangedAction();
        }
    }

    private void StateHasChangedAction()
    {
        if (GetTeamScoreForProgressBar(TeamColor.BLUE))
        {
            _jsRuntime.InvokeVoidAsync("setProgressbar", TeamColor.BLUE, _currentScores[TeamColor.BLUE]);
        }

        if (GetTeamScoreForProgressBar(TeamColor.RED))
        {
            _jsRuntime.InvokeVoidAsync("setProgressbar", TeamColor.RED, _currentScores[TeamColor.RED]);
        }
        
        InvokeAsync(StateHasChanged);
    }

    private bool GetTeamScoreForProgressBar(TeamColor color)
    {
        var newscore = "100%";
        if (_gameStateService.CurrentGame != null)
        {
            var points = _gameStateService.CurrentGame.GetDisplayPoints(color);
            var max = Convert.ToDouble(_gameStateService.CurrentGame.MaxTickets);

            if (max != 0)
            {
                newscore = $"{Convert.ToInt32(points / max * 100)}%";
            }
        }
        
        bool sucess = _currentScores[color] != newscore;
        if (sucess)
        {
            _currentScores[color] = newscore;
        }
        
        return sucess;
    }
}