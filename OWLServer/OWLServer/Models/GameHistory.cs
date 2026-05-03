namespace OWLServer.Models
{
    public class GameHistory
    {
        public int Id { get; set; }
        public int GameMode { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int Winner { get; set; }
        public string EndReason { get; set; } = string.Empty;

        public List<GameHistoryTeam> Teams { get; set; } = new();
        public List<GameHistoryTower> Towers { get; set; } = new();
        public GameHistorySnapshot? Snapshot { get; set; }
    }
}
