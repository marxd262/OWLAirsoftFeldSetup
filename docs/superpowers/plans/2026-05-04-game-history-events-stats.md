# Game History Events & Statistics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add per-event game recording (tower captures, deaths, button presses, points), a timeline view per game, and cross-game statistics dashboard with trend charts.

**Architecture:** Events collected in-memory during play via subscriptions to `KlickerPressedAction`, `TowerPressedAction`, and `StateHasChangedAction`, batch-flushed to a new `GameHistoryEvent` table every 5 seconds. Three derived columns (`Deaths`, `TowersControlled`, `Captures`) removed from existing model — computed from events at query time. Statistics page uses `RadzenChart` components for all visualizations.

**Tech Stack:** .NET 8, EF Core 9 (SQLite), Radzen.Blazor 10.3.1

---

### Task 1: Create GameHistoryEvent entity and GameEventType enum

**Files:**
- Create: `OWLServer/OWLServer/Models/GameHistoryEvent.cs`
- Create: `OWLServer/OWLServer/Models/GameEventType.cs`

- [ ] **Step 1: Write GameEventType.cs**

```csharp
namespace OWLServer.Models
{
    public enum GameEventType
    {
        TowerCaptured,
        TowerNeutralized,
        Death,
        ButtonPressed,
        ButtonReleased,
        PointsAwarded
    }
}
```

- [ ] **Step 2: Write GameHistoryEvent.cs**

```csharp
namespace OWLServer.Models
{
    public class GameHistoryEvent
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public int EventType { get; set; }
        public int TeamColor { get; set; }
        public string Side { get; set; } = string.Empty;
        public string? TowerLetter { get; set; }
        public int? Value { get; set; }

        public GameHistory? GameHistory { get; set; }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameHistoryEvent.cs OWLServer/OWLServer/Models/GameEventType.cs
git commit -m "feat: add GameHistoryEvent entity and GameEventType enum"
```

---

### Task 2: Remove derived columns from existing entities

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameHistoryTeam.cs`
- Modify: `OWLServer/OWLServer/Models/GameHistoryTower.cs`

- [ ] **Step 1: Remove Deaths and TowersControlled from GameHistoryTeam.cs**

Replace entire file content:

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

        public GameHistory? GameHistory { get; set; }
    }
}
```

- [ ] **Step 2: Remove Captures from GameHistoryTower.cs**

Replace entire file content:

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

        public GameHistory? GameHistory { get; set; }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameHistoryTeam.cs OWLServer/OWLServer/Models/GameHistoryTower.cs
