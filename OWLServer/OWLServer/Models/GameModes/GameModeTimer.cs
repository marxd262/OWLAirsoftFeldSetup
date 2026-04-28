using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeTimer : IGameModeBase
{
    private IExternalTriggerService ExternalTriggerService { get; set; }
    private IGameStateService GameStateService { get; set; }
    public string Name { get; set; } = "Zeitspiel";
    public int GameDurationInMinutes { get; set; } = 20;
    public DateTime? StartTime { get; set; }
    public bool IsRunning { get; set; }
    public bool IsFinished { get; set; }
    public int MaxTickets { get; set; } = 1000;
    private CancellationTokenSource _abort = new();
    public bool ShowRespawnButton => false;
    public bool IsPaused { get; set; }
    public TimeSpan PausedDuration { get; set; }
    public DateTime? PauseStartedAt { get; set; }
    public GameMode GameMode => GameMode.Timer;
    
    public GameModeTimer (IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;
    }
    public void RunGame()
    {
        StartTime = DateTime.Now;
        IsRunning = true;
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
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

            if (IsPaused) continue;

            if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now)
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
        _abort.Dispose();
        _abort = new CancellationTokenSource();
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
    }

    public TeamColor GetWinner => TeamColor.NONE;
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
    public int GetDisplayPoints(TeamColor color)
    {
        return MaxTickets;
    }

    public void FillTeams(List<TeamBase> teams)
    {
    }
}