// OWLServer/Models/GameModes/GameModeChainBreak.cs
using System.ComponentModel.DataAnnotations.Schema;
using OWLServer.Models;
using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeChainBreak : IGameModeBase, IDisposable
{
    private IExternalTriggerService ExternalTriggerService { get; }
    private IGameStateService GameStateService { get; }

    public string Name { get; set; } = "ChainBreak";
    public GameMode GameMode => GameMode.ChainBreak;
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxTickets { get; set; } = 15;
    public bool IsTicket { get; set; } = true;
    public int PointDistributionFrequencyInSeconds { get; set; } = 5;
    public bool NeutralAtThresholdEnabled { get; set; } = true;
    public int CaptureNeutralThresholdPercent { get; set; } = 50;
    public bool ShowRespawnButton => false;
    public bool IsPaused { get; set; }
    public TimeSpan PausedDuration { get; set; }
    public DateTime? PauseStartedAt { get; set; }
    public bool IsRunning { get; set; } = false;
    public bool IsFinished { get; set; } = false;
    public DateTime? StartTime { get; set; }

    // ChainBreak-specific config
    public double ChainFactor { get; set; } = 1.0;
    public ChainLayout? ActiveChainLayout { get; set; }

    private CancellationTokenSource _abort = new();
    public Dictionary<TeamColor, int> TeamPoints = new();

    public ChainGraphEngine? Engine { get; private set; }

    public GameModeChainBreak(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;
    }

    public void FillTeams(List<TeamBase> teams)
    {
        foreach (var team in teams)
            TeamPoints[team.TeamColor] = 0;
    }

    [NotMapped]
    public TimeSpan? GetTimer
    {
        get
        {
            if (StartTime == null)
                return new TimeSpan(0, GameDurationInMinutes, 0);
            if (IsFinished)
                return new TimeSpan(0, 0, 0);
            if (IsPaused && PauseStartedAt != null)
                return StartTime.Value.AddMinutes(GameDurationInMinutes) - PauseStartedAt.Value + PausedDuration;
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now + PausedDuration;
        }
    }

    public int GetDisplayPoints(TeamColor color)
    {
        if (IsTicket)
        {
            if (color == TeamColor.BLUE) return MaxTickets - TeamPoints[TeamColor.RED];
            if (color == TeamColor.RED)  return MaxTickets - TeamPoints[TeamColor.BLUE];
        }
        return TeamPoints[color];
    }

    [NotMapped]
    public TeamColor GetWinner
    {
        get
        {
            if (TeamPoints.Values.Distinct().Count() == 1) return TeamColor.NONE;
            return TeamPoints.First(e => e.Value == TeamPoints.Values.Max()).Key;
        }
    }

    public int GetTeamPoints(TeamColor team) => TeamPoints[team];

    public void RunGame()
    {
        Engine = new ChainGraphEngine(ActiveChainLayout, GameStateService.TowerManagerService.Towers);
        Engine.NeutralAtThresholdEnabled = NeutralAtThresholdEnabled;
        Engine.CaptureNeutralThresholdPercent = CaptureNeutralThresholdPercent;
        InitializeTowerStates();
        StartTime = DateTime.Now;
        IsRunning = true;
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
        Task.Run(Runner, _abort.Token);
    }

    private void InitializeTowerStates()
    {
        if (Engine == null) return;
        foreach (var tower in GameStateService.TowerManagerService.Towers.Values)
        {
            bool inChain = Engine.EntryPoints.Contains(tower.MacAddress)
                           || Engine.Predecessors.ContainsKey(tower.MacAddress)
                           || Engine.Successors.ContainsKey(tower.MacAddress);
            if (inChain && !Engine.EntryPoints.Contains(tower.MacAddress))
                tower.SetTowerColor(TeamColor.LOCKED);
            else
                tower.SetTowerColor(TeamColor.NONE);
        }
    }

    private void Runner()
    {
        var lastPointDistributed = DateTime.Now;
        while (true)
        {
            Thread.Sleep(200);

            if (_abort.IsCancellationRequested) { EndGame(); break; }

            if (IsPaused) continue;

            if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now) { EndGame(); break; }

            Engine?.ProcessTick();
            ExternalTriggerService.StateHasChangedAction?.Invoke();

            if (lastPointDistributed.AddSeconds(PointDistributionFrequencyInSeconds) <= DateTime.Now)
            {
                DistributePoints();
                lastPointDistributed = DateTime.Now;
            }

            if (TeamPoints.Any(e => e.Value >= MaxTickets)) { EndGame(); break; }
        }
    }

    private void DistributePoints()
    {
        if (Engine == null) return;
        foreach (var teamColor in TeamPoints.Keys)
            TeamPoints[teamColor] += Engine.GetChainPoints(teamColor, ChainFactor);
    }

    public void EndGame()
    {
        if (IsFinished) return;
        _abort.Cancel();
        IsRunning = false;
        IsFinished = true;
        StartTime = null;
        GameStateService.HandleGameEnd();
    }

    public void ResetGame()
    {
        if (IsRunning) EndGame();
        IsFinished = false;
        StartTime = null;
        foreach (var key in TeamPoints.Keys.ToList())
            TeamPoints[key] = 0;
        _abort.Dispose();
        _abort = new CancellationTokenSource();
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
        Engine = null;
    }

    public override string ToString() => Name;

    public void Dispose()
    {
        StartTime = null;
        _abort.Dispose();
    }

    public (string color, bool arrowA, bool arrowB, bool animated, bool bothWays) GetLinkVisualState(ChainLink link)
    {
        return Engine?.GetLinkVisualState(link)
               ?? ("#BBBBBB", false, false, false, false);
    }
}
