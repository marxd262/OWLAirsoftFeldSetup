# Game History Events & Statistics — Design Spec

**Date:** 2026-05-04
**Status:** In Review

## Overview

Extend the Game History feature with per-event recording during games (tower captures, deaths, button actions, points), a detailed timeline view per game, and cross-game statistics with trends over time. Events are collected in-memory during play and batch-flushed to the database every 5 seconds.

## Data Model

### New Table: GameHistoryEvent

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | auto-increment |
| `GameHistoryId` | int FK | cascade delete to GameHistory |
| `Timestamp` | DateTimeOffset | when the event occurred |
| `EventType` | int | enum (see below) |
| `TeamColor` | int | TeamColor enum: BLUE or RED |
| `Side` | string | "Wald" or "Stadt" |
| `TowerLetter` | string? | e.g. "A", "B" — null for non-tower events |
| `Value` | int? | points awarded / multiplier — null for non-point events |

### EventType Enum

```csharp
enum GameEventType
{
    TowerCaptured,
    TowerNeutralized,
    Death,
    ButtonPressed,
    ButtonReleased,
    PointsAwarded
}
```

### Removed Columns (derivable from events)

**GameHistoryTeam:**
- Drop `Deaths` → `COUNT(GameHistoryEvent WHERE EventType=Death AND TeamColor=X AND GameHistoryId=Y)`
- Drop `TowersControlled` → `COUNT(GameHistoryTower WHERE GameHistoryId=Y AND FinalColor=X)`

**GameHistoryTower:**
- Drop `Captures` → `COUNT(GameHistoryEvent WHERE EventType=TowerCaptured AND TowerLetter=X AND GameHistoryId=Y)`

### No New Aggregate Tables

Game days, cross-game stats, and trends are all derived at query time via SQL/EF grouping and filtering. No pre-computation storage needed.

---

## Service: GameHistoryService (Extended)

### New Dependencies

None beyond existing (`IDbContextFactory<DatabaseContext>`, `IExternalTriggerService`).

### New In-Memory State

```csharp
private List<GameHistoryEvent> _pendingEvents = new();
private Timer? _flushTimer;
private Dictionary<string, TeamColor> _lastKnownTowerColors = new();  // already exists
private Dictionary<TeamColor, int> _lastKnownScores = new();           // NEW: track score deltas
private Dictionary<string, bool> _lastKnownButtonState = new();        // NEW: track button releases
```

### Event Subscriptions (added in RecordGameStart, removed in RecordGameEnd)

| Subscription | Event Recorded |
|---|---|
| `KlickerPressedAction` | Death event |
| `TowerPressedAction` | ButtonPressed event |
| `StateHasChangedAction` | TowerCaptured, TowerNeutralized, ButtonReleased, PointsAwarded |

### Event Detection Logic (on StateHasChangedAction)

For each tower in `_activeTowers`:
1. `CurrentColor != last color` → if new is BLUE/RED → `TowerCaptured`; if new is NONE → `TowerNeutralized`
2. `IsPressed` was true, now false → `ButtonReleased`
3. Update `_lastKnownButtonState`, `_lastKnownTowerColors`

For scores:
1. For each team, `GetDisplayPoints(teamColor)` vs `_lastKnownScores[teamColor]`
2. If changed → `PointsAwarded` with delta as `Value`
3. Update `_lastKnownScores`

### Batch Flush

- `_flushTimer` fires every 5 seconds → write `_pendingEvents` to DB, clear list
- On `RecordGameEnd` → final flush
- Both use `_dbFactory.CreateDbContext()` inside `using`

### New Query Methods

```csharp
List<GameHistoryEvent> GetGameEvents(int gameHistoryId);
List<GameHistoryEvent> GetGameEventsByType(int gameHistoryId, EventType type);
Dictionary<int, int> GetDeathsPerMinute(int gameHistoryId);
Dictionary<string, int> GetTowerContestRanking(int gameHistoryId);
List<(DateTimeOffset Time, int BlueScore, int RedScore)> GetScoreTimeline(int gameHistoryId);

// Cross-game queries
List<GameHistory> GetGamesByDateRange(DateTime from, DateTime to);
Dictionary<string, int> GetWinRateByMode(DateTime? from, DateTime? to);
Dictionary<string, int> GetWinRateBySide(DateTime? from, DateTime? to);
List<(DateTime Day, double AvgDuration)> GetAvgDurationByDay(DateTime from, DateTime to);
List<(DateTime Day, int BlueDeaths, int RedDeaths)> GetDeathsByDay(DateTime from, DateTime to);
Dictionary<string, int> GetGlobalTowerHotspots(DateTime? from, DateTime? to);
```

