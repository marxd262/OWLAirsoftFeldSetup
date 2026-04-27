# ChainBreak Game Mode — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the ChainBreak standalone game mode where teams capture towers in a predefined directed chain, with chain-break mechanics that instantly lock descendant towers on disruption.

**Architecture:** `GameModeChainBreak` extends Conquest configuration and replaces its state machine with a chain-aware version. Chain topology (directed/bidirectional links between towers) is persisted as `ChainLayout` + `ChainLink` DB entities. At game start, the active layout is loaded into an in-memory graph (predecessors, successors, entry points, depth map). The state machine runs every 200 ms, validates presses, completes captures, and cascades locks. No changes to `Tower`, `TowerManagerService`, or `GameModeConquest`.

**Tech Stack:** .NET 8, ASP.NET Core Blazor Interactive Server, SQLite + EF Core 9, Radzen.Blazor

---

## File Map

| Action | File | Responsibility |
|---|---|---|
| Create | `OWLServer/Models/ChainLayout.cs` | Persisted preset entity |
| Create | `OWLServer/Models/ChainLink.cs` | Persisted link entity |
| Create | `OWLServer/Models/GameModes/GameModeChainBreak.cs` | Game mode: chain graph, state machine, scoring |
| Create | `OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor` | Config UI + inline Layout Manager |
| Create | `OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs` | Config code-behind (DB + service access) |
| Modify | `OWLServer/Models/Enums.cs` | Add `ChainBreak` to `GameMode` enum |
| Modify | `OWLServer/Context/DatabaseContext.cs` | Add `DbSet<ChainLayout>` + `DbSet<ChainLink>` + model config |
| Modify | `OWLServer/Components/Pages/AdminPages/AdminPanel.razor.cs` | Add `case GameMode.ChainBreak` |
| Modify | `OWLServer/Components/Pages/AdminPages/AdminPanel.razor` | Add ChainBreak config card in switch |
| Modify | `OWLServer/Components/MapComponents/Map.razor` | Render chain link arrows/lines overlay |

---

### Task 1: Persisted entities — ChainLayout and ChainLink

**Files:**
- Create: `OWLServer/Models/ChainLayout.cs`
- Create: `OWLServer/Models/ChainLink.cs`
- Modify: `OWLServer/Context/DatabaseContext.cs`

- [ ] **Step 1: Create ChainLayout.cs**

```csharp
// OWLServer/Models/ChainLayout.cs
namespace OWLServer.Models;

public class ChainLayout
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ChainLink> Links { get; set; } = new();
}
```

- [ ] **Step 2: Create ChainLink.cs**

```csharp
// OWLServer/Models/ChainLink.cs
namespace OWLServer.Models;

public class ChainLink
{
    public int Id { get; set; }
    public int ChainLayoutId { get; set; }
    public string FromTowerMacAddress { get; set; } = string.Empty;
    public string ToTowerMacAddress { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; }
}
```

- [ ] **Step 3: Register entities in DatabaseContext.cs**

Replace the entire `DatabaseContext.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using OWLServer.Models;

namespace OWLServer.Context
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Tower> Towers { get; set; }
        public DbSet<TeamBase> Teams { get; set; }
        public DbSet<ChainLayout> ChainLayouts { get; set; }
        public DbSet<ChainLink> ChainLinks { get; set; }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Tower>(e =>
            {
                e.HasKey(t => t.Id);
                e.HasOne(t => t.Location);
            });
            builder.Entity<ElementLocation>(e =>
            {
                e.HasKey(el => el.Id);
            });
            builder.Entity<TeamBase>(e =>
            {
                e.HasKey(el => el.Id);
            });
            builder.Entity<ChainLayout>(e =>
            {
                e.HasKey(cl => cl.Id);
                e.HasMany(cl => cl.Links)
                 .WithOne()
                 .HasForeignKey(lnk => lnk.ChainLayoutId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<ChainLink>(e =>
            {
                e.HasKey(lnk => lnk.Id);
            });
        }
    }
}
```

