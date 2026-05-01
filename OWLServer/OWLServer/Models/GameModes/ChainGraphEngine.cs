// OWLServer/Models/GameModes/ChainGraphEngine.cs
namespace OWLServer.Models.GameModes;

public class TowerCaptureUpdate
{
    public Tower Tower { get; set; } = null!;
    public bool CaptureCompleted { get; set; }
    public double CaptureProgress { get; set; }
}

public class ChainGraphEngine
{
    private readonly Dictionary<string, Tower> _towers;
    private readonly Dictionary<string, List<string>> _successors = new();
    private readonly Dictionary<string, List<string>> _predecessors = new();
    private readonly HashSet<string> _chainEntryPoints = new();
    private readonly Dictionary<string, int> _depthMap = new();

    public IReadOnlyDictionary<string, List<string>> Successors => _successors;
    public IReadOnlyDictionary<string, List<string>> Predecessors => _predecessors;
    public IReadOnlySet<string> EntryPoints => _chainEntryPoints;
    public IReadOnlyDictionary<string, int> DepthMap => _depthMap;

    public bool NeutralAtThresholdEnabled { get; set; } = true;
    public int CaptureNeutralThresholdPercent { get; set; } = 50;

    public ChainGraphEngine(ChainLayout? layout, Dictionary<string, Tower> towers)
    {
        _towers = towers;
        BuildChainMaps(layout);
    }

