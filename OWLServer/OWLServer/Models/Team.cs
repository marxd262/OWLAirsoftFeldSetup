using System.Drawing;
using OWLServer.Services;

namespace OWLServer.Models
{
    public class TeamBase
    {
        public TeamColor TeamColor { get; set; }
        public string Name { get; set; }
        public string ColorBackground => $"background-color: {Util.HTMLColorForTeam(TeamColor)}";
        public string ColorCss => Util.HTMLColorForTeam(TeamColor);
        public string ColorCssImportant => $"{Util.HTMLColorForTeam(TeamColor)}!Important";

        private Color _displayColor = Color.Transparent;
        
        public TeamBase(TeamColor teamColor)
        {
            TeamColor = teamColor;
            Name = teamColor.ToString();
        }
    }
}
