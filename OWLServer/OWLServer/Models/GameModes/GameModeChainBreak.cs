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

    private void BuildChainMaps()
    {
        _successors = new Dictionary<string, List<string>>();
        _predecessors = new Dictionary<string, List<string>>();
        _chainEntryPoints = new HashSet<string>();
        _depthMap = new Dictionary<string, int>();

        if (ActiveChainLayout == null) return;

        foreach (var link in ActiveChainLayout.Links)
        {
            AddEdge(link.FromTowerMacAddress, link.ToTowerMacAddress);
            if (link.IsBidirectional)
                AddEdge(link.ToTowerMacAddress, link.FromTowerMacAddress);
        }

        var uniTo = ActiveChainLayout.Links
            .Where(l => !l.IsBidirectional)
            .Select(l => l.ToTowerMacAddress)
            .ToHashSet();
        var uniFrom = ActiveChainLayout.Links
            .Where(l => !l.IsBidirectional)
            .Select(l => l.FromTowerMacAddress)
            .ToHashSet();
        foreach (var mac in uniFrom.Except(uniTo))
            _chainEntryPoints.Add(mac);

        var biDegree = new Dictionary<string, int>();
        foreach (var link in ActiveChainLayout.Links.Where(l => l.IsBidirectional))
        {
            biDegree[link.FromTowerMacAddress] = biDegree.GetValueOrDefault(link.FromTowerMacAddress) + 1;
            biDegree[link.ToTowerMacAddress]   = biDegree.GetValueOrDefault(link.ToTowerMacAddress)   + 1;
        }
        foreach (var (mac, degree) in biDegree)
        {
            if (degree == 1 && !uniTo.Contains(mac))
                _chainEntryPoints.Add(mac);
        }

        var queue = new Queue<string>();
        foreach (var ep in _chainEntryPoints)
        {
            _depthMap[ep] = 0;
            queue.Enqueue(ep);
        }
        while (queue.Count > 0)
        {
            var mac = queue.Dequeue();
            if (!_successors.TryGetValue(mac, out var succs)) continue;
            foreach (var succ in succs)
            {
                if (_depthMap.ContainsKey(succ)) continue;
                _depthMap[succ] = _depthMap[mac] + 1;
                queue.Enqueue(succ);
            }
        }
    }

    private void AddEdge(string from, string to)
    {
        if (!_successors.ContainsKey(from)) _successors[from] = new List<string>();
        _successors[from].Add(to);

        if (!_predecessors.ContainsKey(to)) _predecessors[to] = new List<string>();
        _predecessors[to].Add(from);
    }

    private bool CanPress(string mac, TeamColor team)
    {
        var towers = GameStateService.TowerManagerService.Towers;
        if (!towers.ContainsKey(mac)) return false;
        var tower = towers[mac];

        if (tower.CurrentColor == team) return false;

        bool inLayout = _predecessors.ContainsKey(mac) || _successors.ContainsKey(mac)
                        || _chainEntryPoints.Contains(mac);
        if (!inLayout) return true;

        if (_chainEntryPoints.Contains(mac)) return true;

        if (tower.CurrentColor != TeamColor.NONE
            && tower.CurrentColor != TeamColor.LOCKED
            && tower.CurrentColor != team)
            return true;

        if (_predecessors.TryGetValue(mac, out var preds))
            return preds.Any(p => towers.TryGetValue(p, out var pt) && pt.CurrentColor == team);

        return false;
    }

    private void LockDescendants(string mac, TeamColor previousOwner, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        if (!visited.Add(mac)) return;

        if (!_successors.TryGetValue(mac, out var succs)) return;
        var towers = GameStateService.TowerManagerService.Towers;

        foreach (var succMac in succs)
        {
            if (!towers.TryGetValue(succMac, out var succTower)) continue;
            if (_chainEntryPoints.Contains(succMac)) continue;

            bool ownedByPrev = succTower.CurrentColor == previousOwner;
            bool neutral      = succTower.CurrentColor == TeamColor.NONE;
            if (!ownedByPrev && !neutral) continue;

            succTower.SetTowerColor(TeamColor.LOCKED);
            LockDescendants(succMac, previousOwner, visited);
        }
    }

    private void UnlockSuccessors(string mac, TeamColor capturingTeam)
    {
        if (!_successors.TryGetValue(mac, out var succs)) return;
        var towers = GameStateService.TowerManagerService.Towers;

        foreach (var succMac in succs)
        {
            if (!towers.TryGetValue(succMac, out var succTower)) continue;
            if (succTower.CurrentColor != TeamColor.LOCKED) continue;

            bool prereqMet = _chainEntryPoints.Contains(succMac);
            if (!prereqMet && _predecessors.TryGetValue(succMac, out var preds))
                prereqMet = preds.Any(p => towers.TryGetValue(p, out var pt) && pt.CurrentColor == capturingTeam);

            if (prereqMet)
                succTower.SetTowerColor(TeamColor.NONE);
        }
    }

    private void ProcessChainBreakStateMachine()
    {
        var towers = GameStateService.TowerManagerService.Towers;

        foreach (var tower in towers.Values.Where(t => t.IsPressed).ToList())
        {
            var mac = tower.MacAddress;
            var pressingTeam = tower.PressedByColor;

            if (!CanPress(mac, pressingTeam))
            {
                tower.IsPressed = false;
                tower.LastPressed = null;
                tower.PressedByColor = TeamColor.NONE;
                tower.CaptureProgress = 0;
                continue;
            }

            if (tower.LastPressed?.AddSeconds(tower.TimeToCaptureInSeconds) < DateTime.Now)
            {
                CompleteCaptureChain(tower, pressingTeam);
            }
            else
            {
                var elapsed = DateTime.Now - tower.LastPressed;
                tower.CaptureProgress = elapsed?.TotalSeconds / tower.TimeToCaptureInSeconds ?? 0;
            }
        }

        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }

    private void CompleteCaptureChain(Tower tower, TeamColor capturingTeam)
    {
        var previousOwner = tower.CurrentColor;

        tower.IsPressed = false;
        tower.LastPressed = null;
        tower.PressedByColor = TeamColor.NONE;
        tower.CaptureProgress = 1;
        tower.CapturedAt = DateTime.Now;

        if (previousOwner != TeamColor.NONE && previousOwner != TeamColor.LOCKED)
            LockDescendants(tower.MacAddress, previousOwner);

        bool hasPrereqs = _chainEntryPoints.Contains(tower.MacAddress)
                          || !_predecessors.TryGetValue(tower.MacAddress, out var preds)
                          || preds.Any(p =>
                                 GameStateService.TowerManagerService.Towers.TryGetValue(p, out var pt)
                                 && pt.CurrentColor == capturingTeam);

        if (hasPrereqs)
        {
            tower.SetTowerColor(capturingTeam);
            UnlockSuccessors(tower.MacAddress, capturingTeam);
        }
        else
        {
            tower.SetTowerColor(TeamColor.NONE);
            LockDescendants(tower.MacAddress, TeamColor.NONE);
        }
    }

    private int GetChainPoints(TeamColor team)
    {
        double points = 0;
        foreach (var tower in GameStateService.TowerManagerService.Towers.Values
                     .Where(t => t.CurrentColor == team))
        {
            int depth = _depthMap.GetValueOrDefault(tower.MacAddress, 0);
            points += Math.Pow(ChainFactor, depth) * tower.Multiplier;
        }
        return (int)Math.Round(points);
    }
}
