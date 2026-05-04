namespace OWLServer.Models
{
    public class GameHistoryTower
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string DisplayLetter { get; set; } = string.Empty;
        public int FinalColor { get; set; }

        public GameHistory? GameHistory { get; set; }
    }
}
