using OWLServer.Models;

namespace OWLServer.Services
{
    public class GameStateService
    {

        public event Action EH;

        public Dictionary<TeamColor, Team> Teams { get; set; }
        public Dictionary<int, Tower> Towers { get; set; }

        public GameStateService()
        {
            Teams = new Dictionary<TeamColor, Team>();
            Towers = new Dictionary<int, Tower>();

            Teams.Add(TeamColor.RED, new Team(TeamColor.RED));
            Teams.Add(TeamColor.GREEN, new Team(TeamColor.GREEN));
        }

        public void AddTower()
        {
            int newID = Towers.Count;
            Towers.Add(newID, new Tower(newID));
        }

        public void TowerChangeColor(int TowerID, TeamColor newColor)
        {
            if (Towers.ContainsKey(TowerID))
            {
                Towers[TowerID].CurrentColor = newColor;
                EH.Invoke();
            }
        }

        public int AddPoints(TeamColor color, int points)
        {
            if (Teams.ContainsKey(color))
            {
                Teams[color].Points += points;
                EH.Invoke();
                return Teams[color].Points;
            }

            return 0;
        }

        public void Reset()
        {
            ResetTeams();
            ResetTowers();
        }

        public void ResetTowers()
        {
            foreach (var item in Towers)
            {
                item.Value.Reset();
            }
        }

        public void ResetTeams()
        {
            foreach (var item in Teams)
            {
                item.Value.Reset();
            }
        }
    }
}
