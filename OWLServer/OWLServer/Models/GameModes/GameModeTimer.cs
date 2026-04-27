using OWLServer.Services;

namespace OWLServer.Models.GameModes;

public class GameModeTimer : IGameModeBase
{
    private ExternalTriggerService ExternalTriggerService { get; set; }
    private GameStateService GameStateService { get; set; }
    public string Name { get; set; } = "Zeitspiel";
    public int GameDurationInMinutes { get; set; } = 20;
    public DateTime? StartTime { get; set; }
    public bool IsRunning { get; set; }
    public bool IsFinished { get; set; }
    public int MaxTickets { get; set; } = 1000;
    private CancellationTokenSource _abort = new();
    public bool ShowRespawnButton => false;
    public GameMode GameMode => GameMode.Timer;
    
    public GameModeTimer (ExternalTriggerService externalTriggerService, GameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;
    }
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

            if (StartTime?.AddMinutes(GameDurationInMinutes) <= DateTime.Now)
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
    }

    public TeamColor GetWinner => TeamColor.NONE;
    public TimeSpan? GetTimer
    {
        get
        {
            if (StartTime == null || IsFinished)
                return new TimeSpan(0, IsFinished ? 0 : GameDurationInMinutes, 0);
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now;
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