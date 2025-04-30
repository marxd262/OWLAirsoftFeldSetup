namespace OWLServer.Models
{
    public class TeamBase
    {
        public TeamColor TeamColor { get; set; }
        public string Name { get; set; }
        
        public TeamBase(TeamColor teamColor, string name = "")
        {
            TeamColor = teamColor;
            Name = string.IsNullOrEmpty(name) ? teamColor.ToString() : name;
        }
    }
}
