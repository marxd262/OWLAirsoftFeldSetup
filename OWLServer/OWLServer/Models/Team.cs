using System.Drawing;

namespace OWLServer.Models
{
    public class TeamBase
    {
        public TeamColor TeamColor { get; set; }
        public string Name { get; set; }
        public string ColorBackground => $"background-color: {ColorTranslator.ToHtml(_displayColor)}";
        public string ColorCss => ColorTranslator.ToHtml(_displayColor);
        public string ColorCssImportant => $"{ColorTranslator.ToHtml(_displayColor)}!Important";

        private Color _displayColor = Color.Transparent;
        
        public TeamBase(TeamColor teamColor, Color displayColor)
        {
            TeamColor = teamColor;
            Name = teamColor.ToString();
            _displayColor = displayColor;
        }
    }
}
