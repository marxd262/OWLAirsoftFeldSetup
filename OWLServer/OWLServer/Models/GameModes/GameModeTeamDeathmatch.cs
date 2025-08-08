using OWLServer.Services;

namespace OWLServer.Models.GameModes;

public class GameModeTeamDeathmatch : IGameModeBase, IDisposable
{
    private ExternalTriggerService ExternalTriggerService { get; set; }
    public string Name { get; set; } = "Deathmatch";
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxTickets { get; set; } = 15;
    public bool IsTicket = true;
    public bool IsRunning { get; set; }
    public bool IsFinished { get; set; }
    public DateTime? StartTime { get; set; }

    private CancellationTokenSource _abort = new();

    public Dictionary<TeamColor, int> TeamDeaths = new();

    public GameModeTeamDeathmatch(ExternalTriggerService externalTriggerService)
    {
        ExternalTriggerService = externalTriggerService;
        ExternalTriggerService.KlickerPressedAction += ClickerPressed;
    }

    private void ClickerPressed(object? sender, KlickerEventArgs args)
    {
        if (StartTime != null && IsRunning)
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

    public int GetDisplayPoints(TeamColor color)
    {
        int points;

        if (IsTicket)
        {
            points = MaxTickets - TeamDeaths[color];
        }
        else
        {
            points = TeamDeaths[color];
        }
        
        return points;
    }

    public bool ShowRespawnButton => true;

    public void RunGame()
    {
        StartTime = DateTime.Now;
        IsRunning = true;
        Task.Run(Runner, _abort.Token);
    }

    private void Runner()
    {
        while (true)
        {
            Thread.Sleep(500);

            if (_abort.IsCancellationRequested)
            {
                EndGame();
                break;
            }

            if (StartTime?.AddMinutes(GameDurationInMinutes) == DateTime.Now)
            {
                EndGame();
                break;
            }

            if (TeamDeaths.Any(e => e.Value >= MaxTickets))
            {
                EndGame();
                break;
            }
            
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }

    public void EndGame()
    {
        IsRunning = false;
        IsFinished = true;
        StartTime = null;
        //throw new NotImplementedException();
        // not implemented
        // hier Trigger triggern: Signalanlage (Spielende), UI Refresh
    }

    public TeamColor GetWinner
    {
        get
        {
            if (TeamDeaths.Values.Distinct().Count() == 1)
            {
                return TeamColor.NONE;
            }

            return TeamDeaths.First(e => e.Value == TeamDeaths.Values.Min()).Key;
        }
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
        _abort.Dispose();
    }
}