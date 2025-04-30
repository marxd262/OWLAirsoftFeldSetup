namespace OWLServer.Models.GameModes;

public interface IGameModeBase
{
    public string Name { get; set; }
    public int GameDurationInMinutes { get; set; }
    public DateTime? StartTime { get; set; }
    
    public abstract void RunGame();
    public abstract void EndGame();
    public abstract TeamColor GetWinner();
    public abstract string ToString();
}