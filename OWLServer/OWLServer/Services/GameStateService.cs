using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services
{
    public class GameStateService
    {
        public ExternalTriggerService ExternalTriggerService { get; set; } = null!;

        public IGameModeBase CurrentGame { get; set; } = null!;
        public TowerManagerService TowerManagerService { get; set; }
        public Dictionary<TeamColor, TeamBase> Teams { get; set; } = new();
        
        public GameStateService(ExternalTriggerService externalTriggerService)
        {
            ExternalTriggerService = externalTriggerService;
            
            TowerManagerService = new TowerManagerService(externalTriggerService);

            Teams.Add(TeamColor.RED, new TeamBase(TeamColor.RED));
            Teams.Add(TeamColor.GREEN, new TeamBase(TeamColor.GREEN));
        }

        public void Reset()
        {
            TowerManagerService.ResetTowers();
        }

    }
}
