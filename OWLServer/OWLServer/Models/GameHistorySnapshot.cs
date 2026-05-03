namespace OWLServer.Models
{
    public class GameHistorySnapshot
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public string SnapshotJSON { get; set; } = string.Empty;

        public GameHistory? GameHistory { get; set; }
    }
}
