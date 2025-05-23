﻿using System.Drawing;
using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services
{
    public class GameStateService
    {
        public ExternalTriggerService ExternalTriggerService { get; set; } = null!;

        public IGameModeBase? CurrentGame { get; set; } = null!;
        public TowerManagerService TowerManagerService { get; set; }
        public Dictionary<TeamColor, TeamBase> Teams { get; set; } = new();
        
        public GameStateService(ExternalTriggerService externalTriggerService)
        {
            ExternalTriggerService = externalTriggerService;
            
            TowerManagerService = new TowerManagerService(externalTriggerService, this);
            TowerManagerService.RunTowerManager();

            Teams.Add(TeamColor.RED, new TeamBase(TeamColor.RED, Color.LightCoral));
            Teams.Add(TeamColor.BLUE, new TeamBase(TeamColor.BLUE, Color.CornflowerBlue));
        }

        public void Reset()
        {
            TowerManagerService.ResetTowers();
        }

    }
}
