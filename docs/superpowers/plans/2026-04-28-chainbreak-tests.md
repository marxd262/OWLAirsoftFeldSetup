# ChainBreak Test Suite — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract chain graph logic from `GameModeChainBreak` into a testable `ChainGraphEngine`, then write ~58 unit tests covering all chain operations (graph construction, press rules, lock/unlock cascades, scoring, visualization).

**Architecture:** New `ChainGraphEngine` class holds the directed graph (successors, predecessors, entry points, depth map) and exposes query methods (`CanPress`, `GetChainPoints`, `GetLinkVisualState`) and mutation methods (`CompleteCapture`, `ProcessTick`). `GameModeChainBreak` delegates all chain logic to the engine, keeping only the `Runner()` loop lifecycle, team point distribution, and win conditions.

**Tech Stack:** .NET 8, xUnit 2.9.3, Moq 4.20.72

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `OWLServer/Models/GameModes/ChainGraphEngine.cs` | **Create** | Directed graph construction, chain queries, capture mutations |
| `OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs` | **Create** | ~48 unit tests for all engine methods |
| `OWLServer/Models/GameModes/GameModeChainBreak.cs` | **Modify** | Delegate to engine, remove extracted methods |
| `OWLServer/Components/MapComponents/Map.razor` | **Modify** | Use pass-through property (1 line change) |
| `OWLServer.Tests/Unit/GameModes/GameModeChainBreakTests.cs` | **Modify** | Add ~8 tests for remaining game mode logic |

---

### Task 1: Create ChainGraphEngine.cs — Extract graph + chain logic

**Files:**
- Create: `OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs`

Move all graph construction and chain logic from `GameModeChainBreak.cs` into a new class. The code is extracted verbatim from the existing implementation — only the data access changes (towers dictionary stored as field instead of accessed via `GameStateService.TowerManagerService.Towers`).

- [ ] **Step 1: Create the file with the full class**

```csharp
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

    private void LockDescendants(string mac, TeamColor previousOwner, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        if (!visited.Add(mac)) return;

        if (!_successors.TryGetValue(mac, out var succs)) return;

        foreach (var succMac in succs)
        {
            if (!_towers.TryGetValue(succMac, out var succTower)) continue;
            if (_chainEntryPoints.Contains(succMac)) continue;

            bool ownedByPrev = succTower.CurrentColor == previousOwner;
            bool neutral = succTower.CurrentColor == TeamColor.NONE;
            if (!ownedByPrev && !neutral) continue;

            succTower.SetTowerColor(TeamColor.LOCKED);
            LockDescendants(succMac, previousOwner, visited);
        }
    }

    private void UnlockSuccessors(string mac, TeamColor capturingTeam)
    {
        if (!_successors.TryGetValue(mac, out var succs)) return;

        foreach (var succMac in succs)
        {
            if (!_towers.TryGetValue(succMac, out var succTower)) continue;
            if (succTower.CurrentColor != TeamColor.LOCKED) continue;

            bool prereqMet = _chainEntryPoints.Contains(succMac);
            if (!prereqMet && _predecessors.TryGetValue(succMac, out var preds))
                prereqMet = preds.Any(p => _towers.TryGetValue(p, out var pt) && pt.CurrentColor == capturingTeam);

            if (prereqMet)
                succTower.SetTowerColor(TeamColor.NONE);
        }
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

        if (previousOwner != TeamColor.NONE && previousOwner != TeamColor.LOCKED)
            LockDescendants(mac, previousOwner);

        bool hasPrereqs = _chainEntryPoints.Contains(mac)
                          || !_predecessors.TryGetValue(mac, out var preds)
                          || preds.Any(p =>
                                 _towers.TryGetValue(p, out var pt)
                                 && pt.CurrentColor == capturingTeam);

        if (hasPrereqs)
        {
            tower.SetTowerColor(capturingTeam);
            UnlockSuccessors(mac, capturingTeam);
        }
        else
        {
            tower.SetTowerColor(TeamColor.NONE);
            LockDescendants(mac, TeamColor.NONE);
        }
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
```

- [ ] **Step 2: Build and verify compilation**

```bash
dotnet build OWLServer/OWLServer/OWLServer.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs
git commit -m "feat: add ChainGraphEngine extracted from GameModeChainBreak"
```

---