### Side Resolution

The `Side` field on each event is determined by:
- On `RecordGameStart`: store `_teamInWald` / `_teamInStadt` mapping
- For each event: `Side = (teamColor == _teamInWald) ? "Wald" : "Stadt"`
- This persists even if the admin swaps sides mid-day — each game's events are tagged with that game's side assignments

---

## UI

### 1. Enhanced GameHistoryDetail.razor (Modify)

Add `RadzenTabs` wrapping the content into three tabs:

**Tab "Übersicht":** Existing content (summary, team cards, tower table, config snapshot).

**Tab "Zeitleiste":**
- Filter bar: `RadzenDropDown` for event type (multi-select), team filter
- Event list: `RadzenDataGrid` with columns: Time (mm:ss from game start), Type (icon + label), Team/Side badge, Tower letter, Value
- Grouped by game minute
- Below the list: small `RadzenBarSeries` chart — event count per minute (stacked by event type)

**Tab "Statistiken":**
- **Death heatmap**: `RadzenBarSeries` — minute buckets on X, death count on Y, colored by team
- **Tower contest**: `RadzenBarSeries` horizontal — tower letters on Y, capture count on X, colored by team
- **Score progression**: `RadzenLineSeries` — game time on X, score on Y, two lines (blue, red)
- **Momentum**: `RadzenAreaSeries` stacked — game time on X, tower count by team on Y

### 2. New Statistics Page (`/Admin/Statistics`)

New tab in `AdminStartPage.razor`. `StatisticsPage.razor` + `.razor.cs`.

**Header:** Date range picker (`RadzenDatePicker` range mode), "Apply" button.

**Dashboard grid (3-column `RadzenRow`):**

| Section | Component | Content |
|---|---|---|
| Game Day Summary | `RadzenCard` × 4 | Total games, wins blue, wins red, avg duration cards |
| Win Rate by Mode | `RadzenBarSeries` | X: mode name, Y: win count, stacked by team |
| Side Win Rate | `RadzenPieSeries` | Wald wins vs Stadt wins |
| Death Frequency | `RadzenLineSeries` | X: game minute, Y: avg death count, two lines |
| Most Active Team | `RadzenBarSeries` | X: team color, Y: total deaths |
| Tower Hot Spots | `RadzenBarSeries` horizontal | Y: tower letter, X: capture count, colored by team |
| Duration by Day | `RadzenColumnSeries` | X: date, Y: avg minutes, one column per mode |
| Wins by Day | `RadzenAreaSeries` stacked | X: date, Y: wins, blue + red area |

---

## Files to Create

| File | Purpose |
|---|---|
| `Models/GameHistoryEvent.cs` | Entity class |
| `Models/GameEventType.cs` | Enum |
| `Components/Pages/AdminPages/StatisticsPage.razor` | Statistics dashboard |
| `Components/Pages/AdminPages/StatisticsPage.razor.cs` | Code-behind with query logic, chart data |

## Files to Modify

| File | Change |
|---|---|
| `Context/DatabaseContext.cs` | Add `DbSet<GameHistoryEvent>`, OnModelCreating, raw SQL |
| `Models/GameHistoryTeam.cs` | Remove `Deaths`, `TowersControlled` |
| `Models/GameHistoryTower.cs` | Remove `Captures` |
| `Services/GameHistoryService.cs` | Add event subscriptions, batch flush, new query methods |
| `Services/Interfaces/IGameHistoryService.cs` | Add new method signatures |
| `Components/Pages/AdminPages/GameHistoryDetail.razor` | Add tabs, timeline, stats charts |
| `Components/Pages/AdminPages/AdminStartPage.razor` | Add "Statistiken" tab |

## Edge Cases

- **Mid-game crash**: `_pendingEvents` in memory lost. Acceptable tradeoff — event log truncated at last flush. GameHistory still records start/end.
- **Zero events**: Tabs show "Keine Ereignisse" / "Keine Daten" messages.
- **No games in date range**: Statistics page shows "Keine Spiele im ausgewählten Zeitraum".
- **Side swapped mid-day**: Each game's events tagged with that game's side assignments. Side-based stats remain accurate.
- **Large history (>500 games)**: Cross-game queries may need `Take()` limits or date range narrowing to keep response fast.
