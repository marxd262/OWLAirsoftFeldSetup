using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

/// <summary>
/// Clock, pause, game loop, end and reset shared by every game mode.
/// A mode adds its own <see cref="Tick"/> and scoring.
/// </summary>
public abstract class GameModeBase : IGameModeBase, IDisposable
{
    private static readonly TimeSpan ColorResendInterval = TimeSpan.FromSeconds(10);

    protected IExternalTriggerService ExternalTriggerService { get; }
    protected IGameStateService GameStateService { get; }

    public abstract string Name { get; set; }
    public abstract GameMode GameMode { get; }
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxTickets { get; set; } = 15;
    public virtual bool ShowRespawnButton => false;
    public bool IsPaused { get; set; }
    public TimeSpan PausedDuration { get; set; }
    public DateTime? PauseStartedAt { get; set; }
    public bool IsRunning { get; set; }
    public bool IsFinished { get; set; }
    public DateTime? StartTime { get; set; }

    protected virtual TimeSpan TickInterval => TimeSpan.FromMilliseconds(200);

    private CancellationTokenSource _abort = new();

    protected GameModeBase(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;
    }

    public abstract TeamColor GetWinner { get; }
    public abstract int GetDisplayPoints(TeamColor color);
    public abstract void FillTeams(List<TeamBase> teams);

    /// <summary>One game step. Returns true when a win condition ends the game.</summary>
    protected abstract bool Tick();

    public TimeSpan? GetTimer
    {
        get
        {
            if (IsFinished) return TimeSpan.Zero;
            if (StartTime == null) return TimeSpan.FromMinutes(GameDurationInMinutes);
            var end = StartTime.Value.AddMinutes(GameDurationInMinutes) + PausedDuration;
            return end - (IsPaused && PauseStartedAt != null ? PauseStartedAt.Value : DateTime.Now);
        }
    }

    public virtual void RunGame()
    {
        StartTime = DateTime.Now;
        IsRunning = true;
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
        Task.Run(Runner);
    }

    private async Task Runner()
    {
        var abort = _abort.Token; // ResetGame swaps _abort; this loop must keep seeing its own stop signal
        var lastColorResend = DateTime.Now;
        while (true)
        {
            await Task.Delay(TickInterval);
            try
            {
                if (abort.IsCancellationRequested) { EndGame(); return; }
                if (IsPaused) continue;

                if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now || Tick())
                {
                    EndGame();
                    return;
                }

                if (DateTime.Now - lastColorResend >= ColorResendInterval)
                {
                    // a lost or out-of-order color update would show the wrong owner until the next change
                    lastColorResend = DateTime.Now;
                    foreach (var tower in GameStateService.TowerManagerService.Towers.Values)
                        tower.SetTowerColor(tower.CurrentColor);
                }

                ExternalTriggerService.StateHasChangedAction?.Invoke();
            }
            catch (Exception ex)
            {
                // an exception escaping this background loop would end the whole server process
                Console.Error.WriteLine($"[{Name}] game loop error: {ex}");
            }
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

    public virtual void ResetGame()
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

    public void PauseGame()
    {
        if (!IsRunning || IsPaused) return;
        IsPaused = true;
        PauseStartedAt = DateTime.Now;
    }

    public void ResumeGame()
    {
        if (!IsRunning || !IsPaused) return;
        if (PauseStartedAt is DateTime pausedAt)
        {
            var now = DateTime.Now;
            var pausedFor = now - pausedAt;
            // tower timers run on wall-clock time; move them so the pause doesn't count (done before unpausing so no tick sees old values)
            foreach (var tower in GameStateService.TowerManagerService.Towers.Values)
            {
                tower.CapturedAt += pausedFor;
                if (tower.LastPressed < pausedAt) tower.LastPressed += pausedFor;
                else if (tower.LastPressed != null) tower.LastPressed = now; // pressed during the pause: the hold starts now
            }
            PausedDuration += pausedFor;
        }
        IsPaused = false;
        PauseStartedAt = null;
    }

    public override string ToString() => Name;

    public virtual void Dispose()
    {
        StartTime = null;
        _abort.Dispose();
    }
}
