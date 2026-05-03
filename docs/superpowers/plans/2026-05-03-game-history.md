# Game History Feature Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record every game session (completed or stopped) to the database with full metadata, scores, tower states, team composition, and configuration snapshots; display history in a card grid on the admin panel with filters and a detail modal.

**Architecture:** Four new EF entities (`GameHistory`, `GameHistoryTeam`, `GameHistoryTower`, `GameHistorySnapshot`), a singleton `GameHistoryService` that hooks into `GameStateService.StartGame()`/`HandleGameEnd()`/`StopGame()`, and a Blazor page component with card grid + detail modal added as a tab in `AdminStartPage.razor`.

**Tech Stack:** .NET 8, EF Core 9 (SQLite), Radzen.Blazor 10.3.1, System.Text.Json

---

### Task 1: Create model entity classes

**Files:**
- Create: `OWLServer/OWLServer/Models/GameHistory.cs`
- Create: `OWLServer/OWLServer/Models/GameHistoryTeam.cs`
- Create: `OWLServer/OWLServer/Models/GameHistoryTower.cs`
- Create: `OWLServer/OWLServer/Models/GameHistorySnapshot.cs`

- [ ] **Step 1: Write GameHistory.cs**

```csharp
namespace OWLServer.Models
{
    public class GameHistory
    {
        public int Id { get; set; }
        public int GameMode { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int Winner { get; set; }
        public string EndReason { get; set; } = string.Empty;

        public List<GameHistoryTeam> Teams { get; set; } = new();
        public List<GameHistoryTower> Towers { get; set; } = new();
        public GameHistorySnapshot? Snapshot { get; set; }
    }
}
```

- [ ] **Step 2: Write GameHistoryTeam.cs**

```csharp
namespace OWLServer.Models
{
    public class GameHistoryTeam
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public int TeamColor { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public int FinalScore { get; set; }
        public int Deaths { get; set; }
        public int TowersControlled { get; set; }

        public GameHistory? GameHistory { get; set; }
    }
}
```

- [ ] **Step 3: Write GameHistoryTower.cs**

```csharp
namespace OWLServer.Models
{
    public class GameHistoryTower
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string DisplayLetter { get; set; } = string.Empty;
        public int FinalColor { get; set; }
        public int Captures { get; set; }

        public GameHistory? GameHistory { get; set; }
    }
}
```

- [ ] **Step 4: Write GameHistorySnapshot.cs**

```csharp
namespace OWLServer.Models
{
    public class GameHistorySnapshot
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public string SnapshotJSON { get; set; } = string.Empty;

        public GameHistory? GameHistory { get; set; }
    }
}
```

- [ ] **Step 5: Build and verify no compilation errors**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds (warnings OK, no errors).

- [ ] **Step 6: Commit**

```bash
git add OWLServer/OWLServer/Models/GameHistory.cs OWLServer/OWLServer/Models/GameHistoryTeam.cs OWLServer/OWLServer/Models/GameHistoryTower.cs OWLServer/OWLServer/Models/GameHistorySnapshot.cs
git commit -m "feat: add GameHistory entity model classes"
```

---

### Task 2: Register entities in DatabaseContext

**Files:**
- Modify: `OWLServer/OWLServer/Context/DatabaseContext.cs`

- [ ] **Step 1: Add DbSet properties**

In `DatabaseContext.cs`, add these `DbSet` declarations after line 15 (after `TowerPositions`):

```csharp
public DbSet<GameHistory> GameHistories { get; set; }
public DbSet<GameHistoryTeam> GameHistoryTeams { get; set; }
public DbSet<GameHistoryTower> GameHistoryTowers { get; set; }
public DbSet<GameHistorySnapshot> GameHistorySnapshots { get; set; }
```

- [ ] **Step 2: Add OnModelCreating relationships**

Inside `OnModelCreating`, add this block before the closing `}` of the method:

