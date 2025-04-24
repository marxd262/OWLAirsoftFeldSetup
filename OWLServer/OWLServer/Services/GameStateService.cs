using OWLServer.Models;

namespace OWLServer.Services
{
    public class GameStateService
    {

        public event Action EH;

        public Dictionary<enums.TeamColor, Team> Teams { get;set; }
        public Dictionary<int, Tower> Towers { get; set; }

        public GameStateService() 
        {
            Teams = new Dictionary<enums.TeamColor, Team>();
            Towers = new Dictionary<int, Tower>();

            Teams.Add(enums.TeamColor.RED, new Team(enums.TeamColor.RED));
            Teams.Add(enums.TeamColor.GREEN, new Team(enums.TeamColor.GREEN));
        }

        public void AddTower()
        {
            int newID = Towers.Count;
            Towers.Add(newID, new Tower(newID));
        }

        public void TowerChangeColor(int TowerID, enums.TeamColor newColor)
        {
            if (Towers.ContainsKey(TowerID))
            {
                Towers[TowerID].CurrentColor = newColor;
                EH.Invoke();
            }
        }

        public int AddPoints(enums.TeamColor color, int points)
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
