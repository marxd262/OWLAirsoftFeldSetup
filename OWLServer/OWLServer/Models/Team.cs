using OWLServer.Services;

namespace OWLServer.Models
{
    public class Team
    {
        public Team(enums.TeamColor teamColor)
        {
            TeamColor = teamColor;
        }

        public enums.TeamColor TeamColor { get; set; }
        public int Points { get; set; } = 0;
        public int Deaths { get; set; } = 0;

        public void Reset()
        {
            Points = 0;
            Deaths = 0;
        }
    }
}
