using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeTeamDeathmatch : IGameModeBase, IDisposable
{
    private IExternalTriggerService ExternalTriggerService { get; set; }
    private IGameStateService GameStateService { get; set; }
    public string Name { get; set; } = "Teamdeathmatch";
    public GameMode GameMode => GameMode.TeamDeathMatch;
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxTickets { get; set; } = 15;
    public bool IsTicket = true;
    public bool IsPaused { get; set; }
    public TimeSpan PausedDuration { get; set; }
    public DateTime? PauseStartedAt { get; set; }
    public bool IsRunning { get; set; }
    public bool IsFinished { get; set; }
    public DateTime? StartTime { get; set; }

    private CancellationTokenSource _abort = new();

    public Dictionary<TeamColor, int> TeamDeaths = new();

    public GameModeTeamDeathmatch(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;
        ExternalTriggerService.KlickerPressedAction += ClickerPressed;
    }

    private void ClickerPressed(object? sender, KlickerEventArgs args)
    {
        if (StartTime != null && IsRunning)
        {
            TeamDeaths[args.TeamColor] += 1;
            try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
            catch { }
        }
    }

    public TimeSpan? GetTimer
    {
        get
        {
            if (StartTime == null || IsFinished)
                return new TimeSpan(0, IsFinished ? 0 : GameDurationInMinutes, 0);
            if (IsPaused && PauseStartedAt != null)
                return StartTime.Value.AddMinutes(GameDurationInMinutes) - PauseStartedAt.Value + PausedDuration;
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now + PausedDuration;
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
        
        return points < 0 ? 0 : points;
    }

    public bool ShowRespawnButton => true;

    public void RunGame()
    {
        StartTime = DateTime.Now;
        IsRunning = true;
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
        Task.Run(Runner, _abort.Token);
    }

    private async void Runner()
    {
        while (true)
        {
            await Task.Delay(500);

            if (_abort.IsCancellationRequested)
            {
                EndGame();
                break;
            }

            if (IsPaused) continue;

            if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now)
            {
                EndGame();
                break;
            }

            if (TeamDeaths.Any(e => e.Value >= MaxTickets))
            {
                EndGame();
                break;
            }
            
            try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
            catch { }
        }
    }

    public void EndGame()
    {
        if (IsFinished) return;
        _abort.Cancel();
        IsRunning = false;
        IsFinished = true;
        StartTime = null;
        GameStateService.HandleGameEnd();
    }

    public void ResetGame()
    {
        if (IsRunning) EndGame();
        IsFinished = false;
        StartTime = null;
        foreach (var key in TeamDeaths.Keys.ToList())
            TeamDeaths[key] = 0;
        _abort.Dispose();
        _abort = new CancellationTokenSource();
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
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