- [ ] **Step 4: Delete the existing DB file so EnsureCreated recreates schema**

```
Delete OWLServer/OWLAirsoft.db (and .db-shm, .db-wal if present)
```

- [ ] **Step 5: Build to verify no errors**

```
dotnet build OWLServer/OWLServer.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add OWLServer/Models/ChainLayout.cs OWLServer/Models/ChainLink.cs OWLServer/Context/DatabaseContext.cs
git commit -m "feat: add ChainLayout and ChainLink persisted entities"
```

---

### Task 2: Add ChainBreak to GameMode enum

**Files:**
- Modify: `OWLServer/Models/Enums.cs`

- [ ] **Step 1: Add ChainBreak to the GameMode enum**

In `OWLServer/Models/Enums.cs`, change the `GameMode` enum to:

```csharp
public enum GameMode
{
    None,
    TeamDeathMatch,
    Conquest,
    Timer,
    ChainBreak,
    CaptureTheFlag,
    Bomb,
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build OWLServer/OWLServer.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/Models/Enums.cs
git commit -m "feat: add ChainBreak to GameMode enum"
```

---

### Task 3: GameModeChainBreak — base structure and Conquest config

**Files:**
- Create: `OWLServer/Models/GameModes/GameModeChainBreak.cs`

This task creates the full file with all Conquest config fields, base game loop, and placeholder chain methods (implemented in Task 4).

- [ ] **Step 1: Create GameModeChainBreak.cs**

```csharp
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
```

- [ ] **Step 2: Build to verify**

```
dotnet build OWLServer/OWLServer.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/Models/GameModes/GameModeChainBreak.cs
git commit -m "feat: add GameModeChainBreak base structure"
```

---

### Task 4: GameModeChainBreak — chain graph and state machine

**Files:**
- Modify: `OWLServer/Models/GameModes/GameModeChainBreak.cs`

Replace the three stub methods (`BuildChainMaps`, `ProcessChainBreakStateMachine`, `GetChainPoints`) with the full implementations below.

- [ ] **Step 1: Implement BuildChainMaps()**

Replace `private void BuildChainMaps() { }` with:

```csharp
private void BuildChainMaps()
{
    _successors = new Dictionary<string, List<string>>();
    _predecessors = new Dictionary<string, List<string>>();
    _chainEntryPoints = new HashSet<string>();
    _depthMap = new Dictionary<string, int>();

    if (ActiveChainLayout == null) return;

    // Build directed successor/predecessor maps.
    // Bidirectional links add edges in both directions.
    foreach (var link in ActiveChainLayout.Links)
    {
        AddEdge(link.FromTowerMacAddress, link.ToTowerMacAddress);
        if (link.IsBidirectional)
            AddEdge(link.ToTowerMacAddress, link.FromTowerMacAddress);
    }

    // --- Determine entry points ---
    // Unidirectional roots: appear as From but never as To in directed-only links.
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

    // Bidirectional endpoints: degree 1 in the undirected bidirectional graph,
    // and not appearing as the To of any unidirectional link.
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

    // --- BFS depth from all entry points simultaneously (shortest path) ---
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
```

- [ ] **Step 2: Implement CanPress() helper**

Add this private method after `AddEdge`:

```csharp
private bool CanPress(string mac, TeamColor team)
{
    var towers = GameStateService.TowerManagerService.Towers;
    if (!towers.ContainsKey(mac)) return false;
    var tower = towers[mac];

    // Not in layout → always capturable
    bool inLayout = _predecessors.ContainsKey(mac) || _successors.ContainsKey(mac)
                    || _chainEntryPoints.Contains(mac);
    if (!inLayout) return true;

    // Entry point → always capturable
    if (_chainEntryPoints.Contains(mac)) return true;

    // Held by opponent → always pressable (disruption)
    if (tower.CurrentColor != TeamColor.NONE
        && tower.CurrentColor != TeamColor.LOCKED
        && tower.CurrentColor != team)
        return true;

    // Must hold at least one predecessor
    if (_predecessors.TryGetValue(mac, out var preds))
        return preds.Any(p => towers.TryGetValue(p, out var pt) && pt.CurrentColor == team);

    return false;
}
```

