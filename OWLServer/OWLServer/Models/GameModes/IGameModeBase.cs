namespace OWLServer.Models.GameModes;

public interface IGameModeBase
{
    public string Name { get; set; }
    public int GameDurationInMinutes { get; set; }
    public DateTime? StartTime { get; set; }
    public bool IsRunning { get; set; }
    public int MaxTickets { get; set; }
    
    public abstract void RunGame();
    public abstract void EndGame();
    public abstract TeamColor GetWinner { get; }
    public abstract string ToString();
    public TimeSpan? GetTimer { get; }
    public int GetDisplayPoints(TeamColor color);

    public abstract void FillTeams(List<TeamBase> teams);
}