    private void BuildChainMaps(ChainLayout? layout)
    {
        _successors.Clear();
        _predecessors.Clear();
        _chainEntryPoints.Clear();
        _depthMap.Clear();

        if (layout == null) return;

        foreach (var link in layout.Links)
        {
            AddEdge(link.TowerAMacAddress, link.TowerBMacAddress);
            if (link.EntryAtBothEnds)
                AddEdge(link.TowerBMacAddress, link.TowerAMacAddress);
        }

        var oneWayB = layout.Links
            .Where(l => !l.EntryAtBothEnds)
            .Select(l => l.TowerBMacAddress)
            .ToHashSet();
        var oneWayA = layout.Links
            .Where(l => !l.EntryAtBothEnds)
            .Select(l => l.TowerAMacAddress)
            .ToHashSet();
        foreach (var mac in oneWayA.Except(oneWayB))
            _chainEntryPoints.Add(mac);

        var twoWayDegree = new Dictionary<string, int>();
        foreach (var link in layout.Links.Where(l => l.EntryAtBothEnds))
        {
            twoWayDegree[link.TowerAMacAddress] = twoWayDegree.GetValueOrDefault(link.TowerAMacAddress) + 1;
            twoWayDegree[link.TowerBMacAddress] = twoWayDegree.GetValueOrDefault(link.TowerBMacAddress) + 1;
        }
        foreach (var (mac, degree) in twoWayDegree)
        {
            if (degree == 1 && !oneWayB.Contains(mac))
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

    public bool CanPress(string mac, TeamColor team)
    {
        if (!_towers.ContainsKey(mac)) return false;
        var tower = _towers[mac];

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
            return preds.Any(p => _towers.TryGetValue(p, out var pt) && pt.CurrentColor == team);

        return false;
    }

    public void CompleteCapture(string mac, TeamColor capturingTeam)
    {
        if (!_towers.TryGetValue(mac, out var tower)) return;

        var previousOwner = tower.CurrentColor;

        tower.IsPressed = false;
        tower.LastPressed = null;
        tower.PressedByColor = TeamColor.NONE;
        tower.CaptureProgress = 1;
        tower.CapturedAt = DateTime.Now;

        bool hasPrereqs = _chainEntryPoints.Contains(mac)
                          || !_predecessors.TryGetValue(mac, out var preds)
                          || preds.Any(p =>
                                 _towers.TryGetValue(p, out var pt)
                                 && pt.CurrentColor == capturingTeam);

        tower.SetTowerColor(hasPrereqs ? capturingTeam : TeamColor.NONE);

        RecalculateChainState(previousOwner);
    }

    private void RecalculateChainState(TeamColor previousOwner)
    {
        if (previousOwner != TeamColor.RED && previousOwner != TeamColor.BLUE)
        {
            RecalculateUnlocksAndLocks();
            return;
        }

        var teamEntries = new List<string>();
        foreach (var ep in _chainEntryPoints)
        {
            if (!_towers.TryGetValue(ep, out var t)) continue;
            if (t.CurrentColor == previousOwner)
                teamEntries.Add(ep);
        }

        var reachable = new HashSet<string>();

        if (teamEntries.Count > 0)
        {
            var queue = new Queue<string>();
            foreach (var ep in teamEntries)
            {
                reachable.Add(ep);
                queue.Enqueue(ep);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                var neighbors = new HashSet<string>();
                if (_successors.TryGetValue(current, out var succs))
                    foreach (var n in succs) neighbors.Add(n);
                if (_predecessors.TryGetValue(current, out var preds))
                    foreach (var p in preds)
                        if (_successors.TryGetValue(current, out var curSuccs) && curSuccs.Contains(p))
                            neighbors.Add(p);

                foreach (var neighbor in neighbors)
                {
                    if (reachable.Contains(neighbor)) continue;
                    if (!_towers.TryGetValue(neighbor, out var nt)) continue;
                    if (nt.CurrentColor != previousOwner) continue;
                    reachable.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        foreach (var (mac, tower) in _towers)
        {
            if (tower.CurrentColor != previousOwner) continue;
            if (!reachable.Contains(mac))
                tower.SetTowerColor(TeamColor.NONE);
        }

        RecalculateUnlocksAndLocks();
    }

    private void RecalculateUnlocksAndLocks()
    {

        foreach (var (mac, tower) in _towers)
        {
            if (tower.CurrentColor != TeamColor.LOCKED) continue;

            bool hasCapturablePred = false;
            if (_predecessors.TryGetValue(mac, out var preds))
                hasCapturablePred = preds.Any(p => _towers.TryGetValue(p, out var pt) &&
                    (pt.CurrentColor == TeamColor.RED || pt.CurrentColor == TeamColor.BLUE));
            if (hasCapturablePred || _chainEntryPoints.Contains(mac))
                tower.SetTowerColor(TeamColor.NONE);
        }

        var chainMacs = GetAllChainMacs();
        foreach (var mac in chainMacs)
        {
            if (!_towers.TryGetValue(mac, out var t)) continue;
            if (t.CurrentColor != TeamColor.NONE) continue;
            if (_chainEntryPoints.Contains(mac)) continue;
            if (!CanReachColoredTower(mac))
                t.SetTowerColor(TeamColor.LOCKED);
        }
    }

    private HashSet<string> GetAllChainMacs()
    {
        var all = new HashSet<string>(_chainEntryPoints);
        foreach (var mac in _successors.Keys) all.Add(mac);
        foreach (var mac in _predecessors.Keys) all.Add(mac);
        return all;
    }

    private bool CanReachColoredTower(string startMac)
    {
        if (_predecessors.TryGetValue(startMac, out var preds))
        {
            if (preds.Any(p => _towers.TryGetValue(p, out var pt) &&
                (pt.CurrentColor == TeamColor.RED || pt.CurrentColor == TeamColor.BLUE)))
                return true;
        }

        return false;
    }

    public List<TowerCaptureUpdate> ProcessTick()
    {
        var updates = new List<TowerCaptureUpdate>();

        foreach (var tower in _towers.Values.Where(t => t.IsPressed).ToList())
        {
            var mac = tower.MacAddress;
            var pressingTeam = tower.PressedByColor;

            if (!CanPress(mac, pressingTeam))
            {
                tower.IsPressed = false;
                tower.LastPressed = null;
                tower.PressedByColor = TeamColor.NONE;
                tower.CaptureProgress = 0;
                updates.Add(new TowerCaptureUpdate { Tower = tower, CaptureCompleted = false, CaptureProgress = 0 });
                continue;
            }

            if (tower.LastPressed?.AddSeconds(tower.TimeToCaptureInSeconds) < DateTime.Now)
            {
                CompleteCapture(mac, pressingTeam);
                updates.Add(new TowerCaptureUpdate { Tower = tower, CaptureCompleted = true, CaptureProgress = 1 });
            }
            else
            {
                var elapsed = DateTime.Now - tower.LastPressed;
                tower.CaptureProgress = elapsed?.TotalSeconds / tower.TimeToCaptureInSeconds ?? 0;

                if (NeutralAtThresholdEnabled
                    && tower.CaptureProgress * 100 >= CaptureNeutralThresholdPercent
                    && tower.CurrentColor != TeamColor.NONE
                    && tower.CurrentColor != pressingTeam)
                {
                    var previousOwner = tower.CurrentColor;
                    tower.SetTowerColor(TeamColor.NONE);
                    RecalculateChainState(previousOwner);
                }

                updates.Add(new TowerCaptureUpdate { Tower = tower, CaptureCompleted = false, CaptureProgress = tower.CaptureProgress });
            }
        }

        return updates;
    }

    public int GetChainPoints(TeamColor team, double chainFactor)
    {
        double points = 0;
        foreach (var tower in _towers.Values.Where(t => t.CurrentColor == team))
        {
            int depth = _depthMap.GetValueOrDefault(tower.MacAddress, 0);
            points += Math.Pow(chainFactor, depth) * tower.Multiplier;
        }
        return (int)Math.Round(points);
    }

    /// <summary>
    /// Computes the visualization state for a single chain link, used by Map.razor.
    /// Returns (colorHex, showArrowAtA, showArrowAtB, isAnimated, isAnimatedBothWays).
    /// </summary>
    public (string color, bool arrowA, bool arrowB, bool animated, bool bothWays) GetLinkVisualState(ChainLink link)
    {
        if (!_towers.TryGetValue(link.TowerAMacAddress, out var towerA) ||
            !_towers.TryGetValue(link.TowerBMacAddress, out var towerB))
            return ("#BBBBBB", false, false, false, false);

        var colorA = EffectiveCaptureColor(towerA);
        var colorB = EffectiveCaptureColor(towerB);

        bool isLocked = colorA == TeamColor.LOCKED || colorB == TeamColor.LOCKED;
        if (isLocked)
            return ("#FFD700", false, false, false, false);

        bool aIsTeam = colorA != TeamColor.NONE && colorA != TeamColor.LOCKED && colorA != TeamColor.OFF;
        bool bIsTeam = colorB != TeamColor.NONE && colorB != TeamColor.LOCKED && colorB != TeamColor.OFF;

        if (aIsTeam && bIsTeam && colorA != colorB)
            return ("#FFFFFF", true, true, true, true);

        if (aIsTeam && bIsTeam && colorA == colorB)
            return (TeamColorToHex(colorA), false, false, false, false);

        bool canCaptureAtoB = aIsTeam && colorB == TeamColor.NONE;
        bool canCaptureBtoA = bIsTeam && colorA == TeamColor.NONE;

        if (link.EntryAtBothEnds)
        {
            if (canCaptureAtoB && canCaptureBtoA)
                return ("#FFFFFF", true, true, true, true);
            if (canCaptureAtoB)
                return (EffectiveTeamColorHex(towerA), false, true, true, false);
            if (canCaptureBtoA)
                return (EffectiveTeamColorHex(towerB), true, false, true, false);
        }
        else
        {
            if (canCaptureAtoB)
                return (EffectiveTeamColorHex(towerA), false, true, true, false);
            if (bIsTeam)
                return (EffectiveTeamColorHex(towerB), false, false, false, false);
        }

        if (!aIsTeam && !bIsTeam)
            return ("#BBBBBB", false, false, false, false);

        return ("#777777", false, false, false, false);
    }

    private static TeamColor EffectiveCaptureColor(Tower tower) => tower.CurrentColor;

    private static string EffectiveTeamColorHex(Tower tower) => EffectiveCaptureColor(tower) switch
    {
        TeamColor.RED => "#fc1911",
        TeamColor.BLUE => "#00b4f1",
        _ => "#FFFFFF"
    };

    private static string TeamColorToHex(TeamColor color) => color switch
    {
        TeamColor.RED => "#fc1911",
        TeamColor.BLUE => "#00b4f1",
        _ => "#FFFFFF"
    };
}