- [ ] **Step 3: Implement LockDescendants() and UnlockSuccessors() helpers**

Add these after `CanPress`:

```csharp
private void LockDescendants(string mac, TeamColor previousOwner, HashSet<string>? visited = null)
{
    visited ??= new HashSet<string>();
    if (!visited.Add(mac)) return;

    if (!_successors.TryGetValue(mac, out var succs)) return;
    var towers = GameStateService.TowerManagerService.Towers;

    foreach (var succMac in succs)
    {
        if (!towers.TryGetValue(succMac, out var succTower)) continue;
        if (_chainEntryPoints.Contains(succMac)) continue; // never lock entry points

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

        // Unlock if the team now holds at least one predecessor of this successor
        bool prereqMet = _chainEntryPoints.Contains(succMac);
        if (!prereqMet && _predecessors.TryGetValue(succMac, out var preds))
            prereqMet = preds.Any(p => towers.TryGetValue(p, out var pt) && pt.CurrentColor == capturingTeam);

        if (prereqMet)
            succTower.SetTowerColor(TeamColor.NONE);
    }
}
```

- [ ] **Step 4: Implement ProcessChainBreakStateMachine()**

Replace `private void ProcessChainBreakStateMachine() { }` with:

```csharp
private void ProcessChainBreakStateMachine()
{
    var towers = GameStateService.TowerManagerService.Towers;

    foreach (var tower in towers.Values.Where(t => t.IsPressed).ToList())
    {
        var mac = tower.MacAddress;
        var pressingTeam = tower.PressedByColor;

        // Cancel press if prerequisites no longer met
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

    // Lock previous owner's descendants before changing color
    if (previousOwner != TeamColor.NONE && previousOwner != TeamColor.LOCKED)
        LockDescendants(tower.MacAddress, previousOwner);

    // Check if capturing team holds prerequisites
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
        // Disruption: tower goes neutral; lock any open successors
        tower.SetTowerColor(TeamColor.NONE);
        LockDescendants(tower.MacAddress, TeamColor.NONE);
    }
}
```

- [ ] **Step 5: Implement GetChainPoints()**

Replace `private int GetChainPoints(TeamColor team) => 0;` with:

```csharp
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
```

- [ ] **Step 6: Build to verify**

```
dotnet build OWLServer/OWLServer.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add OWLServer/Models/GameModes/GameModeChainBreak.cs
git commit -m "feat: implement ChainBreak chain graph, state machine, and scoring"
```

---

### Task 5: Wire ChainBreak into the Admin Panel

**Files:**
- Modify: `OWLServer/Components/Pages/AdminPages/AdminPanel.razor.cs`
- Modify: `OWLServer/Components/Pages/AdminPages/AdminPanel.razor`

- [ ] **Step 1: Add ChainBreak case in AdminPanel.razor.cs**

In `GameModeChanged()`, add a new `case` before the `default`:

```csharp
case GameMode.ChainBreak:
    GameStateService.CurrentGame = new GameModeChainBreak(ExternalTriggerService, GameStateService);
    GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
    break;
```

The full `switch` block in `GameModeChanged()` should now look like:

```csharp
switch (mode)
{
    case GameMode.None:
        GameStateService.CurrentGame = null;
        break;
    case GameMode.TeamDeathMatch:
        GameStateService.CurrentGame = new GameModeTeamDeathmatch(ExternalTriggerService, GameStateService);
        GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
        break;
    case GameMode.Conquest:
        GameStateService.CurrentGame = new GameModeConquest(ExternalTriggerService, GameStateService);
        GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
        break;
    case GameMode.Timer:
        GameStateService.CurrentGame = new GameModeTimer(ExternalTriggerService, GameStateService);
        break;
    case GameMode.ChainBreak:
        GameStateService.CurrentGame = new GameModeChainBreak(ExternalTriggerService, GameStateService);
        GameStateService.CurrentGame.FillTeams(GameStateService.Teams.Values.ToList());
        break;
    default:
        break;
}
```