```csharp
builder.Entity<GameHistory>(e =>
{
    e.HasKey(gh => gh.Id);
    e.HasMany(gh => gh.Teams)
     .WithOne(ght => ght.GameHistory)
     .HasForeignKey(ght => ght.GameHistoryId)
     .OnDelete(DeleteBehavior.Cascade);
    e.HasMany(gh => gh.Towers)
     .WithOne(ght => ght.GameHistory)
     .HasForeignKey(ght => ght.GameHistoryId)
     .OnDelete(DeleteBehavior.Cascade);
    e.HasOne(gh => gh.Snapshot)
     .WithOne(gs => gs.GameHistory)
     .HasForeignKey<GameHistorySnapshot>(gs => gs.GameHistoryId)
     .OnDelete(DeleteBehavior.Cascade);
});
builder.Entity<GameHistoryTeam>(e =>
{
    e.HasKey(ght => ght.Id);
});
builder.Entity<GameHistoryTower>(e =>
{
    e.HasKey(ght => ght.Id);
});
builder.Entity<GameHistorySnapshot>(e =>
{
    e.HasKey(gs => gs.Id);
});
```

- [ ] **Step 3: Add raw SQL fallback for table creation**

Inside the `DatabaseContext` constructor, after the existing `TowerControlLinks` raw SQL block (after line 34), add:

```csharp
Database.ExecuteSqlRaw(
    "CREATE TABLE IF NOT EXISTS \"GameHistories\" (" +
    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistories\" PRIMARY KEY AUTOINCREMENT," +
    "    \"GameMode\" INTEGER NOT NULL," +
    "    \"StartTime\" TEXT NOT NULL," +
    "    \"EndTime\" TEXT NULL," +
    "    \"Duration\" TEXT NOT NULL," +
    "    \"Winner\" INTEGER NOT NULL," +
    "    \"EndReason\" TEXT NOT NULL DEFAULT ''" +
    ");");
Database.ExecuteSqlRaw(
    "CREATE TABLE IF NOT EXISTS \"GameHistoryTeams\" (" +
    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistoryTeams\" PRIMARY KEY AUTOINCREMENT," +
    "    \"GameHistoryId\" INTEGER NOT NULL," +
    "    \"TeamColor\" INTEGER NOT NULL," +
    "    \"TeamName\" TEXT NOT NULL DEFAULT ''," +
    "    \"Side\" TEXT NOT NULL DEFAULT ''," +
    "    \"FinalScore\" INTEGER NOT NULL DEFAULT 0," +
    "    \"Deaths\" INTEGER NOT NULL DEFAULT 0," +
    "    \"TowersControlled\" INTEGER NOT NULL DEFAULT 0," +
    "    CONSTRAINT \"FK_GameHistoryTeams_GameHistories_GameHistoryId\" " +
    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
    ");");
Database.ExecuteSqlRaw(
    "CREATE TABLE IF NOT EXISTS \"GameHistoryTowers\" (" +
    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistoryTowers\" PRIMARY KEY AUTOINCREMENT," +
    "    \"GameHistoryId\" INTEGER NOT NULL," +
    "    \"MacAddress\" TEXT NOT NULL DEFAULT ''," +
    "    \"DisplayLetter\" TEXT NOT NULL DEFAULT ''," +
    "    \"FinalColor\" INTEGER NOT NULL DEFAULT -1," +
    "    \"Captures\" INTEGER NOT NULL DEFAULT 0," +
    "    CONSTRAINT \"FK_GameHistoryTowers_GameHistories_GameHistoryId\" " +
    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
    ");");
Database.ExecuteSqlRaw(
    "CREATE TABLE IF NOT EXISTS \"GameHistorySnapshots\" (" +
    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistorySnapshots\" PRIMARY KEY AUTOINCREMENT," +
    "    \"GameHistoryId\" INTEGER NOT NULL," +
    "    \"SnapshotJSON\" TEXT NOT NULL DEFAULT ''," +
    "    CONSTRAINT \"FK_GameHistorySnapshots_GameHistories_GameHistoryId\" " +
    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
    ");");
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add OWLServer/OWLServer/Context/DatabaseContext.cs
git commit -m "feat: register GameHistory entities in DatabaseContext"
```

---

### Task 3: Create IGameHistoryService interface

**Files:**
- Create: `OWLServer/OWLServer/Services/Interfaces/IGameHistoryService.cs`

- [ ] **Step 1: Write the interface**