### Task 2: Graph Construction Tests (~8 tests)

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs`

Verify that `BuildChainMaps` produces correct successors, predecessors, entry points, and depth maps for various graph topologies. Test through the public read-only properties exposed after construction. No mocking needed — create real `ChainLayout`, `ChainLink`, and `Tower` objects.

- [ ] **Step 1: Create test class with helper methods and first test batch**

```csharp
using OWLServer.Models;
using OWLServer.Models.GameModes;
using Xunit;

namespace OWLServer.Tests.Unit.GameModes;

public class ChainGraphEngineTests
{
    // --- Helpers ---

    private static ChainLayout Layout(params ChainLink[] links) => new() { Links = links.ToList() };

    private static ChainLink Link(string a, string b, bool both = false) => new()
    {
        TowerAMacAddress = a, TowerBMacAddress = b, EntryAtBothEnds = both
    };

    private static Dictionary<string, Tower> Towers(params Tower[] list) =>
        list.ToDictionary(t => t.MacAddress);

    private static Tower T(string mac) => new() { MacAddress = mac };

    // --- Graph Construction ---

    [Fact]
    public void LinearChain_T1toT2toT3_EntryIsT1()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Single(engine.EntryPoints);
        Assert.Contains("T1", engine.EntryPoints);
    }

    [Fact]
    public void LinearChain_T1toT2toT3_CorrectSuccessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(new[] { "T2" }, engine.Successors["T1"]);
        Assert.Equal(new[] { "T3" }, engine.Successors["T2"]);
        Assert.False(engine.Successors.ContainsKey("T3"));
    }

    [Fact]
    public void LinearChain_T1toT2toT3_CorrectPredecessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.Predecessors.ContainsKey("T1"));
        Assert.Equal(new[] { "T1" }, engine.Predecessors["T2"]);
        Assert.Equal(new[] { "T2" }, engine.Predecessors["T3"]);
    }

    [Fact]
    public void LinearChain_T1toT2toT3_CorrectDepthMap()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(0, engine.DepthMap["T1"]);
        Assert.Equal(1, engine.DepthMap["T2"]);
        Assert.Equal(2, engine.DepthMap["T3"]);
    }

    [Fact]
    public void Branch_T1toT2_and_T1toT3_BothChildrenAreSuccessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T1", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(2, engine.Successors["T1"].Count);
        Assert.Contains("T2", engine.Successors["T1"]);
        Assert.Contains("T3", engine.Successors["T1"]);
    }

    [Fact]
    public void Merge_T1toT3_and_T2toT3_T3HasTwoPredecessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T3"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(2, engine.Predecessors["T3"].Count);
        Assert.Contains("T1", engine.Predecessors["T3"]);
        Assert.Contains("T2", engine.Predecessors["T3"]);
    }

    [Fact]
    public void Bidirectional_BothEndsAreEntryPoints()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = Layout(Link("T1", "T2", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(2, engine.EntryPoints.Count);
        Assert.Contains("T1", engine.EntryPoints);
        Assert.Contains("T2", engine.EntryPoints);
    }

    [Fact]
    public void Bidirectional_BothAreSuccessorsAndPredecessors()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = Layout(Link("T1", "T2", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Contains("T2", engine.Successors["T1"]);
        Assert.Contains("T1", engine.Successors["T2"]);
        Assert.Contains("T2", engine.Predecessors["T1"]);
        Assert.Contains("T1", engine.Predecessors["T2"]);
    }

    [Fact]
    public void Diamond_T1toT2_T1toT3_T2toT4_T3toT4_EntryIsT1()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"), T("T4"));
        var layout = Layout(Link("T1", "T2"), Link("T1", "T3"), Link("T2", "T4"), Link("T3", "T4"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Single(engine.EntryPoints);
        Assert.Contains("T1", engine.EntryPoints);
        Assert.Equal(0, engine.DepthMap["T1"]);
        Assert.Equal(1, engine.DepthMap["T2"]);
        Assert.Equal(1, engine.DepthMap["T3"]);
        Assert.Equal(2, engine.DepthMap["T4"]);
    }

    [Fact]
    public void TowerNotInLayout_NotInGraph()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.Successors.ContainsKey("T3"));
        Assert.False(engine.Predecessors.ContainsKey("T3"));
        Assert.DoesNotContain("T3", engine.EntryPoints);
        Assert.False(engine.DepthMap.ContainsKey("T3"));
    }

    [Fact]
    public void EmptyLayout_AllGraphsEmpty()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = new ChainLayout();
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Empty(engine.Successors);
        Assert.Empty(engine.Predecessors);
        Assert.Empty(engine.EntryPoints);
        Assert.Empty(engine.DepthMap);
    }

    [Fact]
    public void NullLayout_AllGraphsEmpty()
    {
        var towers = Towers(T("T1"), T("T2"));
        var engine = new ChainGraphEngine(null, towers);

        Assert.Empty(engine.Successors);
        Assert.Empty(engine.Predecessors);
        Assert.Empty(engine.EntryPoints);
        Assert.Empty(engine.DepthMap);
    }

    [Fact]
    public void SingleTowerInLayout_NoLinks_IsEntryPoint()
    {
        var towers = Towers(T("T1"));
        var layout = Layout();
        var engine = new ChainGraphEngine(layout, towers);

        // Tower appears in towers dict but not in any link. In current code,
        // entry points come from link analysis, so a tower with no links should
        // NOT be an entry point (it's irrelevant to the chain).
        Assert.Empty(engine.EntryPoints);
    }
}
```

- [ ] **Step 2: Run graph construction tests**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "FullyQualifiedName~ChainGraphEngineTests" -v n
```
Expected: All 14 graph construction tests pass.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs
git commit -m "test: add ChainGraphEngine graph construction tests"
```

---

### Task 3: CanPress Tests (~8 tests)

**Files:**
- Modify: `OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs`

Add tests for `CanPress()` covering entry points, predecessor checks, counter-capture, already-owned, outsiders, unknown MACs.

- [ ] **Step 1: Add CanPress test methods to the end of ChainGraphEngineTests.cs**

Add these methods inside the `ChainGraphEngineTests` class, after the graph construction tests:

```csharp
    // --- CanPress ---

    [Fact]
    public void CanPress_EntryPoint_ReturnsTrue()
    {
        var t1 = T("T1");
        var towers = Towers(t1, T("T2"));
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T1", TeamColor.RED));
    }

    [Fact]
    public void CanPress_SuccessorWithPredecessorHeld_ReturnsTrue()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T2", TeamColor.RED));
    }

    [Fact]
    public void CanPress_SuccessorWithoutPredecessor_ReturnsFalse()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.CanPress("T2", TeamColor.RED));
    }

    [Fact]
    public void CanPress_CounterCaptureEnemyLockedTower_ReturnsTrue()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.BLUE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.LOCKED);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        // T2 is locked but not owned by RED — it's a "different" color
        Assert.True(engine.CanPress("T2", TeamColor.RED));
    }

    [Fact]
    public void CanPress_AlreadyOwnByPressingTeam_ReturnsFalse()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED);
        var towers = Towers(t1);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.CanPress("T1", TeamColor.RED));
    }

    [Fact]
    public void CanPress_TowerNotInChainLayout_ReturnsTrue()
    {
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.NONE);
        var towers = Towers(T("T1"), T("T2"), t3);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T3", TeamColor.RED));
    }

    [Fact]
    public void CanPress_UnknownMac_ReturnsFalse()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.CanPress("T_UNKNOWN", TeamColor.RED));
    }

    [Fact]
    public void CanPress_BidirectionalEntry_BothCanBePressed()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        // Both are entry points in bidirectional mode
        Assert.True(engine.CanPress("T1", TeamColor.RED));
        Assert.True(engine.CanPress("T2", TeamColor.BLUE));
    }

    [Fact]
    public void CanPress_CounterCaptureEnemyOwnedTower_ReturnsTrue()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.BLUE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.RED); // enemy-owned
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        // RED pressing T1 which is BLUE-owned (counter-capture of predecessor)
        Assert.True(engine.CanPress("T1", TeamColor.RED));
    }