- [ ] **Step 2: Add ChainBreak config card in AdminPanel.razor**

In `AdminPanel.razor`, inside the `@switch (GameStateService.CurrentGame)` block, add a new `case` before `default`:

```razor
case GameModeChainBreak:
    <GameModeChainBreakConfig
        CurrentGame="@((GameModeChainBreak)GameStateService.CurrentGame)"/>
    break;
```

The switch block should now look like:

```razor
@switch (GameStateService.CurrentGame)
{
    case GameModeTeamDeathmatch:
        <GameModeDeathMatchConfig
            CurrentGame="@((GameModeTeamDeathmatch)GameStateService.CurrentGame)"/>
        break;
    case GameModeConquest:
        <GameModeConquestConfig
            CurrentGame="@((GameModeConquest)GameStateService.CurrentGame)"/>
        break;
    case GameModeTimer:
        <GameModeTimerConfig
            CurrentGame="@((GameModeTimer)GameStateService.CurrentGame)"/>
        break;
    case GameModeChainBreak:
        <GameModeChainBreakConfig
            CurrentGame="@((GameModeChainBreak)GameStateService.CurrentGame)"/>
        break;
    default:
        <RadzenText>
            Kein unterstützter Spielmodus Ausgewählt
        </RadzenText>
        break;
}
```

Also add the using at the top of `AdminPanel.razor` if not present:

```razor
@using OWLServer.Models.GameModes
```