git commit -m "refactor: remove derived Deaths/TowersControlled/Captures columns"
```

---

### Task 3: Update DatabaseContext for GameHistoryEvent

**Files:**
- Modify: `OWLServer/OWLServer/Context/DatabaseContext.cs`

- [ ] **Step 1: Read the current file first at** `OWLServer/OWLServer/Context/DatabaseContext.cs`

- [ ] **Step 2: Add DbSet**

Add after the existing `GameHistorySnapshots` DbSet:
```csharp
public DbSet<GameHistoryEvent> GameHistoryEvents { get; set; }
```

- [ ] **Step 3: Add OnModelCreating configuration**

Add before the closing `}` of `OnModelCreating`:
```csharp
builder.Entity<GameHistoryEvent>(e =>
{
    e.HasKey(ge => ge.Id);
    e.HasOne(ge => ge.GameHistory)
     .WithMany()
     .HasForeignKey(ge => ge.GameHistoryId)
     .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 4: Add raw SQL fallback in constructor**

Add after the `GameHistorySnapshots` raw SQL block:
```csharp
Database.ExecuteSqlRaw(
    "CREATE TABLE IF NOT EXISTS \"GameHistoryEvents\" (" +
    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistoryEvents\" PRIMARY KEY AUTOINCREMENT," +
    "    \"GameHistoryId\" INTEGER NOT NULL," +
    "    \"Timestamp\" TEXT NOT NULL," +
    "    \"EventType\" INTEGER NOT NULL," +
    "    \"TeamColor\" INTEGER NOT NULL," +
    "    \"Side\" TEXT NOT NULL DEFAULT ''," +
    "    \"TowerLetter\" TEXT NULL," +
    "    \"Value\" INTEGER NULL," +
    "    CONSTRAINT \"FK_GameHistoryEvents_GameHistories_GameHistoryId\" " +
    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
    ");");
```

- [ ] **Step 5: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 6: Commit**

```bash
git add OWLServer/OWLServer/Context/DatabaseContext.cs
git commit -m "feat: register GameHistoryEvent in DatabaseContext"
```

---

### Task 4: Update IGameHistoryService interface

**Files:**
- Modify: `OWLServer/OWLServer/Services/Interfaces/IGameHistoryService.cs`

- [ ] **Step 1: Replace file content**

```csharp
using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services.Interfaces
{
    public interface IGameHistoryService
    {
        int? CurrentGameId { get; }
        string EndReason { get; set; }
        void RecordGameStart(GameMode gameMode, Dictionary<string, Tower> towers,
            Dictionary<TeamColor, TeamBase> teams, TeamColor teamInWald);
        void RecordGameEnd(IGameModeBase? currentGame, Dictionary<string, Tower> towers);

        List<GameHistory> GetAllGames();
        GameHistory? GetGame(int id);
        List<GameHistoryTeam> GetGameTeams(int gameHistoryId);
        List<GameHistoryTower> GetGameTowers(int gameHistoryId);
        GameHistorySnapshot? GetGameSnapshot(int gameHistoryId);

        List<GameHistoryEvent> GetGameEvents(int gameHistoryId);
        Dictionary<int, int> GetDeathsPerMinute(int gameHistoryId);
        Dictionary<string, int> GetTowerContestRanking(int gameHistoryId);
        List<(DateTimeOffset Time, int BlueScore, int RedScore)> GetScoreTimeline(int gameHistoryId);

        List<GameHistory> GetGamesByDateRange(DateTime from, DateTime to);
        Dictionary<string, int> GetWinRateByMode(DateTime? from, DateTime? to);
        Dictionary<string, int> GetWinRateBySide(DateTime? from, DateTime? to);
        List<(DateTime Day, double AvgDuration)> GetAvgDurationByDay(DateTime from, DateTime to);
        List<(DateTime Day, int BlueDeaths, int RedDeaths)> GetDeathsByDay(DateTime from, DateTime to);
        Dictionary<string, int> GetGlobalTowerHotspots(DateTime? from, DateTime? to);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Services/Interfaces/IGameHistoryService.cs
git commit -m "feat: add event and statistics query methods to IGameHistoryService"
```

---

### Task 5: Update GameHistoryService with event logging and queries

**Files:**
- Modify: `OWLServer/OWLServer/Services/GameHistoryService.cs`

This is the largest task. The full file content replacing the existing one:

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

        public int? CurrentGameId { get; private set; }
        public string EndReason { get; set; } = "Completed";

        private List<GameHistoryEvent> _pendingEvents = new();
        private Timer? _flushTimer;
        private Dictionary<string, TeamColor> _lastKnownTowerColors = new();
        private Dictionary<TeamColor, int> _lastKnownScores = new();
        private Dictionary<string, bool> _lastKnownButtonState = new();
        private Dictionary<string, Tower>? _activeTowers;
        private Dictionary<TeamColor, TeamBase>? _activeTeams;
        private TeamColor _teamInWald;
        private IGameModeBase? _activeGame;

        public GameHistoryService(
            IDbContextFactory<DatabaseContext> dbFactory,
            IExternalTriggerService externalTriggerService)
        {
            _dbFactory = dbFactory;
            _externalTriggerService = externalTriggerService;
        }

        public void RecordGameStart(GameMode gameMode, Dictionary<string, Tower> towers,
            Dictionary<TeamColor, TeamBase> teams, TeamColor teamInWald)
        {
            EndReason = "Completed";
            _pendingEvents = new();
            _lastKnownTowerColors = new();
            _lastKnownScores = new();
            _lastKnownButtonState = new();
            _activeTowers = towers;
            _activeTeams = teams;
            _teamInWald = teamInWald;

            foreach (var kvp in towers)
            {
                _lastKnownTowerColors[kvp.Key] = kvp.Value.CurrentColor;
                _lastKnownButtonState[kvp.Key] = kvp.Value.IsPressed;
            }

            using var db = _dbFactory.CreateDbContext();

            var history = new GameHistory
            {
                GameMode = (int)gameMode,
                StartTime = DateTime.Now,
                Winner = (int)TeamColor.NONE
            };

            db.GameHistories.Add(history);
            db.SaveChanges();
            CurrentGameId = history.Id;

            foreach (var kvp in towers)
            {
                var tower = kvp.Value;
                db.GameHistoryTowers.Add(new GameHistoryTower
                {
                    GameHistoryId = history.Id,
                    MacAddress = tower.MacAddress,
                    DisplayLetter = tower.DisplayLetter,
                    FinalColor = (int)TeamColor.NONE
                });
            }

            foreach (var kvp in teams)
            {
                var team = kvp.Value;
                string side = kvp.Key == teamInWald ? "Wald" : "Stadt";
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
                SnapshotJSON = BuildSnapshotJSON(gameMode, towers, teams)
            };
            db.GameHistorySnapshots.Add(snapshot);

            db.SaveChanges();

            _externalTriggerService.StateHasChangedAction += OnStateChanged;
            _externalTriggerService.KlickerPressedAction += OnKlickerPressed;
            _externalTriggerService.TowerPressedAction += OnTowerPressed;

            _flushTimer = new Timer(_ => FlushEvents(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public void RecordGameEnd(IGameModeBase? currentGame, Dictionary<string, Tower> towers)
        {
            _activeGame = currentGame;
            _externalTriggerService.StateHasChangedAction -= OnStateChanged;
            _externalTriggerService.KlickerPressedAction -= OnKlickerPressed;
            _externalTriggerService.TowerPressedAction -= OnTowerPressed;
            _flushTimer?.Dispose();
            _flushTimer = null;

            FlushEvents();

            if (CurrentGameId == null) return;

            using var db = _dbFactory.CreateDbContext();

            var history = db.GameHistories.Find(CurrentGameId.Value);
            if (history == null) return;

            history.EndTime = DateTime.Now;
            history.Duration = history.EndTime.Value - history.StartTime;
            history.Winner = (int)(currentGame?.GetWinner ?? TeamColor.NONE);
            history.EndReason = EndReason;

            var teams = db.GameHistoryTeams.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var team in teams)
            {
                var teamColor = (TeamColor)team.TeamColor;
                team.FinalScore = currentGame?.GetDisplayPoints(teamColor) ?? 0;
            }

            var towerRecords = db.GameHistoryTowers.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var tr in towerRecords)
            {
                if (towers.TryGetValue(tr.MacAddress, out var tower))
                {
                    tr.FinalColor = (int)tower.CurrentColor;
                }
            }

            db.SaveChanges();

            CurrentGameId = null;
            _activeTowers = null;
            _activeTeams = null;
            _activeGame = null;
            _pendingEvents = new();
            _lastKnownTowerColors = new();
            _lastKnownScores = new();
            _lastKnownButtonState = new();
        }

        private void OnStateChanged()
        {
            if (CurrentGameId == null || _activeTowers == null) return;

            foreach (var kvp in _activeTowers)
            {
                var tower = kvp.Value;
                var mac = kvp.Key;

                if (_lastKnownTowerColors.TryGetValue(mac, out var lastColor))
                {
                    if (lastColor != tower.CurrentColor)
                    {
                        if (tower.CurrentColor == TeamColor.BLUE || tower.CurrentColor == TeamColor.RED)
                        {
                            AddEvent(GameEventType.TowerCaptured, tower.CurrentColor, tower.DisplayLetter);
                        }
                        else if (tower.CurrentColor == TeamColor.NONE)
                        {
                            var prevTeam = lastColor == TeamColor.BLUE || lastColor == TeamColor.RED ? lastColor : TeamColor.NONE;
                            AddEvent(GameEventType.TowerNeutralized, prevTeam, tower.DisplayLetter);
                        }
                        _lastKnownTowerColors[mac] = tower.CurrentColor;
                    }
                }
                else
                {
                    _lastKnownTowerColors[mac] = tower.CurrentColor;
                }

                if (_lastKnownButtonState.TryGetValue(mac, out var wasPressed))
                {
                    if (wasPressed && !tower.IsPressed)
                    {
                        AddEvent(GameEventType.ButtonReleased, tower.PressedByColor, tower.DisplayLetter);
                    }
                }
                _lastKnownButtonState[mac] = tower.IsPressed;
            }

            if (_activeGame != null)
            {
                foreach (var kvp in _lastKnownScores)
                {
                    var teamColor = kvp.Key;
                    int currentScore = _activeGame.GetDisplayPoints(teamColor);
                    int lastScore = kvp.Value;
                    if (currentScore != lastScore)
                    {
                        int delta = currentScore - lastScore;
                        AddEvent(GameEventType.PointsAwarded, teamColor, null, delta);
                        _lastKnownScores[teamColor] = currentScore;
                    }
                }
            }
        }

        private void OnKlickerPressed(object? sender, KlickerEventArgs args)
        {
            if (CurrentGameId == null) return;
            AddEvent(GameEventType.Death, args.TeamColor, null);
        }

        private void OnTowerPressed(object? sender, TowerEventArgs args)
        {
            if (CurrentGameId == null || _activeTowers == null) return;
            string? letter = null;
            if (_activeTowers.TryGetValue(args.TowerId, out var tower))
                letter = tower.DisplayLetter;
            AddEvent(GameEventType.ButtonPressed, args.TeamColor, letter);
        }

        private void AddEvent(GameEventType type, TeamColor teamColor, string? towerLetter, int? value = null)
        {
            string side = teamColor == _teamInWald ? "Wald" : "Stadt";
            _pendingEvents.Add(new GameHistoryEvent
            {
                GameHistoryId = CurrentGameId!.Value,
                Timestamp = DateTimeOffset.Now,
                EventType = (int)type,
                TeamColor = (int)teamColor,
                Side = side,
                TowerLetter = towerLetter,
                Value = value
            });
        }

        private void FlushEvents()
        {
            if (_pendingEvents.Count == 0) return;

            using var db = _dbFactory.CreateDbContext();
            db.GameHistoryEvents.AddRange(_pendingEvents);
            db.SaveChanges();
            _pendingEvents.Clear();
        }

        private string BuildSnapshotJSON(GameMode gameMode, Dictionary<string, Tower> towers,
            Dictionary<TeamColor, TeamBase> teams)
        {
            var snapshot = new
            {
                Timestamp = DateTime.Now,
                GameMode = gameMode.ToString(),
                Towers = towers.Values.Select(t => new
                {
                    t.MacAddress,
                    t.Name,
                    t.DisplayLetter,
                    t.TimeToCaptureInSeconds,
                    t.Multiplier,
                    t.ResetsAfterInSeconds,
                    Location = t.Location != null ? new { t.Location.Top, t.Location.Left } : null
                }).ToList(),
                Teams = teams.Select(kvp => new
                {
                    TeamColor = kvp.Key.ToString(),
                    kvp.Value.Name,
                    Side = kvp.Key == _teamInWald ? "Wald" : "Stadt"
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

        public List<GameHistoryEvent> GetGameEvents(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }

        public Dictionary<int, int> GetDeathsPerMinute(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            var game = db.GameHistories.Find(gameHistoryId);
            if (game == null) return new();

            return db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId && e.EventType == (int)GameEventType.Death)
                .AsEnumerable()
                .GroupBy(e => (int)((e.Timestamp - game.StartTime).TotalMinutes))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public Dictionary<string, int> GetTowerContestRanking(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId && e.EventType == (int)GameEventType.TowerCaptured)
                .AsEnumerable()
                .GroupBy(e => e.TowerLetter ?? "")
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public List<(DateTimeOffset Time, int BlueScore, int RedScore)> GetScoreTimeline(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            var events = db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId && e.EventType == (int)GameEventType.PointsAwarded)
                .OrderBy(e => e.Timestamp)
                .ToList();

            int blue = 0, red = 0;
            var timeline = new List<(DateTimeOffset, int, int)>();

            foreach (var e in events)
            {
                if ((TeamColor)e.TeamColor == TeamColor.BLUE) blue += e.Value ?? 0;
                else red += e.Value ?? 0;
                timeline.Add((e.Timestamp, blue, red));
            }

            return timeline;
        }

        public List<GameHistory> GetGamesByDateRange(DateTime from, DateTime to)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories
                .Where(g => g.StartTime >= from && g.StartTime <= to)
                .OrderBy(g => g.StartTime)
                .ToList();
        }

        public Dictionary<string, int> GetWinRateByMode(DateTime? from, DateTime? to)
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.GameHistories.AsQueryable();
            if (from.HasValue) query = query.Where(g => g.StartTime >= from.Value);
            if (to.HasValue) query = query.Where(g => g.StartTime <= to.Value);

            return query
                .AsEnumerable()
                .GroupBy(g => ((GameMode)g.GameMode).ToString())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public Dictionary<string, int> GetWinRateBySide(DateTime? from, DateTime? to)
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.GameHistories.AsQueryable();
            if (from.HasValue) query = query.Where(g => g.StartTime >= from.Value);
            if (to.HasValue) query = query.Where(g => g.StartTime <= to.Value);

            var games = query.ToList();
            var result = new Dictionary<string, int> { { "Wald", 0 }, { "Stadt", 0 } };

            foreach (var game in games)
            {
                var winner = (TeamColor)game.Winner;
                if (winner == TeamColor.NONE) continue;

                var teams = db.GameHistoryTeams.Where(t => t.GameHistoryId == game.Id).ToList();
                var winnerTeam = teams.FirstOrDefault(t => (TeamColor)t.TeamColor == winner);
                if (winnerTeam != null)
                {
                    result[winnerTeam.Side] = result.GetValueOrDefault(winnerTeam.Side, 0) + 1;
                }
            }

            return result;
        }

        public List<(DateTime Day, double AvgDuration)> GetAvgDurationByDay(DateTime from, DateTime to)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories
                .Where(g => g.StartTime >= from && g.StartTime <= to && g.EndTime != null)
                .AsEnumerable()
                .GroupBy(g => g.StartTime.Date)
                .Select(g => (g.Key, g.Average(x => x.Duration.TotalMinutes)))
                .OrderBy(x => x.Key)
                .ToList();
        }

        public List<(DateTime Day, int BlueDeaths, int RedDeaths)> GetDeathsByDay(DateTime from, DateTime to)
        {
            using var db = _dbFactory.CreateDbContext();
            var gameIds = db.GameHistories
                .Where(g => g.StartTime >= from && g.StartTime <= to)
                .Select(g => g.Id)
                .ToList();

            var events = db.GameHistoryEvents
                .Where(e => gameIds.Contains(e.GameHistoryId) && e.EventType == (int)GameEventType.Death)
                .ToList();

            var gameDates = db.GameHistories
                .Where(g => gameIds.Contains(g.Id))
                .ToDictionary(g => g.Id, g => g.StartTime.Date);

            return events
                .GroupBy(e => gameDates.GetValueOrDefault(e.GameHistoryId, DateTime.MinValue))
                .Select(g => (
                    g.Key,
                    g.Count(e => (TeamColor)e.TeamColor == TeamColor.BLUE),
                    g.Count(e => (TeamColor)e.TeamColor == TeamColor.RED)
                ))
                .OrderBy(x => x.Key)
                .ToList();
        }

        public Dictionary<string, int> GetGlobalTowerHotspots(DateTime? from, DateTime? to)
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.GameHistoryEvents
                .Where(e => e.EventType == (int)GameEventType.TowerCaptured && e.TowerLetter != null);

            if (from.HasValue)
            {
                var gameIdsFrom = db.GameHistories.Where(g => g.StartTime >= from.Value).Select(g => g.Id).ToList();
                query = query.Where(e => gameIdsFrom.Contains(e.GameHistoryId));
            }
            if (to.HasValue)
            {
                var gameIdsTo = db.GameHistories.Where(g => g.StartTime <= to.Value).Select(g => g.Id).ToList();
                query = query.Where(e => gameIdsTo.Contains(e.GameHistoryId));
            }

            return query
                .AsEnumerable()
                .GroupBy(e => e.TowerLetter!)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public void Dispose()
        {
            _flushTimer?.Dispose();
            _externalTriggerService.StateHasChangedAction -= OnStateChanged;
            _externalTriggerService.KlickerPressedAction -= OnKlickerPressed;
            _externalTriggerService.TowerPressedAction -= OnTowerPressed;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Services/GameHistoryService.cs
git commit -m "feat: add event logging, batch flush, and statistics queries to GameHistoryService"
```

---

### Task 6: Update GameHistoryDetail with tabs, timeline, and stats

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryDetail.razor`
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryPage.razor.cs` (pass events parameter)

- [ ] **Step 1: Update GameHistoryPage.razor.cs — OpenDetail method**

Change the `OpenDetail` method to also fetch events:

In `GameHistoryPage.razor.cs`, replace the `OpenDetail` method:
```csharp
private async Task OpenDetail(int gameId)
{
    var game = GameHistoryService.GetGame(gameId);
    var teams = GameHistoryService.GetGameTeams(gameId);
    var towers = GameHistoryService.GetGameTowers(gameId);
    var snapshot = GameHistoryService.GetGameSnapshot(gameId);
    var events = GameHistoryService.GetGameEvents(gameId);
    var deathsPerMinute = GameHistoryService.GetDeathsPerMinute(gameId);
    var towerContest = GameHistoryService.GetTowerContestRanking(gameId);
    var scoreTimeline = GameHistoryService.GetScoreTimeline(gameId);

    var parameters = new Dictionary<string, object>
    {
        { "Game", game! },
        { "Teams", teams },
        { "Towers", towers },
        { "Snapshot", snapshot },
        { "EventData", events },
        { "DeathsPerMinute", deathsPerMinute },
        { "TowerContest", towerContest },
        { "ScoreTimeline", scoreTimeline }
    };

    await DialogService.OpenAsync<GameHistoryDetail>("Spieldetails", parameters,
        new DialogOptions { Width = "900px", Height = "95vh", ShowTitle = true, Draggable = true });
}
```

- [ ] **Step 2: Rewrite GameHistoryDetail.razor**

Replace entire file content:

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
    var modeName = GetGameModeDisplayName(Game.GameMode);

    <RadzenTabs TabPosition="TabPosition.Top">
        <Tabs>
            <RadzenTabsItem Text="Übersicht">
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
                            var teamDeaths = EventData.Count(e => e.EventType == (int)GameEventType.Death && (TeamColor)e.TeamColor == (TeamColor)team.TeamColor);
                            var teamTowers = Towers.Count(t => (TeamColor)t.FinalColor == (TeamColor)team.TeamColor);
                            <RadzenColumn Size="6">
                                <RadzenCard Class="rz-p-3" Style="border-left: 4px solid @Util.HTMLColorForTeam((TeamColor)team.TeamColor);">
                                    <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">
                                        @team.TeamName (@team.Side)
                                    </RadzenText>
                                    <RadzenText TextStyle="TextStyle.Body2" TagName="TagName.Div">Punkte: @team.FinalScore</RadzenText>
                                    <RadzenText TextStyle="TextStyle.Body2" TagName="TagName.Div">Tode: @teamDeaths</RadzenText>
                                    <RadzenText TextStyle="TextStyle.Body2" TagName="TagName.Div">Türme: @teamTowers</RadzenText>
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
                                <RadzenDataGridColumn TItem="GameHistoryTower" Title="Eroberungen" Context="tower">
                                    <Template>
                                        @EventData.Count(e => e.EventType == (int)GameEventType.TowerCaptured && e.TowerLetter == tower.DisplayLetter)
                                    </Template>
                                </RadzenDataGridColumn>
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
            </RadzenTabsItem>

            <RadzenTabsItem Text="Zeitleiste">
                <div class="rz-p-3">
                    @if (EventData.Count == 0)
                    {
                        <RadzenText TextStyle="TextStyle.Body1" style="color: var(--rz-text-tertiary-color)">Keine Ereignisse</RadzenText>
                    }
                    else
                    {
                        <RadzenDataGrid Data="@EventData" TItem="GameHistoryEvent" AllowPaging="true" PageSize="50" Density="Density.Compact">
                            <Columns>
                                <RadzenDataGridColumn TItem="GameHistoryEvent" Title="Zeit" Context="ev">
                                    <Template>
                                        @((ev.Timestamp - Game.StartTime).ToString(@"mm\:ss"))
                                    </Template>
                                </RadzenDataGridColumn>
                                <RadzenDataGridColumn TItem="GameHistoryEvent" Title="Ereignis" Context="ev">
                                    <Template>
                                        @GetEventIcon(ev)
                                        @GetEventName(ev.EventType)
                                    </Template>
                                </RadzenDataGridColumn>
                                <RadzenDataGridColumn TItem="GameHistoryEvent" Title="Team" Context="ev">
                                    <Template>
                                        <RadzenBadge BadgeStyle="BadgeStyle.Primary" style="background: @Util.HTMLColorForTeam((TeamColor)ev.TeamColor); color: white;">
                                            @ev.Side
                                        </RadzenBadge>
                                    </Template>
                                </RadzenDataGridColumn>
                                <RadzenDataGridColumn TItem="GameHistoryEvent" Property="TowerLetter" Title="Turm" />
                                <RadzenDataGridColumn TItem="GameHistoryEvent" Title="Wert" Context="ev">
                                    <Template>
                                        @(ev.Value.HasValue ? ev.Value.Value.ToString() : "-")
                                    </Template>
                                </RadzenDataGridColumn>
                            </Columns>
                        </RadzenDataGrid>

                        <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4" class="rz-mt-4">Ereignisse pro Minute</RadzenText>
                        <RadzenChart style="height:200px;">
                            <RadzenBarSeries Data="@GetEventsPerMinute()" CategoryProperty="Minute" ValueProperty="Count" Title="Ereignisse" />
                            <RadzenCategoryAxis />
                            <RadzenValueAxis />
                        </RadzenChart>
                    }
                </div>
            </RadzenTabsItem>

            <RadzenTabsItem Text="Statistiken">
                <div class="rz-p-3">
                    @if (DeathsPerMinute.Count > 0)
                    {
                        <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4">Tode pro Minute</RadzenText>
                        <RadzenChart style="height:200px;">
                            <RadzenBarSeries Data="@DeathsPerMinute.Select(kvp => new { Minute = kvp.Key, Count = kvp.Value })" CategoryProperty="Minute" ValueProperty="Count" Title="Tode" />
                            <RadzenCategoryAxis />
                            <RadzenValueAxis />
                        </RadzenChart>
                    }

                    @if (TowerContest.Count > 0)
                    {
                        <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4" class="rz-mt-4">Turmeroberungen</RadzenText>
                        <RadzenChart style="height:@(TowerContest.Count * 40 + 40)px;">
                            <RadzenBarSeries Data="@TowerContest.Select(kvp => new { Letter = kvp.Key, Captures = kvp.Value })" CategoryProperty="Letter" ValueProperty="Captures" Title="Eroberungen" />
                            <RadzenCategoryAxis />
                            <RadzenValueAxis />
                        </RadzenChart>
                    }

                    @if (ScoreTimeline.Count > 1)
                    {
                        var chartData = ScoreTimeline.Select(s => new { Minute = (int)(s.Time - Game.StartTime).TotalMinutes, BlueScore = s.BlueScore, RedScore = s.RedScore }).ToList();
                        <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4" class="rz-mt-4">Punkteverlauf</RadzenText>
                        <RadzenChart style="height:250px;">
                            <RadzenLineSeries Data="@chartData" CategoryProperty="Minute" ValueProperty="BlueScore" Title="Blau" LineType="LineType.Solid" />
                            <RadzenLineSeries Data="@chartData" CategoryProperty="Minute" ValueProperty="RedScore" Title="Rot" LineType="LineType.Solid" />
                            <RadzenCategoryAxis />
                            <RadzenValueAxis />
                        </RadzenChart>
                    }

                    @if (DeathsPerMinute.Count == 0 && TowerContest.Count == 0 && ScoreTimeline.Count <= 1)
                    {
                        <RadzenText TextStyle="TextStyle.Body1" style="color: var(--rz-text-tertiary-color)">Keine Statistikdaten verfügbar</RadzenText>
                    }
                </div>
            </RadzenTabsItem>
        </Tabs>
    </RadzenTabs>
}

@code {
    [Parameter] public GameHistory Game { get; set; } = null!;
    [Parameter] public List<GameHistoryTeam> Teams { get; set; } = new();
    [Parameter] public List<GameHistoryTower> Towers { get; set; } = new();
    [Parameter] public GameHistorySnapshot? Snapshot { get; set; }
    [Parameter] public List<GameHistoryEvent> EventData { get; set; } = new();
    [Parameter] public Dictionary<int, int> DeathsPerMinute { get; set; } = new();
    [Parameter] public Dictionary<string, int> TowerContest { get; set; } = new();
    [Parameter] public List<(DateTimeOffset Time, int BlueScore, int RedScore)> ScoreTimeline { get; set; } = new();

    private string GetGameModeDisplayName(int gameMode) => ((GameMode)gameMode) switch
    {
        GameMode.Conquest => "Eroberung",
        GameMode.TeamDeathMatch => "Team Deathmatch",
        GameMode.Timer => "Timer",
        GameMode.ChainBreak => "Kettenbruch",
        _ => ((GameMode)gameMode).ToString()
    };

    private string GetEventIcon(GameHistoryEvent ev) => ((GameEventType)ev.EventType) switch
    {
        GameEventType.TowerCaptured => "🚩",
        GameEventType.TowerNeutralized => "⚪",
        GameEventType.Death => "💀",
        GameEventType.ButtonPressed => "🔘",
        GameEventType.ButtonReleased => "⭕",
        GameEventType.PointsAwarded => "💰",
        _ => "📌"
    };

    private string GetEventName(int eventType) => ((GameEventType)eventType) switch
    {
        GameEventType.TowerCaptured => "Turm erobert",
        GameEventType.TowerNeutralized => "Turm neutralisiert",
        GameEventType.Death => "Tod",
        GameEventType.ButtonPressed => "Knopf gedrückt",
        GameEventType.ButtonReleased => "Knopf losgelassen",
        GameEventType.PointsAwarded => "Punkte vergeben",
        _ => "Unbekannt"
    };

    private List<object> GetEventsPerMinute()
    {
        if (Game == null) return new();
        return EventData
            .GroupBy(e => (int)((e.Timestamp - Game.StartTime).TotalMinutes))
            .Select(g => (object)new { Minute = g.Key, Count = g.Count() })
            .ToList();
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryDetail.razor OWLServer/OWLServer/Components/Pages/AdminPages/GameHistoryPage.razor.cs
git commit -m "feat: add timeline and per-game statistics tabs to GameHistoryDetail"
```

---

### Task 7: Create StatisticsPage component

**Files:**
- Create: `OWLServer/OWLServer/Components/Pages/AdminPages/StatisticsPage.razor`
- Create: `OWLServer/OWLServer/Components/Pages/AdminPages/StatisticsPage.razor.cs`

- [ ] **Step 1: Write StatisticsPage.razor**

```razor
@page "/Admin/Statistics"
@implements IDisposable

<RadzenText TextStyle="TextStyle.H4" TagName="TagName.H2" class="mb-3">Statistiken</RadzenText>

<div class="mb-3">
    <RadzenRow Gap="16px" AlignItems="AlignItems.Center">
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenDatePicker @bind-Value="@_dateFrom" Placeholder="Von Datum" Style="width:100%" />
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenDatePicker @bind-Value="@_dateTo" Placeholder="Bis Datum" Style="width:100%" />
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="2">
            <RadzenButton Text="Anwenden" Click="@LoadStats" ButtonStyle="ButtonStyle.Primary" Style="width:100%" />
        </RadzenColumn>
    </RadzenRow>
</div>

@if (_hasData)
{
    <RadzenRow Gap="16px" class="mb-3">
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenCard Class="rz-p-3 rz-text-align-center">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Spiele gesamt</RadzenText>
                <RadzenText TextStyle="TextStyle.H4" TagName="TagName.Div">@_totalGames</RadzenText>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenCard Class="rz-p-3 rz-text-align-center">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Siege Blau</RadzenText>
                <RadzenText TextStyle="TextStyle.H4" TagName="TagName.Div" style="color: #00b4f1;">@_blueWins</RadzenText>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenCard Class="rz-p-3 rz-text-align-center">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">Siege Rot</RadzenText>
                <RadzenText TextStyle="TextStyle.H4" TagName="TagName.Div" style="color: #fc1911;">@_redWins</RadzenText>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="3">
            <RadzenCard Class="rz-p-3 rz-text-align-center">
                <RadzenText TextStyle="TextStyle.Subtitle2" TagName="TagName.Div">⌀ Dauer</RadzenText>
                <RadzenText TextStyle="TextStyle.H4" TagName="TagName.Div">@_avgDuration.ToString("F0")min</RadzenText>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    <RadzenRow Gap="16px" class="mb-3">
        <RadzenColumn Size="12" SizeMD="6">
            <RadzenCard Class="rz-p-3">
                <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4">Siege nach Spielmodus</RadzenText>
                <RadzenChart style="height:250px;">
                    <RadzenBarSeries Data="@_winRateByMode" CategoryProperty="Mode" ValueProperty="Wins" Title="Siege" />
                    <RadzenCategoryAxis />
                    <RadzenValueAxis />
                </RadzenChart>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="6">
            <RadzenCard Class="rz-p-3">
                <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4">Siege nach Seite</RadzenText>
                <RadzenChart style="height:250px;">
                    <RadzenPieSeries Data="@_winRateBySide" CategoryProperty="Side" ValueProperty="Wins" Title="Siege" />
                </RadzenChart>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    <RadzenRow Gap="16px" class="mb-3">
        <RadzenColumn Size="12" SizeMD="6">
            <RadzenCard Class="rz-p-3">
                <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4">Tode pro Tag</RadzenText>
                <RadzenChart style="height:250px;">
                    <RadzenBarSeries Data="@_deathsByDay" CategoryProperty="Day" ValueProperty="BlueDeaths" Title="Blau" />
                    <RadzenBarSeries Data="@_deathsByDay" CategoryProperty="Day" ValueProperty="RedDeaths" Title="Rot" />
                    <RadzenCategoryAxis />
                    <RadzenValueAxis />
                </RadzenChart>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="6">
            <RadzenCard Class="rz-p-3">
                <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4">Dauer pro Tag</RadzenText>
                <RadzenChart style="height:250px;">
                    <RadzenBarSeries Data="@_avgDurationByDay" CategoryProperty="Day" ValueProperty="Duration" Title="Minuten" />
                    <RadzenCategoryAxis />
                    <RadzenValueAxis />
                </RadzenChart>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    @if (_towerHotspots.Count > 0)
    {
        <RadzenRow Gap="16px" class="mb-3">
            <RadzenColumn Size="12">
                <RadzenCard Class="rz-p-3">
                    <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.H4">Turm Hotspots</RadzenText>
                    <RadzenChart style="height:@(_towerHotspots.Count * 40 + 40)px;">
                        <RadzenBarSeries Data="@_towerHotspots" CategoryProperty="Letter" ValueProperty="Captures" Title="Eroberungen" />
                        <RadzenCategoryAxis />
                        <RadzenValueAxis />
                    </RadzenChart>
                </RadzenCard>
            </RadzenColumn>
        </RadzenRow>
    }
}
else
{
    <RadzenCard Class="rz-p-4 rz-text-align-center">
        <RadzenText TextStyle="TextStyle.Body1" style="color: var(--rz-text-tertiary-color)">
            Keine Spiele im ausgewählten Zeitraum
        </RadzenText>
    </RadzenCard>
}
```

- [ ] **Step 2: Write StatisticsPage.razor.cs**

```csharp
using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services.Interfaces;
using Radzen.Blazor;

namespace OWLServer.Components.Pages.AdminPages;

public partial class StatisticsPage : IDisposable
{
    [Inject] private IExternalTriggerService ExternalTriggerService { get; set; } = null!;
    [Inject] private IGameHistoryService GameHistoryService { get; set; } = null!;

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private bool _hasData;
    private int _totalGames;
    private int _blueWins;
    private int _redWins;
    private double _avgDuration;

    private List<object> _winRateByMode = new();
    private List<object> _winRateBySide = new();
    private List<object> _deathsByDay = new();
    private List<object> _avgDurationByDay = new();
    private List<object> _towerHotspots = new();

    private Action _stateChangedHandler = null!;

    protected override void OnInitialized()
    {
        _stateChangedHandler = () => InvokeAsync(StateHasChanged);
        ExternalTriggerService.StateHasChangedAction += _stateChangedHandler;
        LoadStats();
    }

    private void LoadStats()
    {
        var from = _dateFrom ?? DateTime.MinValue;
        var to = _dateTo ?? DateTime.MaxValue;

        var games = GameHistoryService.GetGamesByDateRange(from, to);
        _hasData = games.Count > 0;
        if (!_hasData) { StateHasChanged(); return; }

        _totalGames = games.Count;
        _blueWins = games.Count(g => (Models.TeamColor)g.Winner == Models.TeamColor.BLUE);
        _redWins = games.Count(g => (Models.TeamColor)g.Winner == Models.TeamColor.RED);
        _avgDuration = games.Where(g => g.EndTime.HasValue).Select(g => g.Duration.TotalMinutes).DefaultIfEmpty(0).Average();

        _winRateByMode = GameHistoryService.GetWinRateByMode(from, to)
            .Select(kvp => (object)new { Mode = kvp.Key, Wins = kvp.Value })
            .ToList();

        _winRateBySide = GameHistoryService.GetWinRateBySide(from, to)
            .Select(kvp => (object)new { Side = kvp.Key, Wins = kvp.Value })
            .ToList();

        _deathsByDay = GameHistoryService.GetDeathsByDay(from, to)
            .Select(d => (object)new { Day = d.Day.ToString("dd.MM"), BlueDeaths = d.BlueDeaths, RedDeaths = d.RedDeaths })
            .ToList();

        _avgDurationByDay = GameHistoryService.GetAvgDurationByDay(from, to)
            .Select(d => (object)new { Day = d.Day.ToString("dd.MM"), Duration = d.AvgDuration })
            .ToList();

        _towerHotspots = GameHistoryService.GetGlobalTowerHotspots(from, to)
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => (object)new { Letter = kvp.Key, Captures = kvp.Value })
            .ToList();

        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ExternalTriggerService.StateHasChangedAction -= _stateChangedHandler;
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/StatisticsPage.razor OWLServer/OWLServer/Components/Pages/AdminPages/StatisticsPage.razor.cs
git commit -m "feat: add StatisticsPage with cross-game charts"
```

---

### Task 8: Add Statistics tab to AdminStartPage

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/AdminStartPage.razor`

- [ ] **Step 1: Add tab**

Add after the "Spielverlauf" tab:
```razor
        <RadzenTabsItem Text="Statistiken">
            <StatisticsPage/>
        </RadzenTabsItem>
```

- [ ] **Step 2: Build**

Run: `dotnet build OWLServer/OWLServer/OWLServer.csproj`

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/AdminStartPage.razor
git commit -m "feat: add Statistics tab to AdminStartPage"
```

---

### Task 9: Full build and test verification

**Files:** none

- [ ] **Step 1: Full solution build**

Run: `dotnet build OWLServer/OWLServer.sln`

- [ ] **Step 2: Run tests**

Run: `dotnet test OWLServer/OWLServer.sln --no-build`

- [ ] **Step 3: Commit**

```bash
git commit --allow-empty -m "verify: full build and tests pass for events and statistics feature"
```
