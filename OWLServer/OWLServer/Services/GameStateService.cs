using System.Drawing;
using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services
{
    public class GameStateService
    {
        public ExternalTriggerService ExternalTriggerService { get; set; } = null!;
        public AudioService AudioService { get; set; } = null!;

        public IGameModeBase? CurrentGame { get; set; } = null!;
        public TowerManagerService TowerManagerService { get; set; }
        public Dictionary<TeamColor, TeamBase> Teams { get; set; } = new();

        public bool TeamRedReady { get; set; } = false;
        public bool TeamBlueReady { get; set; } = false;
        
        public bool TeamSetReady { get; set; } = false;
        
        public bool AutoStartAfterReady { get; set; } = false;
        public int SecondsTillAutoStartAfterReady { get; set; } = 20;
        
        public GameStateService(ExternalTriggerService externalTriggerService, AudioService audioService)
        {
            ExternalTriggerService = externalTriggerService;
            AudioService = audioService;
            
            TowerManagerService = new TowerManagerService(externalTriggerService, this);
            TowerManagerService.RunTowerManager();

            Teams.Add(TeamColor.BLUE, new TeamBase(TeamColor.BLUE, ColorTranslator.FromHtml("#00b4f1")));
            Teams.Add(TeamColor.RED, new TeamBase(TeamColor.RED, ColorTranslator.FromHtml("#fc1911")));
        }

        public void TriggerStateHasChanged()
        {
        }

        public void StartGame()
        {
            AudioService.PlaySound(Sounds.Countdown);
            AudioService.PlaySound(Sounds.Start);
            CurrentGame?.RunGame();
        }

        public void StopGame()
        {
            CurrentGame?.EndGame();
        }

        public void Reset()
        {
            TowerManagerService.ResetTowers();
        }
    }
}
