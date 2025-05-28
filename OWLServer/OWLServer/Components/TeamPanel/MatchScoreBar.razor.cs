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


    bool isChecking = false;

    private Dictionary<TeamColor, string> _currentScores = new() { { TeamColor.BLUE, "50%" },  { TeamColor.RED, "50%" } };
    
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
        InvokeAsync(StateHasChanged);
        if (!isChecking)
        {
            isChecking = true;

            InvokeAsync(GetTeamScoreForProgressBar);

            isChecking = false;
        }
    }

    private void GetTeamScoreForProgressBar()
    {
        foreach (TeamColor color in _currentScores.Keys)
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
                _jsRuntime.InvokeVoidAsync("setProgressbar", color, _currentScores[color]);
            }
        }
        Thread.Sleep(123);
    }
}