- [ ] **Step 3: Build to verify (GameModeChainBreakConfig doesn't exist yet — expect a build error on the component reference)**

```
dotnet build OWLServer/OWLServer.csproj
```

Expected: Error referencing `GameModeChainBreakConfig` — that's fine, Task 6 adds it.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/Components/Pages/AdminPages/AdminPanel.razor.cs OWLServer/Components/Pages/AdminPages/AdminPanel.razor
git commit -m "feat: wire ChainBreak into admin panel game mode selector"
```

---

### Task 6: ChainBreak config component with inline Layout Manager

**Files:**
- Create: `OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor`
- Create: `OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs`

- [ ] **Step 1: Create GameModeChainBreakConfig.razor.cs**

```csharp
// OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class GameModeChainBreakConfig : ComponentBase
{
    [Parameter] public GameModeChainBreak CurrentGame { get; set; } = null!;

    [Inject] public GameStateService GameStateService { get; set; } = null!;
    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;

    private List<ChainLayout> _savedLayouts = new();

    // Editor state
    private List<ChainLink> _editorLinks = new();
    private int? _editingLayoutId;
    private string _newLayoutName = string.Empty;

    // New-link form
    private string _fromMac = string.Empty;
    private string _toMac = string.Empty;
    private bool _isBidirectional;

    protected override async Task OnInitializedAsync()
    {
        await LoadSavedLayouts();
    }

    private async Task LoadSavedLayouts()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _savedLayouts = await db.ChainLayouts.Include(cl => cl.Links).ToListAsync();
    }

    private void LoadLayoutIntoEditor(ChainLayout layout)
    {
        _editingLayoutId = layout.Id;
        _newLayoutName = layout.Name;
        _editorLinks = layout.Links
            .Select(l => new ChainLink
            {
                FromTowerMacAddress = l.FromTowerMacAddress,
                ToTowerMacAddress   = l.ToTowerMacAddress,
                IsBidirectional     = l.IsBidirectional
            })
            .ToList();
    }

    private void AddLink()
    {
        if (string.IsNullOrEmpty(_fromMac) || string.IsNullOrEmpty(_toMac)) return;
        if (_fromMac == _toMac) return;
        _editorLinks.Add(new ChainLink
        {
            FromTowerMacAddress = _fromMac,
            ToTowerMacAddress   = _toMac,
            IsBidirectional     = _isBidirectional
        });
        _fromMac = string.Empty;
        _toMac   = string.Empty;
        _isBidirectional = false;
    }

    private void RemoveLink(ChainLink link) => _editorLinks.Remove(link);

    private async Task SaveAsNew()
    {
        if (string.IsNullOrWhiteSpace(_newLayoutName)) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = new ChainLayout
        {
            Name  = _newLayoutName,
            Links = _editorLinks.Select(l => new ChainLink
            {
                FromTowerMacAddress = l.FromTowerMacAddress,
                ToTowerMacAddress   = l.ToTowerMacAddress,
                IsBidirectional     = l.IsBidirectional
            }).ToList()
        };
        db.ChainLayouts.Add(layout);
        await db.SaveChangesAsync();
        _editingLayoutId = layout.Id;
        await LoadSavedLayouts();
    }

    private async Task UpdateExisting()
    {
        if (_editingLayoutId == null) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = await db.ChainLayouts.Include(cl => cl.Links)
                             .FirstOrDefaultAsync(cl => cl.Id == _editingLayoutId);
        if (layout == null) return;
        layout.Name = _newLayoutName;
        db.ChainLinks.RemoveRange(layout.Links);
        layout.Links = _editorLinks.Select(l => new ChainLink
        {
            FromTowerMacAddress = l.FromTowerMacAddress,
            ToTowerMacAddress   = l.ToTowerMacAddress,
            IsBidirectional     = l.IsBidirectional
        }).ToList();
        await db.SaveChangesAsync();
        await LoadSavedLayouts();
    }

    private async Task DeleteLayout(ChainLayout layout)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var entity = await db.ChainLayouts.FindAsync(layout.Id);
        if (entity != null)
        {
            db.ChainLayouts.Remove(entity);
            await db.SaveChangesAsync();
        }
        if (_editingLayoutId == layout.Id)
        {
            _editingLayoutId = null;
            _editorLinks.Clear();
            _newLayoutName = string.Empty;
        }
        await LoadSavedLayouts();
    }

    private void ActivateLayout(ChainLayout layout)
    {
        // Attach the full links from the saved list (already includes all links from DB)
        CurrentGame.ActiveChainLayout = layout;
    }

    private string TowerLabel(string mac)
    {
        if (GameStateService.TowerManagerService.Towers.TryGetValue(mac, out var t))
            return $"{t.DisplayLetter} – {t.Name}";
        return mac;
    }

    private string LinkLabel(ChainLink link)
    {
        var arrow = link.IsBidirectional ? "↔" : "→";
        return $"{TowerLabel(link.FromTowerMacAddress)} {arrow} {TowerLabel(link.ToTowerMacAddress)}";
    }
}
```

- [ ] **Step 2: Create GameModeChainBreakConfig.razor**

```razor
@* OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor *@
@using OWLServer.Models
@using OWLServer.Models.GameModes