```csharp
using OWLServer.Models;

namespace OWLServer.Services.Interfaces
{
    public interface IGameHistoryService
    {
        int? CurrentGameId { get; }
        string EndReason { get; set; }
        void RecordGameStart();
        void RecordGameEnd();
        List<GameHistory> GetAllGames();
        GameHistory? GetGame(int id);
        List<GameHistoryTeam> GetGameTeams(int gameHistoryId);
        List<GameHistoryTower> GetGameTowers(int gameHistoryId);
        GameHistorySnapshot? GetGameSnapshot(int gameHistoryId);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Services/Interfaces/IGameHistoryService.cs
git commit -m "feat: add IGameHistoryService interface"
```

---

### Task 4: Create GameHistoryService implementation

**Files:**
- Create: `OWLServer/OWLServer/Services/GameHistoryService.cs`

- [ ] **Step 1: Write GameHistoryService.cs**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services
{
    public class GameHistoryService : IGameHistoryService, IDisposable
    {
        private readonly IDbContextFactory<DatabaseContext> _dbFactory;
        private readonly IExternalTriggerService _externalTriggerService;
        private readonly IGameStateService _gameStateService;

        public int? CurrentGameId { get; private set; }
        public string EndReason { get; set; } = "Completed";

        private Dictionary<string, int> _towerCaptureCounts = new();
        private Dictionary<string, TeamColor> _lastKnownTowerColors = new();

        public GameHistoryService(
            IDbContextFactory<DatabaseContext> dbFactory,
            IExternalTriggerService externalTriggerService,
            IGameStateService gameStateService)
        {
            _dbFactory = dbFactory;
            _externalTriggerService = externalTriggerService;
            _gameStateService = gameStateService;
        }

        public void RecordGameStart()
        {
            EndReason = "Completed";
            _towerCaptureCounts = new();
            _lastKnownTowerColors = new();

            using var db = _dbFactory.CreateDbContext();

            var history = new GameHistory
            {
                GameMode = (int)(_gameStateService.CurrentGame?.GameMode ?? GameMode.None),
                StartTime = DateTime.Now,
                Winner = (int)TeamColor.NONE
            };

            db.GameHistories.Add(history);
            db.SaveChanges();
            CurrentGameId = history.Id;

            foreach (var kvp in _gameStateService.TowerManagerService.Towers)
            {
                var tower = kvp.Value;
                _towerCaptureCounts[kvp.Key] = 0;
                _lastKnownTowerColors[kvp.Key] = tower.CurrentColor;

                db.GameHistoryTowers.Add(new GameHistoryTower
                {
                    GameHistoryId = history.Id,
                    MacAddress = tower.MacAddress,
                    DisplayLetter = tower.DisplayLetter,
                    FinalColor = (int)TeamColor.NONE
                });
            }

            foreach (var kvp in _gameStateService.Teams)
            {
                var team = kvp.Value;
                string side = kvp.Key == _gameStateService.TeamInWald ? "Wald" : "Stadt";
                db.GameHistoryTeams.Add(new GameHistoryTeam
                {
                    GameHistoryId = history.Id,
                    TeamColor = (int)team.TeamColor,
                    TeamName = team.Name,
                    Side = side
                });
            }

            var snapshot = new GameHistorySnapshot
            {
                GameHistoryId = history.Id,
                SnapshotJSON = BuildSnapshotJSON()
            };
            db.GameHistorySnapshots.Add(snapshot);

            db.SaveChanges();

            _externalTriggerService.StateHasChangedAction += TrackCaptures;
        }

        private void TrackCaptures()
        {
            if (CurrentGameId == null) return;

            foreach (var kvp in _gameStateService.TowerManagerService.Towers)
            {
                var tower = kvp.Value;
                if (!_lastKnownTowerColors.TryGetValue(kvp.Key, out var lastColor))
                {
                    _lastKnownTowerColors[kvp.Key] = tower.CurrentColor;
                    continue;
                }

                if (lastColor != tower.CurrentColor)
                {
                    if (tower.CurrentColor == TeamColor.BLUE || tower.CurrentColor == TeamColor.RED)
                    {
                        if (_towerCaptureCounts.ContainsKey(kvp.Key))
                            _towerCaptureCounts[kvp.Key]++;
                        else
                            _towerCaptureCounts[kvp.Key] = 1;
                    }
                    _lastKnownTowerColors[kvp.Key] = tower.CurrentColor;
                }
            }
        }

        public void RecordGameEnd()
        {
            _externalTriggerService.StateHasChangedAction -= TrackCaptures;

            if (CurrentGameId == null) return;

            using var db = _dbFactory.CreateDbContext();

            var history = db.GameHistories.Find(CurrentGameId.Value);
            if (history == null) return;

            history.EndTime = DateTime.Now;
            history.Duration = history.EndTime.Value - history.StartTime;
            history.Winner = (int)(_gameStateService.CurrentGame?.GetWinner ?? TeamColor.NONE);
            history.EndReason = EndReason;

            var teams = db.GameHistoryTeams.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var team in teams)
            {
                var teamColor = (TeamColor)team.TeamColor;
                team.FinalScore = _gameStateService.CurrentGame?.GetDisplayPoints(teamColor) ?? 0;
                team.TowersControlled = _gameStateService.TowerManagerService.Towers.Values
                    .Count(t => t.CurrentColor == teamColor);

                var currentGame = _gameStateService.CurrentGame;
                if (currentGame is GameModeTeamDeathmatch tdm)
                {
                    team.Deaths = tdm.TeamDeaths.GetValueOrDefault(teamColor, 0);
                }
            }

            var towerRecords = db.GameHistoryTowers.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var tr in towerRecords)
            {
                if (_gameStateService.TowerManagerService.Towers.TryGetValue(tr.MacAddress, out var tower))
                {
                    tr.FinalColor = (int)tower.CurrentColor;
                }
                if (_towerCaptureCounts.TryGetValue(tr.MacAddress, out var count))
                {
                    tr.Captures = count;
                }
            }

            db.SaveChanges();

            CurrentGameId = null;
            _towerCaptureCounts = new();
            _lastKnownTowerColors = new();
        }

        private string BuildSnapshotJSON()
        {
            var snapshot = new
            {
                Timestamp = DateTime.Now,
                GameMode = _gameStateService.CurrentGame?.GameMode.ToString(),
                Towers = _gameStateService.TowerManagerService.Towers.Values.Select(t => new
                {
                    t.MacAddress,
                    t.Name,
                    t.DisplayLetter,
                    t.TimeToCaptureInSeconds,
                    t.Multiplier,
                    t.ResetsAfterInSeconds,
                    Location = t.Location != null ? new { t.Location.Top, t.Location.Left } : null
                }).ToList(),
                Teams = _gameStateService.Teams.Select(kvp => new
                {
                    TeamColor = kvp.Key.ToString(),
                    kvp.Value.Name,
                    Side = kvp.Key == _gameStateService.TeamInWald ? "Wald" : "Stadt"
                }).ToList()
            };

            return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public List<GameHistory> GetAllGames()
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories
                .OrderByDescending(gh => gh.StartTime)
                .ToList();
        }

        public GameHistory? GetGame(int id)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories.Find(id);
        }

        public List<GameHistoryTeam> GetGameTeams(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryTeams
                .Where(t => t.GameHistoryId == gameHistoryId)
                .ToList();
        }

        public List<GameHistoryTower> GetGameTowers(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryTowers
                .Where(t => t.GameHistoryId == gameHistoryId)
                .ToList();
        }

        public GameHistorySnapshot? GetGameSnapshot(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistorySnapshots
                .FirstOrDefault(s => s.GameHistoryId == gameHistoryId);
        }

        public void Dispose()
        {
            _externalTriggerService.StateHasChangedAction -= TrackCaptures;
        }
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Services/GameHistoryService.cs
git commit -m "feat: implement GameHistoryService"
```

---

### Task 5: Integrate GameHistoryService into GameStateService

**Files:**
- Modify: `OWLServer/OWLServer/Services/GameStateService.cs`
- Modify: `OWLServer/OWLServer/Services/Interfaces/IGameStateService.cs`

- [ ] **Step 1: Update IGameStateService interface**

Replace the entire file content:

```csharp
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
```

- [ ] **Step 2: Update GameStateService.cs**

Replace the entire file content:

```csharp
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

        public async void StartGame()
        {
            GameHistoryService.RecordGameStart();
            AudioService.PlaySound(Sounds.Start);
            var delay = AudioService.GetDelay(Sounds.Start);
            if (delay > 0) await Task.Delay(delay * 1000);
            CurrentGame?.RunGame();
        }

        public void StopGame()
        {
            GameHistoryService.EndReason = "Stopped";
            CurrentGame?.EndGame();
        }

        public void HandleGameEnd()
        {
            GameHistoryService.RecordGameEnd();
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
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Services/GameStateService.cs OWLServer/OWLServer/Services/Interfaces/IGameStateService.cs
git commit -m "feat: integrate GameHistoryService into GameStateService"
```

---

### Task 6: Register service in Program.cs

**Files:**
- Modify: `OWLServer/OWLServer/Program.cs`

- [ ] **Step 1: Add GameHistoryService registration**

Add this line after `builder.Services.AddSingleton<IGameStateService, GameStateService>();` (line 22):

```csharp
builder.Services.AddSingleton<IGameHistoryService, GameHistoryService>();
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Program.cs
git commit -m "feat: register GameHistoryService in DI container"
```

---

### Task 7: Create GameHistoryPage Blazor component

**Files:**
- Create: `OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryPage.razor`
- Create: `OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryPage.razor.cs`

- [ ] **Step 1: Write GameHistoryPage.razor**

```razor
@page "/Admin/GameHistory"
@implements IDisposable

<RadzenText TextStyle="TextStyle.H4" TagName="TagName.H2" class="mb-3">Spielverlauf</RadzenText>

<div class="mb-3">
    <RadzenRow Gap="16px" AlignItems="AlignItems.Center" class="mb-2">
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenDatePicker @bind-Value="@_dateFrom" Change="@OnFilterChanged" Placeholder="Von Datum" Style="width:100%" />
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenDatePicker @bind-Value="@_dateTo" Change="@OnFilterChanged" Placeholder="Bis Datum" Style="width:100%" />
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="2">
            <RadzenDropDown TValue="int?" @bind-Value="@_filterGameMode" Change="@OnFilterChanged" Placeholder="Spielmodus" Style="width:100%"
                Data="@_gameModeOptions" TextProperty="Text" ValueProperty="Value" AllowClear="true" />
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="2">
            <RadzenDropDown TValue="int?" @bind-Value="@_filterWinner" Change="@OnFilterChanged" Placeholder="Gewinner" Style="width:100%"
                Data="@_winnerOptions" TextProperty="Text" ValueProperty="Value" AllowClear="true" />
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="2">
            <RadzenDropDown TValue="string" @bind-Value="@_sortOrder" Change="@OnFilterChanged" Placeholder="Sortierung" Style="width:100%"
                Data="@_sortOptions" TextProperty="Text" ValueProperty="Value" />
        </RadzenColumn>
    </RadzenRow>
</div>

@if (_filteredGames.Count == 0)
{
    <RadzenCard Class="rz-p-4 rz-text-align-center">
        <RadzenText TextStyle="TextStyle.Body1" TagName="TagName.Span" style="color: var(--rz-text-tertiary-color)">
            Keine Spiele aufgezeichnet
        </RadzenText>
    </RadzenCard>
}
else
{
    <RadzenRow Gap="16px">
        @foreach (var game in _filteredGames)
        {
            var winnerColor = (TeamColor)game.Winner;
            var headerStyle = winnerColor switch
            {
                TeamColor.BLUE => "background: linear-gradient(135deg, #00b4f1, #0088cc); color: white;",
                TeamColor.RED => "background: linear-gradient(135deg, #fc1911, #cc1400); color: white;",
                _ => "background: var(--rz-base-300); color: var(--rz-text-color);"
            };
            var modeName = ((GameMode)game.GameMode).ToString();

            <RadzenColumn Size="12" SizeMD="6" SizeLG="4">
                <RadzenCard Class="rz-p-0" Style="overflow:hidden; cursor:pointer;"
                            Click="@(() => OpenDetail(game.Id))">
                    <div style="@headerStyle" class="rz-p-3">
                        <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.Span">@modeName</RadzenText>
                    </div>
                    <div class="rz-p-3">
                        <RadzenText TextStyle="TextStyle.Caption" TagName="TagName.Div" style="color: var(--rz-text-tertiary-color)">
                            @game.StartTime.ToString("dd.MM.yyyy HH:mm") — @game.Duration.ToString(@"mm\:ss")
                        </RadzenText>
                        <div class="rz-d-flex rz-align-items-center rz-gap-2 rz-mt-2">
                            @if (winnerColor != TeamColor.NONE)
                            {
                                <RadzenBadge BadgeStyle="BadgeStyle.Primary" style="background: @Util.HTMLColorForTeam(winnerColor); color: white;">
                                    @winnerColor.ToString()
                                </RadzenBadge>
                            }
                            else
                            {
                                <RadzenBadge BadgeStyle="BadgeStyle.Light">Unentschieden</RadzenBadge>
                            }
                            <RadzenBadge BadgeStyle="@(game.EndReason == "Stopped" ? BadgeStyle.Warning : BadgeStyle.Success)">
                                @(game.EndReason == "Stopped" ? "Abgebrochen" : "Abgeschlossen")
                            </RadzenBadge>
                        </div>
                        <div class="rz-d-flex rz-gap-3 rz-mt-3">
                            @foreach (var team in _gameTeamsByGame.GetValueOrDefault(game.Id, new()))
                            {
                                var barColor = Util.HTMLColorForTeam((TeamColor)team.TeamColor);
                                <div style="flex:1;">
                                    <RadzenText TextStyle="TextStyle.Caption" TagName="TagName.Div">
                                        @team.TeamName (@team.Side)
                                    </RadzenText>
                                    <div style="background:var(--rz-base-200); border-radius:4px; overflow:hidden; height:8px;">
                                        <div style="width:@(GetScorePercent(team))%; background:@barColor; height:100%;"></div>
                                    </div>
                                    <RadzenText TextStyle="TextStyle.Caption" TagName="TagName.Div" style="text-align:right;">
                                        @team.FinalScore
                                    </RadzenText>
                                </div>
                            }
                        </div>
                    </div>
                </RadzenCard>
            </RadzenColumn>
        }
    </RadzenRow>
}
```

- [ ] **Step 2: Write GameHistoryPage.razor.cs**

```csharp
using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services;
using OWLServer.Services.Interfaces;
using Radzen;
using Radzen.Blazor;

namespace OWLServer.Components.Pages.AdminPages;

public partial class GameHistoryPage : IDisposable
{
    [Inject] private IExternalTriggerService ExternalTriggerService { get; set; } = null!;
    [Inject] private IGameHistoryService GameHistoryService { get; set; } = null!;
    [Inject] private DialogService DialogService { get; set; } = null!;

    private List<GameHistory> _allGames = new();
    private List<GameHistory> _filteredGames = new();
    private Dictionary<int, List<GameHistoryTeam>> _gameTeamsByGame = new();

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private int? _filterGameMode;
    private int? _filterWinner;
    private string _sortOrder = "newest";

    private Action _stateChangedHandler = null!;

    private readonly List<(string Text, int Value)> _gameModeOptions = new()
    {
        ("Conquest", 2),
        ("TeamDeathmatch", 1),
        ("Timer", 3),
        ("ChainBreak", 4)
    };

    private readonly List<(string Text, int? Value)> _winnerOptions = new()
    {
        ("Blau", (int)TeamColor.BLUE),
        ("Rot", (int)TeamColor.RED),
        ("Unentschieden", (int)TeamColor.NONE)
    };

    private readonly List<(string Text, string Value)> _sortOptions = new()
    {
        ("Neueste zuerst", "newest"),
        ("Alteste zuerst", "oldest"),
        ("Langste Dauer", "longest")
    };

    protected override void OnInitialized()
    {
        _stateChangedHandler = () => InvokeAsync(StateHasChanged);
        ExternalTriggerService.StateHasChangedAction += _stateChangedHandler;
        LoadGames();
    }

    private void LoadGames()
    {
        _allGames = GameHistoryService.GetAllGames();
        foreach (var game in _allGames)
        {
            _gameTeamsByGame[game.Id] = GameHistoryService.GetGameTeams(game.Id);
        }
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = _allGames.AsEnumerable();

        if (_dateFrom.HasValue)
            query = query.Where(g => g.StartTime.Date >= _dateFrom.Value.Date);
        if (_dateTo.HasValue)
            query = query.Where(g => g.StartTime.Date <= _dateTo.Value.Date);
        if (_filterGameMode.HasValue)
            query = query.Where(g => g.GameMode == _filterGameMode.Value);
        if (_filterWinner.HasValue)
            query = query.Where(g => g.Winner == _filterWinner.Value);

        query = _sortOrder switch
        {
            "oldest" => query.OrderBy(g => g.StartTime),
            "longest" => query.OrderByDescending(g => g.Duration),
            _ => query.OrderByDescending(g => g.StartTime)
        };

        _filteredGames = query.ToList();
    }

    private void OnFilterChanged() => ApplyFilters();

    private double GetScorePercent(GameHistoryTeam team)
    {
        var teams = _gameTeamsByGame.GetValueOrDefault(team.GameHistoryId, new());
        int max = teams.Max(t => t.FinalScore);
        if (max == 0) return 0;
        return (double)team.FinalScore / max * 100;
    }

    private async Task OpenDetail(int gameId)
    {
        var game = GameHistoryService.GetGame(gameId);
        var teams = GameHistoryService.GetGameTeams(gameId);
        var towers = GameHistoryService.GetGameTowers(gameId);
        var snapshot = GameHistoryService.GetGameSnapshot(gameId);

        var parameters = new Dictionary<string, object>
        {
            { "Game", game! },
            { "Teams", teams },
            { "Towers", towers },
            { "Snapshot", snapshot }
        };

        await DialogService.OpenAsync<GameHistoryDetail>("Spieldetails", parameters,
            new DialogOptions { Width = "800px", Height = "90vh", ShowTitle = true, Draggable = true });
    }

    public void Dispose()
    {
        ExternalTriggerService.StateHasChangedAction -= _stateChangedHandler;
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 3: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryPage.razor OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryPage.razor.cs
git commit -m "feat: add GameHistoryPage Blazor component"
```

---

### Task 8: Create GameHistoryDetail dialog component

**Files:**
- Create: `OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryDetail.razor`

- [ ] **Step 1: Write GameHistoryDetail.razor**

```razor
@using OWLServer.Models
@using OWLServer.Services
@using Radzen.Blazor

@if (Game == null)
{
    <RadzenText>Keine Daten</RadzenText>
}
else
{
    var winnerColor = (TeamColor)Game.Winner;
    var modeName = ((GameMode)Game.GameMode).ToString();

    <div class="rz-p-3">

        <RadzenRow Gap="16px" class="mb-3">
            <RadzenColumn Size="12" SizeMD="6">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Spielmodus</RadzenText>
                <RadzenText TagName="TagName.Div">@modeName</RadzenText>
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="6">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Datum</RadzenText>
                <RadzenText TagName="TagName.Div">@Game.StartTime.ToString("dd.MM.yyyy HH:mm")</RadzenText>
            </RadzenColumn>
        </RadzenRow>
        <RadzenRow Gap="16px" class="mb-3">
            <RadzenColumn Size="12" SizeMD="6">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Dauer</RadzenText>
                <RadzenText TagName="TagName.Div">@Game.Duration.ToString(@"mm\:ss")</RadzenText>
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="6">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Status</RadzenText>
                <RadzenBadge BadgeStyle="@(Game.EndReason == "Stopped" ? BadgeStyle.Warning : BadgeStyle.Success)">
                    @(Game.EndReason == "Stopped" ? "Abgebrochen" : "Abgeschlossen")
                </RadzenBadge>
            </RadzenColumn>
        </RadzenRow>

        @if (winnerColor != TeamColor.NONE)
        {
            <div class="mb-3">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Gewinner</RadzenText>
                <RadzenBadge BadgeStyle="BadgeStyle.Primary" style="background: @Util.HTMLColorForTeam(winnerColor); color: white; font-size: 1.1em;">
                    @winnerColor.ToString()
                </RadzenBadge>
            </div>
        }
        else
        {
            <div class="mb-3">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Gewinner</RadzenText>
                <RadzenBadge BadgeStyle="BadgeStyle.Light" style="font-size: 1.1em;">Unentschieden</RadzenBadge>
            </div>
        }

        <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4" class="rz-mt-4">Teams</RadzenText>
        <RadzenRow Gap="16px" class="mb-3">
            @foreach (var team in Teams)
            {
                <RadzenColumn Size="6">
                    <RadzenCard Class="rz-p-3" Style="border-left: 4px solid @Util.HTMLColorForTeam((TeamColor)team.TeamColor);">
                        <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">
                            @team.TeamName (@team.Side)
                        </RadzenText>
                        <RadzenText TextStyle="TextStyle.Body2" TagName="TagName.Div">Punkte: @team.FinalScore</RadzenText>
                        <RadzenText TextStyle="TextStyle.Body2" TagName="TagName.Div">Tode: @team.Deaths</RadzenText>
                        <RadzenText TextStyle="TextStyle.Body2" TagName="TagName.Div">Türme: @team.TowersControlled</RadzenText>
                    </RadzenCard>
                </RadzenColumn>
            }
        </RadzenRow>

        @if (Towers.Count > 0)
        {
            <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4" class="rz-mt-4">Türme</RadzenText>
            <RadzenDataGrid Data="@Towers" TItem="GameHistoryTower" AllowPaging="false" Density="Density.Compact">
                <Columns>
                    <RadzenDataGridColumn TItem="GameHistoryTower" Property="DisplayLetter" Title="Turm" />
                    <RadzenDataGridColumn TItem="GameHistoryTower" Property="MacAddress" Title="MAC" />
                    <RadzenDataGridColumn TItem="GameHistoryTower" Title="Farbe" Context="tower">
                        <Template>
                            <span style="display:inline-block; width:16px; height:16px; border-radius:50%; background:@Util.HTMLColorForTeam((TeamColor)tower.FinalColor); vertical-align:middle;"></span>
                        </Template>
                    </RadzenDataGridColumn>
                    <RadzenDataGridColumn TItem="GameHistoryTower" Property="Captures" Title="Eroberungen" />
                </Columns>
            </RadzenDataGrid>
        }
        
        @if (Snapshot != null && !string.IsNullOrEmpty(Snapshot.SnapshotJSON))
        {
            <RadzenPanel AllowCollapse="true" Text="Konfiguration (Snapshot)" class="rz-mt-4">
                <pre style="max-height:400px; overflow:auto; font-size:0.8em; background:var(--rz-base-100); padding:1em; border-radius:4px;">
                    <code>@Snapshot.SnapshotJSON</code>
                </pre>
            </RadzenPanel>
        }
    </div>
}

@code {
    [Parameter] public GameHistory Game { get; set; } = null!;
    [Parameter] public List<GameHistoryTeam> Teams { get; set; } = new();
    [Parameter] public List<GameHistoryTower> Towers { get; set; } = new();
    [Parameter] public GameHistorySnapshot? Snapshot { get; set; }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryDetail.razor
git commit -m "feat: add GameHistoryDetail dialog component"
```

---

### Task 9: Add Game History tab to AdminStartPage

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/AdminStartPage.razor`

- [ ] **Step 1: Add the tab**

Add a new `RadzenTabsItem` after the "Tower-Einstellungen" tab (after line 21) and before "Map-Einstellungen":

```razor
        <RadzenTabsItem Text="Spielverlauf">
            <GameHistoryPage/>
        </RadzenTabsItem>
```

The full file will be:

```razor
@page "/Admin"
@using OWLServer.Services
@inject IExternalTriggerService ExternalTriggerService
@inject IGameStateService GameStateService

<PageTitle>Admin</PageTitle>

<RadzenTabs TabPosition="TabPosition.Top" >
    <Tabs>
        <RadzenTabsItem Text="@(GameStateService.CurrentGame?.IsRunning == true ? "Spiel-Übersicht" : "Startseite")">
            <GameControlDashboard/>
        </RadzenTabsItem>
        <RadzenTabsItem Text="Spiel-Einstellungen">
            <AdminPanel/>
        </RadzenTabsItem>
        <RadzenTabsItem Text="Sounds">
            <SoundTest></SoundTest>
        </RadzenTabsItem>
        <RadzenTabsItem Text="Tower-Einstellungen">
            <TowerConfig/>
        </RadzenTabsItem>
        <RadzenTabsItem Text="Spielverlauf">
            <GameHistoryPage/>
        </RadzenTabsItem>
        <RadzenTabsItem Text="Map-Einstellungen">
            <MapTests></MapTests>
        </RadzenTabsItem>
    </Tabs>
</RadzenTabs>
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/AdminStartPage.razor
git commit -m "feat: add GameHistory tab to AdminStartPage"
```

---

### Task 10: Final build verification

**Files:**
- None (verification only)

- [ ] **Step 1: Full build**

Run: `dotnet build --project OWLServer/OWLServer`
Expected: Build succeeds with zero errors.

- [ ] **Step 2: Review overall changes**

```bash
git diff --stat HEAD~6
```

- [ ] **Step 3: Commit final verification note**

```bash
git commit --allow-empty -m "verify: full build passes for game history feature"
```
