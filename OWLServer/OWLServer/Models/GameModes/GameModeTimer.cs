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

            if (StartTime?.AddMinutes(GameDurationInMinutes) == DateTime.Now)
            {
                EndGame();
                break;
            }
            
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }

    public void EndGame()
    {
        _abort.Cancel();
        IsRunning = false;
        StartTime = null;
        //throw new NotImplementedException();
        // not implemented
        // hier Trigger triggern: Signalanlage (Spielende), UI Refresh
    }

    public TeamColor GetWinner => TeamColor.NONE;
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
    public int GetDisplayPoints(TeamColor color)
    {
        return MaxTickets;
    }

    public void FillTeams(List<TeamBase> teams)
    {
    }
}