<RadzenStack Orientation="Orientation.Vertical" Gap="1rem">

    @* ── Base config ── *@
    <RadzenStack>
        <RadzenFormField Text="Game Duration (min)" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.GameDurationInMinutes" Min="1"/>
        </RadzenFormField>
        <RadzenFormField Text="Max Tickets" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.MaxTickets" Min="1"/>
        </RadzenFormField>
        <RadzenFormField Text="Is Ticket" Variant="Variant.Flat">
            <RadzenCheckBox @bind-Value="CurrentGame.IsTicket"/>
        </RadzenFormField>
        <RadzenFormField Text="Point Distribution (s)" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.PointDistributionFrequencyInSeconds" Min="1"/>
        </RadzenFormField>
        <RadzenFormField Text="Chain Factor" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.ChainFactor" Min="1.0" Step="0.1" TValue="double"/>
        </RadzenFormField>
    </RadzenStack>

    @* ── Active layout badge ── *@
    @if (CurrentGame.ActiveChainLayout != null)
    {
        <RadzenAlert AlertStyle="AlertStyle.Success" AllowClose="false">
            Aktives Layout: <strong>@CurrentGame.ActiveChainLayout.Name</strong>
            (@CurrentGame.ActiveChainLayout.Links.Count Links)
        </RadzenAlert>
    }
    else
    {
        <RadzenAlert AlertStyle="AlertStyle.Warning" AllowClose="false">
            Kein Chain-Layout aktiv — alle Tower frei einnehm bar.
        </RadzenAlert>
    }

    @* ── Saved layouts list ── *@
    <RadzenCard Variant="Variant.Outlined">
        <RadzenText TextStyle="TextStyle.Subtitle2" class="rz-mb-2">Gespeicherte Layouts</RadzenText>
        @if (!_savedLayouts.Any())
        {
            <RadzenText TextStyle="TextStyle.Body2">Noch keine Layouts gespeichert.</RadzenText>
        }
        else
        {
            @foreach (var layout in _savedLayouts)
            {
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                             JustifyContent="JustifyContent.SpaceBetween" class="rz-mb-1">
                    <RadzenText>@layout.Name (@layout.Links.Count Links)</RadzenText>
                    <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem">
                        <RadzenButton Text="Laden" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Secondary"
                                      Click="@(() => LoadLayoutIntoEditor(layout))"/>
                        <RadzenButton Text="Aktivieren" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Success"
                                      Click="@(() => ActivateLayout(layout))"/>
                        <RadzenButton Text="Löschen" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Danger"
                                      Click="@(async () => await DeleteLayout(layout))"/>
                    </RadzenStack>
                </RadzenStack>
            }
        }
    </RadzenCard>

    @* ── Layout editor ── *@
    <RadzenCard Variant="Variant.Outlined">
        <RadzenText TextStyle="TextStyle.Subtitle2" class="rz-mb-2">Layout-Editor</RadzenText>

        @* Existing links *@
        @if (_editorLinks.Any())
        {
            @foreach (var link in _editorLinks.ToList())
            {
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                             JustifyContent="JustifyContent.SpaceBetween" class="rz-mb-1">
                    <RadzenText>@LinkLabel(link)</RadzenText>
                    <RadzenButton Icon="delete" ButtonStyle="ButtonStyle.Danger" Size="ButtonSize.Small"
                                  Click="@(() => RemoveLink(link))"/>
                </RadzenStack>
            }
        }
        else
        {
            <RadzenText TextStyle="TextStyle.Body2" class="rz-mb-2">Noch keine Links im Editor.</RadzenText>
        }

        @* Add new link form *@
        <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem" Wrap="FlexWrap.Wrap" class="rz-mt-2">
            <RadzenDropDown TValue="string" @bind-Value="_fromMac"
                            Data="GameStateService.TowerManagerService.Towers.Values"
                            TextProperty="DisplayLetter"
                            ValueProperty="MacAddress"
                            Placeholder="Von Tower"
                            Style="min-width:120px"/>
            <RadzenDropDown TValue="string" @bind-Value="_toMac"
                            Data="GameStateService.TowerManagerService.Towers.Values"
                            TextProperty="DisplayLetter"
                            ValueProperty="MacAddress"
                            Placeholder="Zu Tower"
                            Style="min-width:120px"/>
            <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap=".25rem">
                <RadzenCheckBox @bind-Value="_isBidirectional" Name="biCheck"/>
                <RadzenLabel Text="Bidirektional" Component="biCheck"/>
            </RadzenStack>
            <RadzenButton Text="Link hinzufügen" Icon="add" ButtonStyle="ButtonStyle.Primary"
                          Click="AddLink"/>
        </RadzenStack>

        @* Save / Update *@
        <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem" class="rz-mt-3" Wrap="FlexWrap.Wrap">
            <RadzenTextBox @bind-Value="_newLayoutName" Placeholder="Layout-Name" Style="flex:1;min-width:150px"/>
            <RadzenButton Text="Neu speichern" Icon="save" ButtonStyle="ButtonStyle.Info"
                          Click="@(async () => await SaveAsNew())"/>
            @if (_editingLayoutId != null)
            {
                <RadzenButton Text="Aktualisieren" Icon="update" ButtonStyle="ButtonStyle.Warning"
                              Click="@(async () => await UpdateExisting())"/>
            }
        </RadzenStack>
    </RadzenCard>

    @* ── Towers outside layout info ── *@
    @{
        var allMacs = GameStateService.TowerManagerService.Towers.Keys.ToHashSet();
        var inLayout = _editorLinks
            .SelectMany(l => new[] { l.FromTowerMacAddress, l.ToTowerMacAddress })
            .ToHashSet();
        var outsideCount = allMacs.Except(inLayout).Count();
    }
    @if (outsideCount > 0)
    {
        <RadzenText TextStyle="TextStyle.Caption" Style="color:var(--rz-text-secondary-color)">
            @outsideCount Tower außerhalb des Layouts — frei einnehmbar.
        </RadzenText>
    }

