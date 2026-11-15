using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services
{
    public class GameStateService : IGameStateService
    {
        public IExternalTriggerService ExternalTriggerService { get; set; } = null!;
        public IAudioService AudioService { get; set; } = null!;
        public IGameHistoryService GameHistoryService { get; set; } = null!;

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
                                ITowerManagerService towerManagerService, IGameHistoryService gameHistoryService)
        {
            ExternalTriggerService = externalTriggerService;
            AudioService = audioService;
            TowerManagerService = towerManagerService;
            GameHistoryService = gameHistoryService;

            Teams.Add(TeamColor.BLUE, new TeamBase(TeamColor.BLUE));
            Teams.Add(TeamColor.RED, new TeamBase(TeamColor.RED));
        }

        private CancellationTokenSource? _startDelayCts;

        public async void StartGame()
        {
            if (CurrentGame == null) return; // no mode selected: no horn, no ghost history entry
            Reset(); // every start path, auto-start included, begins with a fresh game

            _startDelayCts?.Cancel();
            var cts = _startDelayCts = new CancellationTokenSource();

            AudioService.PlaySound(Sounds.Start);
            var delay = AudioService.GetDelay(Sounds.Start);
            if (delay > 0)
            {
                try { await Task.Delay(delay * 1000, cts.Token); }
                catch (TaskCanceledException) { return; } // Stop (or a second Start) during the start sound
            }

            if (CurrentGame is not { } game) return;
            // recorded after the delay so history duration and events match the actual game
            try { GameHistoryService.RecordGameStart(game, TowerManagerService.Towers, Teams, TeamInWald); }
            catch (Exception ex) { Console.Error.WriteLine($"GameHistory: game start not recorded: {ex.Message}"); } // history must not stop the game
            game.RunGame();
        }

        public void StopGame()
        {
            _startDelayCts?.Cancel();
            GameHistoryService.EndReason = "Stopped";
            CurrentGame?.EndGame();
        }

        public void HandleGameEnd()
        {
            // runs on the game loop thread; a database error must not take the server down
            try { GameHistoryService.RecordGameEnd(CurrentGame, TowerManagerService.Towers); }
            catch (Exception ex) { Console.Error.WriteLine($"GameHistory: game end not recorded: {ex.Message}"); }
            AudioService.PlaySound(Sounds.Stop);
            try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
            catch { }
        }

        public async void AutoStartGame()
        {
            var cancel = AutoStartCancellationTokenSrc.Token; // toggling swaps the source; this loop must keep seeing its own cancel
            while (AutoStartAfterReady)
            {
                AutoStartProcessStarted = null;
                try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }

                while ((!StadtSpawnReady || !WaldSpawnReady))
                {
                    await Task.Delay(100);
                    try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }

                    if (cancel.IsCancellationRequested) return;
                }

                AutoStartProcessStarted = DateTime.Now;
                try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }

                while ((DateTime.Now - AutoStartProcessStarted).Value.TotalSeconds < SecondsTillAutoStartAfterReady && StadtSpawnReady && WaldSpawnReady)
                {
                    if (cancel.IsCancellationRequested) return;
                    await Task.Delay(100);
                    try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }
                }

                if (cancel.IsCancellationRequested) return;

                if (!StadtSpawnReady || !WaldSpawnReady)
                    continue;

                StartGame();
                StadtSpawnReady = false;
                WaldSpawnReady = false;
                AutoStartAfterReady = false;
                return;
            }
        }


        // Devices and spawn tablets sit in a spawn; which team is there changes when sides are swapped.
        public TeamColor TeamAtSpawn(string spawn) => spawn switch
        {
            "Wald" => TeamInWald,
            "Stadt" => TeamInStadt,
            _ => TeamColor.NONE
        };

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