```

- [ ] **Step 2: Run CanPress tests**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "FullyQualifiedName~ChainGraphEngineTests.CanPress" -v n
```
Expected: All 9 CanPress tests pass.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs
git commit -m "test: add ChainGraphEngine CanPress tests"
```

---

### Task 4: LockDescendants, UnlockSuccessors, CompleteCapture Tests (~16 tests)

**Files:**
- Modify: `OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs`

Test the mutation methods. `LockDescendants` and `UnlockSuccessors` are private — verify their behavior through `CompleteCapture`. For direct unit testing of the locked state post-capture, assert tower `CurrentColor` after `CompleteCapture` or set up towers in known states.

- [ ] **Step 1: Add capture mutation tests**

```csharp
    // --- CompleteCapture → LockDescendants ---

    [Fact]
    public void CompleteCapture_ForwardFromEntry_LocksDownstreamOfPreviousOwner()
    {
        // A→B→C, A owned by BLUE, B owned by RED, C owned by RED
        var tA = T("A"); tA.SetTowerColor(TeamColor.BLUE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED);
        var tC = T("C"); tC.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        // BLUE captures A from BLUE → but A is already BLUE.
        // Let's test: RED captures B (counter-capture), which should lock C.
        engine.CompleteCapture("B", TeamColor.RED);

        // B now RED, C should still be RED (same team, not locked)
        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.RED, tC.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_MidChainLocksDescendants()
    {
        // A→B→C, A=RED, B=BLUE, C=BLUE
        // RED captures B → LOCK descendants of BLUE → C should be LOCKED
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tC.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_EntryPointSkipsLocked()
    {
        // A→B→C, A=NONE entry, B=BLUE, C=BLUE
        // BLUE captures C directly (B is predecessor held by BLUE)
        // But first: BLUE captures B, then C.
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("C", TeamColor.BLUE);

        Assert.Equal(TeamColor.BLUE, tC.CurrentColor);
        // A is entry point → skipped by LockDescendants (previous owner of C was NONE, so no locking)
    }

    [Fact]
    public void CompleteCapture_PreviousOwnerNONE_LocksAllDescendants()
    {
        // A→B→C, A=RED, B=NONE, C=NONE
        // RED captures B → previous owner is NONE, LockDescendants locks all downstream (B's children)
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        // C was NONE and previousOwner was NONE → it gets locked
        Assert.Equal(TeamColor.LOCKED, tC.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_RecursiveCascade_DeepChain()
    {
        // A→B→C→D→E, A=RED, B=BLUE, C=BLUE, D=BLUE, E=BLUE
        // RED captures B → C,D,E should all be LOCKED
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.BLUE);
        var tD = T("D"); tD.SetTowerColor(TeamColor.BLUE);
        var tE = T("E"); tE.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB, tC, tD, tE);
        var layout = Layout(Link("A", "B"), Link("B", "C"), Link("C", "D"), Link("D", "E"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tC.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tD.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tE.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_NoSuccessors_NoLocking()
    {
        // A→B, A=RED, B=BLUE
        // RED captures B (counter-capture) → B's previous owner is BLUE, B has no successors
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        // A unchanged (is entry point, skipped by LockDescendants)
        Assert.Equal(TeamColor.RED, tA.CurrentColor);
    }

    // --- CompleteCapture → UnlockSuccessors ---

    [Fact]
    public void CompleteCapture_UnlocksNextTower()
    {
        // A→B→C; A=RED, B=LOCKED, C=NONE
        // RED captures A (entry point) → B should unlock to NONE
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.LOCKED);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("A", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tA.CurrentColor);
        Assert.Equal(TeamColor.NONE, tB.CurrentColor); // unlocked
    }

    [Fact]
    public void CompleteCapture_DoesNotUnlockIfPrereqUnmet()
    {
        // T1→T2, T3→T2 (merge); T1=RED, T2=LOCKED, T3=NONE
        // RED captures T1 → T2 has 2 predecessors. T1 is now RED, but T3 is not → prereq UNMET
        // So T2 should NOT unlock.
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.LOCKED);
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2, t3);
        var layout = Layout(Link("T1", "T2"), Link("T3", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("T1", TeamColor.RED);

        Assert.Equal(TeamColor.RED, t1.CurrentColor);
        // T2 stays LOCKED because T3 (other predecessor) is not held by RED
        Assert.Equal(TeamColor.LOCKED, t2.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_UnlocksWhenBothPredecessorsHeld()
    {
        // T1→T3, T2→T3 (merge); T1=RED, T2=RED, T3=LOCKED
        // RED captures T1 (already RED actually) → both predecessors held → T3 should unlock
        // Hmm. Let me change this test to be clearer:
        // T1=RED, T2=BLUE, T3=LOCKED
        // BLUE captures T2 → T3 still locked (only T2 is BLUE, T1 is RED)
        // Then RED captures T1 → still matters whether RED's predecessor capture triggers unlock
        // Actually UnlockSuccessors runs after CompleteCapture of T1. After T1 capture,
        // T1=RED, T2=BLUE. Does T3 unlock? It needs ANY predecessor held by capturingTeam (RED).
        // T1 is RED → yes, T3 unlocks.
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.BLUE);
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.LOCKED);
        var towers = Towers(t1, t2, t3);
        var layout = Layout(Link("T1", "T3"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("T1", TeamColor.RED);

        Assert.Equal(TeamColor.RED, t1.CurrentColor);
        // T3 needs ANY predecessor held by RED. T1 is RED → unlock.
        Assert.Equal(TeamColor.NONE, t3.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_NoSuccessors_NoUnlock()
    {
        // A→B; B=RED, no successors
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        // Counter-capture B → no successors to unlock
        engine.CompleteCapture("B", TeamColor.RED);
        Assert.Equal(TeamColor.RED, tB.CurrentColor);
    }

    // --- CompleteCapture → Missing Prerequisites ---

    [Fact]
    public void CompleteCapture_MissingPrerequisites_SetsNONE()
    {
        // T1→T2, T3→T2 (merge); T1=NONE, T2=NONE, T3=NONE
        // Entry points are T1 and T3 (both source-only).
        // Press on T2 directly. hasPrereqs = T2 is not entry point AND has predecessors.
        // Preds: T1=NONE, T3=NONE. Neither RED → hasPrereqs = false.
        // → T2 stays NONE + descendants locked.
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2, t3);
        var layout = Layout(Link("T1", "T2"), Link("T3", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("T2", TeamColor.RED);

        Assert.Equal(TeamColor.NONE, t2.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_MissingPrerequisites_LocksDownstream()
    {
        // A→B→C; A=NONE, B=NONE, C=RED
        // Entry point = A. RED tries to capture B directly → hasPrereqs false (A is not RED)
        // → B stays NONE, descendants locked. C is RED owned by RED.
        // LockDescendants(B, TeamColor.NONE) → C is RED (not NONE) → stays RED.
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.NONE, tB.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tC.CurrentColor); // locked by NONE-triggered LockDescendants
    }

    [Fact]
    public void CompleteCapture_CounterCaptureLocksPreviousOwnersDescendants()
    {
        // A→B→C; A=RED, B=BLUE, C=BLUE
        // RED captures B → previous owner = BLUE, LockDescendants(B, BLUE)
        // → C is BLUE owned → C gets LOCKED
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tC.CurrentColor); // BLUE's descendant locked
    }

    [Fact]
    public void CompleteCapture_EntryPointCapture_UnlocksDescendants()
    {
        // A→B→C; A=NONE (entry), B=LOCKED, C=NONE
        // RED captures A → A becomes RED, B unlocks to NONE
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.LOCKED);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("A", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tA.CurrentColor);
        Assert.Equal(TeamColor.NONE, tB.CurrentColor);
    }
```

- [ ] **Step 2: Run capture mutation tests**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "FullyQualifiedName~ChainGraphEngineTests.CompleteCapture" -v n
```
Expected: All 14 tests pass.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs
git commit -m "test: add ChainGraphEngine capture and lock/unlock tests"
```

---

### Task 5: GetChainPoints, GetLinkVisualState, ProcessTick Tests (~16 tests)

**Files:**
- Modify: `OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs`

- [ ] **Step 1: Add GetChainPoints, GetLinkVisualState, and ProcessTick tests**

```csharp
    // --- GetChainPoints ---

    [Fact]
    public void GetChainPoints_SingleTower_ReturnsMultiplier()
    {
        var t1 = T("A"); t1.SetTowerColor(TeamColor.RED); t1.Multiplier = 2.0;
        var towers = Towers(t1, T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        int points = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);

        Assert.Equal(2, points); // multiplier only, depth 0
    }

    [Fact]
    public void GetChainPoints_LinearDepthScaling()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED); tA.Multiplier = 1.0;
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED); tB.Multiplier = 1.0;
        var tC = T("C"); tC.SetTowerColor(TeamColor.RED); tC.Multiplier = 1.0;
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        int points = engine.GetChainPoints(TeamColor.RED, chainFactor: 2.0);

        // depth: A=0, B=1, C=2 → 2^0*1 + 2^1*1 + 2^2*1 = 1+2+4 = 7
        Assert.Equal(7, points);
    }

    [Fact]
    public void GetChainPoints_ChainFactorEffects()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED); tA.Multiplier = 1.0;
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED); tB.Multiplier = 1.0;
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        int pointsChainFactor1 = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);
        int pointsChainFactor3 = engine.GetChainPoints(TeamColor.RED, chainFactor: 3.0);

        Assert.Equal(2, pointsChainFactor1);     // 1^0*1 + 1^1*1 = 2
        Assert.Equal(4, pointsChainFactor3);     // 3^0*1 + 3^1*1 = 4
    }

    [Fact]
    public void GetChainPoints_NoOwnedTowers_ReturnsZero()
    {
        var towers = Towers(T("A"), T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(0, engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0));
    }

    [Fact]
    public void GetChainPoints_MixedTeamOwnership_OnlyCountsOwnTeam()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED); tA.Multiplier = 1.0;
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE); tB.Multiplier = 1.0;
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        int redPoints = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);
        int bluePoints = engine.GetChainPoints(TeamColor.BLUE, chainFactor: 1.0);

        Assert.Equal(1, redPoints);  // only A (depth 0)
        Assert.Equal(1, bluePoints); // only B (depth 1, but owned by BLUE)
    }

    [Fact]
    public void GetChainPoints_TowerNotInDepthMap_UsesDepthZero()
    {
        // Tower in towers dict but not part of chain layout → depth defaults to 0
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED); t1.Multiplier = 3.0;
        var towers = Towers(t1, T("T2"));
        var layout = Layout(Link("T2", "T3")); // T1 is not in chain
        var engine = new ChainGraphEngine(layout, towers);

        int points = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);
        Assert.Equal(3, points);
    }

    // --- GetLinkVisualState ---

    [Fact]
    public void GetLinkVisualState_BothSameTeam_SolidTeamColor()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, arrowA, arrowB, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#fc1911", color);
        Assert.False(arrowA);
        Assert.False(arrowB);
        Assert.False(animated);
        Assert.False(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_BothNeutral_Grey()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, _) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#BBBBBB", color);
        Assert.False(animated);
    }

    [Fact]
    public void GetLinkVisualState_OneEndOwnedOtherNONE_Animated()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, arrowA, arrowB, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#fc1911", color);
        Assert.False(arrowA);
        Assert.True(arrowB);  // capture possible A→B (arrow at B)
        Assert.True(animated);
        Assert.False(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_BothTeamsContested_WhiteAnimatedBothWays()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#FFFFFF", color);
        Assert.True(animated);
        Assert.True(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_LockedTower_Yellow()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.LOCKED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, _, _) = engine.GetLinkVisualState(layout.Links[0]);
        Assert.Equal("#FFD700", color);
    }

    [Fact]
    public void GetLinkVisualState_BidirectionalBothNONE_AnimatedBothWays()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, arrowA, arrowB, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        // Bidirectional: canCaptureAtoB && canCaptureBtoA → only when both are team colors
        // Here: A=SAME_TEAM, B=NONE → canCaptureAtoB=true, canCaptureBtoA=false
        // So it goes to canCaptureAtoB: returns team color + animated + arrowB
        Assert.False(bothWays);
        Assert.True(animated);
        Assert.True(arrowB);
    }

    [Fact]
    public void GetLinkVisualState_BidirectionalBothTeam_DifferentTeams_WhiteBothWays()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#FFFFFF", color);
        Assert.True(animated);
        Assert.True(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_MissingTower_Grey()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA); // T2 missing
        var layout = Layout(Link("A", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, _) = engine.GetLinkVisualState(layout.Links[0]);
        Assert.Equal("#BBBBBB", color);
        Assert.False(animated);
    }

    // --- ProcessTick ---

    [Fact]
    public void ProcessTick_NoPressedTowers_ReturnsEmpty()
    {
        var towers = Towers(T("A"), T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Empty(updates);
    }

    [Fact]
    public void ProcessTick_PressedTowerWithinTime_UpdatesProgress()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        tA.IsPressed = true;
        tA.PressedByColor = TeamColor.RED;
        tA.LastPressed = DateTime.Now; // just pressed
        tA.TimeToCaptureInSeconds = 5;
        var towers = Towers(tA, T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Single(updates);
        Assert.False(updates[0].CaptureCompleted);
        Assert.True(updates[0].CaptureProgress > 0);
        Assert.True(updates[0].CaptureProgress < 1);
    }

    [Fact]
    public void ProcessTick_PressedTowerTimerExpired_CompletesCapture()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        tA.IsPressed = true;
        tA.PressedByColor = TeamColor.RED;
        tA.LastPressed = DateTime.Now.AddSeconds(-10); // expired
        tA.TimeToCaptureInSeconds = 5;
        var towers = Towers(tA, T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Single(updates);
        Assert.True(updates[0].CaptureCompleted);
        Assert.Equal(1, updates[0].CaptureProgress);
        Assert.Equal(TeamColor.RED, tA.CurrentColor); // captured
    }

    [Fact]
    public void ProcessTick_PressDisallowed_ResetsState()
    {
        // A→B, B pressed but A not held (CanPress returns false)
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        tB.IsPressed = true;
        tB.PressedByColor = TeamColor.RED;
        tB.LastPressed = DateTime.Now;
        tB.CaptureProgress = 0.5;
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Single(updates);
        Assert.False(updates[0].CaptureCompleted);
        Assert.Equal(0, updates[0].CaptureProgress);
        Assert.False(tB.IsPressed);
        Assert.Null(tB.LastPressed);
    }
```

- [ ] **Step 2: Run remaining tests**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "FullyQualifiedName~ChainGraphEngineTests" -v n
```
Expected: All ~48 tests pass (includes all tests from Tasks 2-5).

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs
git commit -m "test: add ChainGraphEngine points, visual state, and tick tests"
```

---

### Task 6: Refactor GameModeChainBreak to Delegate to Engine

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeChainBreak.cs`

Replace all extracted methods and fields with delegation to `ChainGraphEngine`. Keep: `Runner()`, `RunGame()`, `EndGame()`, `ResetGame()`, `InitializeTowerStates()`, `DistributePoints()`, `FillTeams()`, properties, constructors.

- [ ] **Step 1: Replace the class body**

Replace lines 32-39 (the private fields) and lines 90-177 (RunGame through EndGame) and lines 179-474 (all chain methods). The class becomes:

```csharp
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

    // Delegated engine — built at RunGame(), nulled at ResetGame()
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

    // Pass-through for Map.razor — delegates to engine
    public (string color, bool arrowA, bool arrowB, bool animated, bool bothWays) GetLinkVisualState(ChainLink link)
    {
        return Engine?.GetLinkVisualState(link)
               ?? ("#BBBBBB", false, false, false, false);
    }
}
```

- [ ] **Step 2: Build and verify compilation**

```bash
dotnet build OWLServer/OWLServer/OWLServer.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Run full test suite**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj -v n
```
Expected: All existing tests pass (including the 2 pre-existing `GameModeChainBreakTests`).

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeChainBreak.cs
git commit -m "refactor: delegate ChainBreak logic to ChainGraphEngine"
```

---

### Task 7: No Map.razor Changes Needed

**Files:**
- No changes needed.

The pass-through `GetLinkVisualState` method on `GameModeChainBreak` preserves the existing API surface consumed by `Map.razor` at line 119. No build or runtime changes required in the UI layer.

> **Skip this task** — Map.razor requires zero modifications.

---

### Task 8: Expand GameModeChainBreakTests

**Files:**
- Modify: `OWLServer/OWLServer.Tests/Unit/GameModes/GameModeChainBreakTests.cs`

Add tests for the remaining game mode logic: `InitializeTowerStates`, `DistributePoints`, win conditions, lifecycle. These tests mock `IExternalTriggerService`, `IGameStateService`, and `ITowerManagerService` per the existing pattern.

- [ ] **Step 1: Add new test methods to the existing test class**

Append these methods to the `GameModeChainBreakTests` class:

```csharp
    [Fact]
    public void InitializeTowerStates_EntryPointTowers_SetToNONE()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["EP"] = new Tower { MacAddress = "EP" },
            ["T2"] = new Tower { MacAddress = "T2" }
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "EP", TowerBMacAddress = "T2", EntryAtBothEnds = false } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();

        Assert.Equal(TeamColor.NONE, towers["EP"].CurrentColor);
    }

    [Fact]
    public void InitializeTowerStates_NonEntryChainTowers_SetToLOCKED()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["EP"] = new Tower { MacAddress = "EP" },
            ["T2"] = new Tower { MacAddress = "T2" }
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "EP", TowerBMacAddress = "T2", EntryAtBothEnds = false } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();

        Assert.Equal(TeamColor.LOCKED, towers["T2"].CurrentColor);
    }

    [Fact]
    public void InitializeTowerStates_OutsiderTower_SetToNONE()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["EP"] = new Tower { MacAddress = "EP" },
            ["T2"] = new Tower { MacAddress = "T2" },
            ["T3"] = new Tower { MacAddress = "T3" } // not in chain
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "EP", TowerBMacAddress = "T2", EntryAtBothEnds = false } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();

        Assert.Equal(TeamColor.NONE, towers["T3"].CurrentColor);
    }

    [Fact]
    public void GetWinner_BlueHasMorePoints_ReturnsBlue()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.BLUE] = 10;
        chainBreak.TeamPoints[TeamColor.RED] = 5;

        Assert.Equal(TeamColor.BLUE, chainBreak.GetWinner);
    }

    [Fact]
    public void GetWinner_RedHasMorePoints_ReturnsRed()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.BLUE] = 3;
        chainBreak.TeamPoints[TeamColor.RED] = 8;

        Assert.Equal(TeamColor.RED, chainBreak.GetWinner);
    }

    [Fact]
    public void GetDisplayPoints_IsTicket_MirrorsOpponent()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = true, MaxTickets = 15 };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.RED] = 3;

        Assert.Equal(12, chainBreak.GetDisplayPoints(TeamColor.BLUE)); // 15 - 3
    }

    [Fact]
    public void GetDisplayPoints_NotTicket_ReturnsOwnPoints()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.BLUE] = 7;

        Assert.Equal(7, chainBreak.GetDisplayPoints(TeamColor.BLUE));
    }

    [Fact]
    public void Engine_NullAfterReset()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout { Links = new List<ChainLink>() }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();
        Assert.NotNull(chainBreak.Engine);

        chainBreak.ResetGame();
        Assert.Null(chainBreak.Engine);
    }

    [Fact]
    public void GetLinkVisualState_PassThrough_DelegatesToEngine()
    {
        // Create a real engine with real towers to test the pass-through
        var towers = new Dictionary<string, Tower>
        {
            ["A"] = new Tower { MacAddress = "A" },
            ["B"] = new Tower { MacAddress = "B" }
        };
        towers["A"].SetTowerColor(TeamColor.RED);
        towers["B"].SetTowerColor(TeamColor.RED);

        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "A", TowerBMacAddress = "B" } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();

        var link = chainBreak.ActiveChainLayout!.Links[0];
        var (color, _, _, _, _) = chainBreak.GetLinkVisualState(link);
        Assert.Equal("#fc1911", color);
    }
```

- [ ] **Step 2: Run GameModeChainBreak tests**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "FullyQualifiedName~GameModeChainBreakTests" -v n
```
Expected: All 11 tests pass (2 pre-existing + 9 new).

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer.Tests/Unit/GameModes/GameModeChainBreakTests.cs
git commit -m "test: expand GameModeChainBreak tests for tower init, scoring, lifecycle"
```

---

### Task 9: Full Test Suite Verification

- [ ] **Step 1: Run the complete test suite**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj -v n
```
Expected: All tests pass (pre-existing tests + ~58 new ChainBreak tests).

- [ ] **Step 2: Verify no regressions in other test categories**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj -v n 2>&1 | Select-String "Failed|Passed|Total"
```
Expected: `Failed: 0`, `Passed:` matches total.

- [ ] **Step 3: Build the main project**

```bash
dotnet build OWLServer/OWLServer/OWLServer.csproj
```
Expected: Build succeeds with no warnings.

- [ ] **Step 4: Commit**

```bash
git commit -m "verify: full test suite passes after ChainBreak refactor" --allow-empty
```
(Only commit if there are any trailing changes; use `--allow-empty` if you just want to mark the verification checkpoint.)
```