</RadzenStack>
```

- [ ] **Step 3: Register IDbContextFactory in Program.cs**

Find where `DatabaseContext` is registered in `Program.cs` (look for `AddDbContext`). Replace it (or add alongside) with `AddDbContextFactory`:

```csharp
// Replace AddDbContext with AddDbContextFactory
builder.Services.AddDbContextFactory<DatabaseContext>(options =>
    options.UseSqlite("Data Source=OWLAirsoft.db"));
```

If the original line is `builder.Services.AddDbContext<DatabaseContext>(...)`, replace it entirely with the `AddDbContextFactory` line above.

- [ ] **Step 4: Build to verify**

```
dotnet build OWLServer/OWLServer.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run and verify manually**

```
dotnet run --project OWLServer/OWLServer.csproj
```

Navigate to the admin panel at `http://localhost:65059`. Select "ChainBreak" from the game mode dropdown. Verify:
- ChainBreak config card appears with all fields (Duration, Tickets, ChainFactor, etc.)
- Layout Manager shows "Noch keine Layouts gespeichert"
- Adding two towers from the dropdowns and clicking "Link hinzufügen" adds a link to the editor
- "Neu speichern" with a name saves a layout (appears in the saved list)
- "Aktivieren" sets the active layout (green badge shows)

- [ ] **Step 6: Commit**

```bash
git add OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs
git commit -m "feat: add ChainBreak config component with inline Layout Manager"
```

---

### Task 7: Map overlay for chain links

**Files:**
- Modify: `OWLServer/Components/MapComponents/Map.razor`

The map already renders SVG arrows for the `ControllsTowerID` mechanic. We add a second set of lines for ChainBreak links, rendered only when `GameModeChainBreak` is active and an `ActiveChainLayout` is set.

- [ ] **Step 1: Add chain link helper properties to the @code block**

In `Map.razor`, inside the `@code { }` block, add these properties after `ControllingSortedTowers`:

```csharp
private bool IsChainBreakActive =>
    _GameStateService.CurrentGame is GameModeChainBreak cb
    && cb.ActiveChainLayout != null;

private IEnumerable<ChainLink> ActiveChainLinks =>
    (_GameStateService.CurrentGame as GameModeChainBreak)
        ?.ActiveChainLayout?.Links
    ?? Enumerable.Empty<ChainLink>();

private string ChainLinkColor => "#9b59b6"; // purple, distinct from team/control arrows
```

Also add the using at the top of `Map.razor` if not already there:

```razor
@using OWLServer.Models.GameModes
@using OWLServer.Models
```

- [ ] **Step 2: Add chain link SVG rendering**

Inside the `<svg>` element, after the existing `@foreach (var src in ControllingSortedTowers)` loop that renders control arrows, add:

