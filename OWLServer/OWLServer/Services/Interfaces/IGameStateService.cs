using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services.Interfaces;

public interface IGameStateService
{
    IExternalTriggerService ExternalTriggerService { get; }
    IAudioService AudioService { get; }
    IGameHistoryService GameHistoryService { get; }
    IGameModeBase? CurrentGame { get; set; }
    ITowerManagerService TowerManagerService { get; }
    Dictionary<TeamColor, TeamBase> Teams { get; }
    TeamColor TeamInWald { get; set; }
    TeamColor TeamInStadt { get; set; }
    bool WaldSpawnReady { get; set; }
    bool StadtSpawnReady { get; set; }
    bool TeamSetReady { get; set; }
    bool AutoStartAfterReady { get; set; }
    int SecondsTillAutoStartAfterReady { get; set; }
    DateTime? AutoStartProcessStarted { get; set; }
    int? AutoStartSecondsRemaining { get; }
    bool AutoStartCountdownActive { get; }
    bool AutoStartWaitingForSpawns { get; }
    CancellationTokenSource AutoStartCancellationTokenSrc { get; set; }
    void StartGame();
    void StopGame();
    void HandleGameEnd();
    void Reset();
    void AutoStartGame();
}
