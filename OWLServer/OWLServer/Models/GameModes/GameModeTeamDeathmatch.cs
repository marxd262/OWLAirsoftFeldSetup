using System.Net.Http.Headers;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components;
using OWLServer.Services;

namespace OWLServer.Models.GameModes;

public class GameModeTeamDeathmatch : IGameModeBase, IDisposable
{
    private ExternalTriggerService ExternalTriggerService { get; set; }
    public string Name { get; set; } = "Deathmatch";
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxDeaths = 15;
    public bool IsTicket = true;
    public bool IsRunning { get; set; } = false;
    public bool IsFinished { get; set; } = false;
    public DateTime? StartTime { get; set; }

    private CancellationTokenSource abort = new();

    public Dictionary<TeamColor, int> TeamDeaths = new();

    public GameModeTeamDeathmatch(ExternalTriggerService externalTriggerService)
    {
        ExternalTriggerService = externalTriggerService;
        ExternalTriggerService.KlickerPressedAction += ClickerPressed;
    }

    private void ClickerPressed(object? sender, KlickerEventArgs args)
    {
        if (StartTime != null)
        {
            TeamDeaths[args.TeamColor] += 1;
            ExternalTriggerService.StateHasChangedAction.Invoke();
        }
    }

    public TimeSpan? GetTimer 
    {
        get
        {
            if (StartTime == null)
                return new TimeSpan(0, GameDurationInMinutes, 0);
            else
                return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now;
        }
    }

    public void FillTeams(List<TeamBase> teams)
    {
        foreach (var teamColor in teams)    
        {
            TeamDeaths[teamColor.TeamColor] = 0;
        }
    }

    public string GetTeamPoints(TeamColor color)
    {
        int retval = 0;

        if (IsTicket)
        {
            retval = MaxDeaths - TeamDeaths[color];
        }
        else
        {
            retval = TeamDeaths[color];
        }
        
        return retval.ToString();
    }
    
    public void RunGame()
    {
        StartTime = DateTime.Now;
        IsRunning = true;
        Task.Run(Runner, abort.Token);
    }

    private void Runner()
    {
        while (true)
        {
            Thread.Sleep(500);

            if (abort.IsCancellationRequested)
            {
                EndGame();
                break;
            }

            if (StartTime?.AddMinutes(GameDurationInMinutes) == DateTime.Now)
            {
                EndGame();
                break;
            }

            if (TeamDeaths.Any(e => e.Value >= MaxDeaths))
            {
                EndGame();
                break;
            }
            
            ExternalTriggerService.StateHasChangedAction.Invoke();
        }
    }

    public void EndGame()
    {
        IsRunning = false;
        IsFinished = true;
        //throw new NotImplementedException();
        // not implemented
        // hier Trigger triggern: Signalanlage (Spielende), UI Refresh
    }

    public TeamColor GetWinner()
    {
        if (TeamDeaths.Values.Distinct().Count() == 1)
        {
            return TeamColor.NONE;
        }

        return TeamDeaths.First(e => e.Value == TeamDeaths.Values.Min()).Key;
    }

    public int GetTeamDeaths(TeamColor team)
    {
        return TeamDeaths[team];
    }
    
    public override string ToString()
    {
        return Name;
    }

    public void Dispose()
    {
        ExternalTriggerService.KlickerPressedAction -= ClickerPressed;
        StartTime = null;
        abort.Dispose();
    }
}