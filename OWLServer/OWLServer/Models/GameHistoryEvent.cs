namespace OWLServer.Models
{
    public class GameHistoryEvent
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public int EventType { get; set; }
        public int TeamColor { get; set; }
        public string Side { get; set; } = string.Empty;
        public string? TowerLetter { get; set; }
        public int? Value { get; set; }

        public GameHistory? GameHistory { get; set; }
    }
}
