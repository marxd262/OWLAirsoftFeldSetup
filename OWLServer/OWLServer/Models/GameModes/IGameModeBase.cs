namespace OWLServer.Models.GameModes;

public interface IGameModeBase
{
    public string Name { get; set; }
    public int GameDurationInMinutes { get; set; }
    public DateTime? StartTime { get; set; }
    public bool IsRunning { get; set; }
    public bool IsFinished { get; set; }
    public int MaxTickets { get; set; }
    public bool ShowRespawnButton { get; }
    public GameMode GameMode { get;  }
    
    public abstract void RunGame();
    public abstract void EndGame();
    public abstract void ResetGame();
    public abstract TeamColor GetWinner { get; }
    public abstract string ToString();
    public TimeSpan? GetTimer { get; }
    public int GetDisplayPoints(TeamColor color);

    public abstract void FillTeams(List<TeamBase> teams);

    public bool IsPaused { get; set; }
    public TimeSpan PausedDuration { get; set; }
    public DateTime? PauseStartedAt { get; set; }

    public void PauseGame()
    {
        if (!IsRunning || IsPaused) return;
        IsPaused = true;
        PauseStartedAt = DateTime.Now;
    }

    public void ResumeGame()
    {
        if (!IsRunning || !IsPaused) return;
        IsPaused = false;
        if (PauseStartedAt != null)
            PausedDuration += DateTime.Now - PauseStartedAt.Value;
        PauseStartedAt = null;
    }
}
