using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;
using OWLServer.Services;

namespace OWLServer.Models
{
    public class TeamBase
    {
        public int Id { get; set; }

        public TeamColor TeamColor { get; set; }

        public string Name { get; set; }

        [NotMapped]
        public string ColorBackground => $"background-color: {Util.HTMLColorForTeam(TeamColor)}";

        [NotMapped]
        public string ColorCss => Util.HTMLColorForTeam(TeamColor);

        [NotMapped]
        public string ColorCssImportant => $"{Util.HTMLColorForTeam(TeamColor)}!Important";

        private Color _displayColor = Color.Transparent;
        
        public TeamBase(TeamColor teamColor)
        {
            TeamColor = teamColor;
            Name = teamColor.ToString();
        }

        public TeamBase() { } //Benötigt für die DB
    }
}
