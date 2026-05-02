using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services
{
    public class GameStateService : IGameStateService
    {
        public IExternalTriggerService ExternalTriggerService { get; set; } = null!;
        public IAudioService AudioService { get; set; } = null!;

        public IGameModeBase? CurrentGame { get; set; } = null!;
        public ITowerManagerService TowerManagerService { get; set; }
        public Dictionary<TeamColor, TeamBase> Teams { get; set; } = new();
        
        public TeamColor TeamInWald { get; set; } = TeamColor.BLUE;
        public TeamColor TeamInStadt { get; set; } = TeamColor.RED;

        public bool WaldSpawnReady { get; set; } = false;
        public bool StadtSpawnReady { get; set; } = false;
        public bool TeamSetReady { get; set; } = false;
        
        public DateTime? AutoStartProcessStarted { get; set; } = null;
        public CancellationTokenSource AutoStartCancellationTokenSrc { get; set; } = new CancellationTokenSource();
        
        public bool AutoStartAfterReady { get; set; } = false;
        public int SecondsTillAutoStartAfterReady { get; set; } = 20;

        public int? AutoStartSecondsRemaining => 
            AutoStartProcessStarted.HasValue 
                ? Math.Max(0, SecondsTillAutoStartAfterReady - (int)(DateTime.Now - AutoStartProcessStarted.Value).TotalSeconds)
                : null;

        public bool AutoStartCountdownActive => 
            AutoStartAfterReady 
            && AutoStartProcessStarted.HasValue 
            && (DateTime.Now - AutoStartProcessStarted.Value).TotalSeconds < SecondsTillAutoStartAfterReady;

        public bool AutoStartWaitingForSpawns =>
            AutoStartAfterReady && !AutoStartProcessStarted.HasValue;
        
        public GameStateService(IExternalTriggerService externalTriggerService, IAudioService audioService,
                                ITowerManagerService towerManagerService)
        {
            ExternalTriggerService = externalTriggerService;
            AudioService = audioService;
            TowerManagerService = towerManagerService;

            Teams.Add(TeamColor.BLUE, new TeamBase(TeamColor.BLUE));
            Teams.Add(TeamColor.RED, new TeamBase(TeamColor.RED));
        }

        public async void StartGame()
        {
            AudioService.PlaySound(Sounds.Start);
            var delay = AudioService.GetDelay(Sounds.Start);
            if (delay > 0) await Task.Delay(delay * 1000);
            CurrentGame?.RunGame();
        }

        public void StopGame()
        {
            CurrentGame?.EndGame();
        }

        public void HandleGameEnd()
        {
            AudioService.PlaySound(Sounds.Stop);
            try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
            catch { }
        }

        public async void AutoStartGame()
        {
            while (AutoStartAfterReady)
            {
                AutoStartProcessStarted = null;
                try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }

                while ((!StadtSpawnReady || !WaldSpawnReady))
                {
                    await Task.Delay(100);
                    try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }

                    if (AutoStartCancellationTokenSrc.IsCancellationRequested) return;
                }

                AutoStartProcessStarted = DateTime.Now;
                try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }

                while ((DateTime.Now - AutoStartProcessStarted).Value.TotalSeconds < SecondsTillAutoStartAfterReady && StadtSpawnReady && WaldSpawnReady)
                {
                    if (AutoStartCancellationTokenSrc.IsCancellationRequested) return;
                    await Task.Delay(100);
                    try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }
                }

                if (AutoStartCancellationTokenSrc.IsCancellationRequested) return;

                if (!StadtSpawnReady || !WaldSpawnReady)
                    continue;

                StartGame();
                StadtSpawnReady = false;
                WaldSpawnReady = false;
                AutoStartAfterReady = false;
                return;
            }
        }


        public void Reset()
        {
            CurrentGame?.ResetGame();
            TowerManagerService.ResetTowers();
            WaldSpawnReady = false;
            StadtSpawnReady = false;
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }
}
