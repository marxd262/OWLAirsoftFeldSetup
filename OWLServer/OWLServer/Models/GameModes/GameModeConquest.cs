using System.Net.Http.Headers;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components;
using OWLServer.Services;

namespace OWLServer.Models.GameModes;

public class GameModeConquest : IGameModeBase, IDisposable
{
    private ExternalTriggerService ExternalTriggerService { get; set; }
    private GameStateService GameStateService { get; set; }
    public string Name { get; set; } = "Conquest";
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxTickets { get; set; } = 15;
    public bool IsTicket = true;
    public int PointDistributionFrequencyInSeconds { get; set; } = 5;
    public bool IsRunning { get; set; } = false;
    public bool IsFinished { get; set; } = false;
    public DateTime? StartTime { get; set; }

    private CancellationTokenSource abort = new();

    public Dictionary<TeamColor, int> TeamPoints = new();

    public GameModeConquest(ExternalTriggerService externalTriggerService, GameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;
    }

    public TimeSpan? GetTimer 
    {
        get
        {
            if (StartTime == null)
                return new TimeSpan(0, GameDurationInMinutes, 0);
            else if (IsFinished)
                return new TimeSpan(0, 0, 0);
            else
                return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now;
        }
    }

    public void FillTeams(List<TeamBase> teams)
    {
        foreach (var teamColor in teams)    
        {
            TeamPoints[teamColor.TeamColor] = 0;
        }
    }

    public int GetDisplayPoints(TeamColor color)
    {
        int points = 0;

        if (IsTicket)
        {
            if(color == TeamColor.BLUE)
                points = MaxTickets - TeamPoints[TeamColor.RED];
            else if (color == TeamColor.RED)
                points = MaxTickets - TeamPoints[TeamColor.BLUE];
        }
        else
        {
            points = TeamPoints[color];
        }
        
        return points;
    }
    
    public void RunGame()
    {
        StartTime = DateTime.Now;
        IsRunning = true;
        Task.Run(Runner, abort.Token);
    }

    private void Runner()
    {
        DateTime lastPointDistributed = DateTime.Now;
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

            if (lastPointDistributed.AddSeconds(PointDistributionFrequencyInSeconds) <= DateTime.Now)
            {
                DistributePoints();
                lastPointDistributed = DateTime.Now;
            }

            if (TeamPoints.Any(e => e.Value >= MaxTickets))
            {
                EndGame();
                break;
            }
            
            ExternalTriggerService.StateHasChangedAction.Invoke();
        }
    }

    private void DistributePoints()
    {
        foreach (var teamColor in TeamPoints.Keys)
        {
            TeamPoints[teamColor] += GameStateService.TowerManagerService.GetPoints(teamColor);
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

    public TeamColor GetWinner
    {
        get
        {
            if (TeamPoints.Values.Distinct().Count() == 1)
            {
                return TeamColor.NONE;
            }

            return TeamPoints.First(e => e.Value == TeamPoints.Values.Min()).Key;
        }
    }

    public int GetTeamPoints(TeamColor team)
    {
        return TeamPoints[team];
    }
    
    public override string ToString()
    {
        return Name;
    }

    public void Dispose()
    {
        StartTime = null;
        abort.Dispose();
    }
}