namespace OWLServer.Models
{
    public class GameHistoryTeam
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public int TeamColor { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public int FinalScore { get; set; }
        public int Deaths { get; set; }
        public int TowersControlled { get; set; }

        public GameHistory? GameHistory { get; set; }
    }
}