```razor
@* Chain link overlay — only visible in ChainBreak mode *@
@if (IsChainBreakActive)
{
    @* Arrowhead marker for unidirectional chain links *@
    <defs>
        <marker id="chain-arrow" markerWidth="6" markerHeight="5" refX="0" refY="2.5"
                orient="auto" markerUnits="userSpaceOnUse">
            <polygon points="0 0, 6 2.5, 0 5" fill="@ChainLinkColor"/>
        </marker>
    </defs>

    @foreach (var link in ActiveChainLinks)
    {
        var srcTower = _GameStateService.TowerManagerService.Towers.Values
            .FirstOrDefault(t => t.MacAddress == link.FromTowerMacAddress && t.Location != null);
        var dstTower = _GameStateService.TowerManagerService.Towers.Values
            .FirstOrDefault(t => t.MacAddress == link.ToTowerMacAddress && t.Location != null);

        @if (srcTower != null && dstTower != null)
        {
            @if (link.IsBidirectional)
            {
                @* Bidirectional: plain line, no arrowhead *@
                var (bx1, by1, bx2, by2) = ArrowLine(srcTower, dstTower, srcGap: 3.0, destGap: 3.0);
                <line x1="@bx1" y1="@by1" x2="@bx2" y2="@by2"
                      stroke="@ChainLinkColor"
                      stroke-width="2"
                      stroke-dasharray="4 2"
                      filter="url(#arrowGlow)"/>
            }
            else
            {
                @* Unidirectional: arrow *@
                var (ax1, ay1, ax2, ay2) = ArrowLine(srcTower, dstTower);
                <line x1="@ax1" y1="@ay1" x2="@ax2" y2="@ay2"
                      stroke="@ChainLinkColor"
                      stroke-width="2"
                      stroke-dasharray="4 2"
                      marker-end="url(#chain-arrow)"
                      filter="url(#arrowGlow)"/>
            }
        }
    }
}
```

- [ ] **Step 3: Build to verify**

```
dotnet build OWLServer/OWLServer.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run and verify manually**

```
dotnet run --project OWLServer/OWLServer.csproj
```

Navigate to the home page. With ChainBreak active and a layout with links activated:
- Verify purple dashed arrows appear on the map between linked towers (towers must have a `Location` set via the map editor)
- Verify unidirectional links show an arrowhead
- Verify bidirectional links show a plain dashed line (no arrowhead)
- Verify no chain overlay appears when Conquest or other modes are selected

- [ ] **Step 5: Commit**

```bash
git add OWLServer/Components/MapComponents/Map.razor
git commit -m "feat: render chain link overlay on map in ChainBreak mode"
```

---

## Self-Review Checklist

- [x] **Spec coverage:**
  - ChainLayout + ChainLink entities → Task 1 ✓
  - ChainBreak enum entry → Task 2 ✓
  - GameModeChainBreak base + Conquest config fields → Task 3 ✓
  - Chain graph (predecessors, successors, entry points, depth BFS) → Task 4 ✓
  - Press validation (entry points, opponent disruption, prerequisite check) → Task 4 ✓
  - Capture complete: flip to color or NEUTRAL + cascade locks → Task 4 ✓
  - Unlock successors after capture → Task 4 ✓
  - ChainFactor exponential scoring → Task 4 ✓
  - Admin panel wiring → Task 5 ✓
  - Config card with all fields + Layout Manager (CRUD, load, activate) → Task 6 ✓
  - Towers outside layout shown as freely capturable info → Task 6 ✓
  - Map overlay: arrow for unidirectional, line for bidirectional → Task 7 ✓

- [x] **Type consistency:**
  - `GameModeChainBreak.ActiveChainLayout` is `ChainLayout?` — used consistently in Task 3, 4, 6, 7
  - `ChainLink.IsBidirectional` `bool` used in Task 1, 4, 6, 7
  - `_depthMap`, `_successors`, `_predecessors`, `_chainEntryPoints` defined in Task 3, populated in Task 4

- [x] **No placeholders:** All steps include complete code. No TBDs.

- [x] **DB note:** The existing `OWLAirsoft.db` must be deleted before first run so `EnsureCreated` rebuilds the schema with the new `ChainLayouts` and `ChainLinks` tables. Task 1 Step 4 calls this out explicitly.