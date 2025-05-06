using System.Drawing;

namespace OWLServer.Models
{
    public class TeamBase
    {
        public TeamColor TeamColor { get; set; }
        public string Name { get; set; }
        public string ColorBackground { get; set; }

        private Color _displayColor = Color.Transparent;
        
        public TeamBase(TeamColor teamColor, Color displayColor)
        {
            TeamColor = teamColor;
            Name = teamColor.ToString();
            ColorBackground = $"background-color: {ColorTranslator.ToHtml(displayColor)}";
        }
    }
}
