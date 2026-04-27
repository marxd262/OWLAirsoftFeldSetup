// OWLServer/Models/GameModes/GameModeChainBreak.cs
using System.ComponentModel.DataAnnotations.Schema;
using OWLServer.Models;
using OWLServer.Services;

namespace OWLServer.Models.GameModes;

public class GameModeChainBreak : IGameModeBase, IDisposable
{
    private ExternalTriggerService ExternalTriggerService { get; }
    private GameStateService GameStateService { get; }

    public string Name { get; set; } = "ChainBreak";
    public GameMode GameMode => GameMode.ChainBreak;
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxTickets { get; set; } = 15;
    public bool IsTicket { get; set; } = true;
    public int PointDistributionFrequencyInSeconds { get; set; } = 5;
    public bool ShowRespawnButton => false;
    public bool IsRunning { get; set; } = false;
    public bool IsFinished { get; set; } = false;
    public DateTime? StartTime { get; set; }

    // ChainBreak-specific config
    public double ChainFactor { get; set; } = 1.0;
    public ChainLayout? ActiveChainLayout { get; set; }

    private CancellationTokenSource _abort = new();
    public Dictionary<TeamColor, int> TeamPoints = new();

    // Runtime chain graph — built at RunGame()
    private Dictionary<string, List<string>> _successors = new();
    private Dictionary<string, List<string>> _predecessors = new();
    private HashSet<string> _chainEntryPoints = new();
    private Dictionary<string, int> _depthMap = new();

    public GameModeChainBreak(ExternalTriggerService externalTriggerService, GameStateService gameStateService)
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
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now;
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
        BuildChainMaps();
        StartTime = DateTime.Now;
        IsRunning = true;
        Task.Run(Runner, _abort.Token);
    }

    private void Runner()
    {
        var lastPointDistributed = DateTime.Now;
        while (true)
        {
            Thread.Sleep(200);

            if (_abort.IsCancellationRequested) { EndGame(); break; }

            if (StartTime?.AddMinutes(GameDurationInMinutes) <= DateTime.Now) { EndGame(); break; }

            ProcessChainBreakStateMachine();

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
        foreach (var teamColor in TeamPoints.Keys)
            TeamPoints[teamColor] += GetChainPoints(teamColor);
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
    }

    public override string ToString() => Name;

    public void Dispose()
    {
        StartTime = null;
        _abort.Dispose();
    }

    // -------------------------------------------------------------------------
    // Chain graph — implemented in Task 4
    // -------------------------------------------------------------------------

    private void BuildChainMaps() { }
    private void ProcessChainBreakStateMachine() { }
    private int GetChainPoints(TeamColor team) => 0;
}
