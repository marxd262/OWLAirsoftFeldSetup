using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using OWLServer.Models;

namespace OWLServer.Services
{
    public class GameStateService
    {
        [Inject] 
        public ExternalTriggerService ExternalTriggerService { get; set; } = null!;

        public Dictionary<TeamColor, TeamBase> Teams { get; set; }
        public Dictionary<int, Tower> Towers { get; set; }

        public GameStateService()
        {
            Teams = new Dictionary<TeamColor, TeamBase>();
            Towers = new Dictionary<int, Tower>();

            Teams.Add(TeamColor.RED, new TeamBase(TeamColor.RED));
            Teams.Add(TeamColor.GREEN, new TeamBase(TeamColor.GREEN));
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
                ExternalTriggerService.StateHasChangedAction.Invoke();
            }
        }

        public void Reset()
        {
            ResetTowers();
        }

        public void ResetTowers()
        {
            foreach (var item in Towers)
            {
                item.Value.Reset();
            }
        }
    }
}
