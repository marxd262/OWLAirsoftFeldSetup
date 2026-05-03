# Game History Feature — Design Spec

**Date:** 2026-05-03  
**Status:** Approved

## Overview

Add a Game History feature so admins can review past games. Every game session (whether completed or manually stopped) is recorded in the database with full metadata, scores, tower states, team composition, and a configuration snapshot. The admin panel gets a new tab with a card grid view for browsing history and a detail modal for drilling into individual games.

## Data Model

### GameHistory

One row per game session.

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | auto-increment |
| `GameMode` | int | Maps to `GameMode` enum |
| `StartTime` | DateTime | When game started |
| `EndTime` | DateTime? | null until game ends |
| `Duration` | TimeSpan | `EndTime - StartTime` |
| `Winner` | int | `TeamColor` enum value |
| `EndReason` | string | `"Completed"` or `"Stopped"` |

### GameHistoryTeam

One row per team per game (2 rows per game: BLUE and RED).

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | auto-increment |
| `GameHistoryId` | int FK | cascade delete to `GameHistory` |
| `TeamColor` | int | BLUE or RED |
| `TeamName` | string | e.g. "Wald", "Stadt" |
| `Side` | string | `"Wald"` or `"Stadt"` |
| `FinalScore` | int | Points at game end |
| `Deaths` | int | Clicker presses / respawns |
| `TowersControlled` | int | Number of towers owned at game end |

### GameHistoryTower

One row per active tower per game.

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | auto-increment |
| `GameHistoryId` | int FK | cascade delete to `GameHistory` |
| `MacAddress` | string | Tower MAC address |
| `DisplayLetter` | string | A, B, C... |
| `FinalColor` | int | `TeamColor` at game end |
| `Captures` | int | Times captured during game |

### GameHistorySnapshot

One row per game — configuration captured at game start.

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | auto-increment |
| `GameHistoryId` | int FK | cascade delete to `GameHistory` |
| `SnapshotJSON` | string | Serialized config (tower settings, chain layout, game mode params) |

### Relationships (EF Fluent API)

- `GameHistory` → `GameHistoryTeam` (one-to-many, cascade delete)
- `GameHistory` → `GameHistoryTower` (one-to-many, cascade delete)
- `GameHistory` → `GameHistorySnapshot` (one-to-one, cascade delete)

## Service: GameHistoryService

A new singleton registered in `Program.cs`. Follows the same pattern as other services (singleton lifetime, injected dependencies).

### Dependencies

- `IDbContextFactory<DatabaseContext>` — for creating short-lived DB contexts
- `IExternalTriggerService` — subscribes to `StateHasChangedAction` for tower capture tracking
- `IGameStateService` — reads current game state at start/end

### Public Interface

```csharp
interface IGameHistoryService
{
    int? CurrentGameId { get; }
    void RecordGameStart();
    void RecordGameEnd(string endReason);
    List<GameHistory> GetAllGames();
    GameHistory? GetGame(int id);
    List<GameHistoryTeam> GetGameTeams(int gameHistoryId);
    List<GameHistoryTower> GetGameTowers(int gameHistoryId);
    GameHistorySnapshot? GetGameSnapshot(int gameHistoryId);
}
```

### Behavior

**RecordGameStart():**
1. Called from `GameStateService.StartGame()` (injection or direct call)
2. Creates `GameHistory` row with `StartTime = DateTime.Now`, `GameMode`, `EndReason = null`
3. Enumerates `TowerManagerService.Towers` → creates `GameHistoryTower` rows (FinalColor=NONE, Captures=0)
4. Enumerates `Teams` → creates `GameHistoryTeam` rows (scores/deaths=0)
5. Serializes current config → `GameHistorySnapshot.SnapshotJSON`
6. Stores `CurrentGameId` for the running game
7. Subscribes to `ExternalTriggerService.StateHasChangedAction` for capture counting

**Capture tracking (during game):**
- On each `StateHasChangedAction`, compares current tower colors against previous snapshot
- If a tower color changed and the new color is BLUE or RED, increments that tower's `Captures` counter in the DB
- Debounced: only writes to DB every 5 seconds (to avoid per-frame writes)

**RecordGameEnd(string endReason):**
1. Called from `GameStateService.HandleGameEnd()` and `StopGame()`
2. Updates `GameHistory` row: `EndTime`, `Duration`, `Winner`, `EndReason` (passed as arg)
3. Updates `GameHistoryTeam` rows: `FinalScore` from `IGameModeBase.GetDisplayPoints()`, `Deaths` from death counts, `TowersControlled` from tower color counts
4. Updates `GameHistoryTower` rows: `FinalColor` from current tower colors
5. Unsubscribes from `StateHasChangedAction`
6. Clears `CurrentGameId`

**Get methods:** Simple DB reads via the factory context.

### Integration Points

- `GameStateService.StartGame()`: Call `_gameHistoryService.RecordGameStart()` after game is created and configured but before `RunGame()` is called
- `GameStateService.HandleGameEnd()`: Call `_gameHistoryService.RecordGameEnd("Completed")`
- `GameStateService.StopGame()`: Call `_gameHistoryService.RecordGameEnd("Stopped")`

## UI: GameHistoryPage.razor

**Route:** `/Admin/GameHistory`

**Layout:** A new tab in `AdminStartPage.razor` tab strip, inserted between "Tower-Einstellungen" and "Map-Einstellungen".

### Card Grid

- Responsive grid using `RadzenRow` + `RadzenColumn` (3 columns on desktop, 1 on mobile)
- Each card is a `RadzenCard` with:

| Card area | Content |
|---|---|
| Header bar | Game mode name (German), background tinted by winner (blue/red/gray) |
| Date | `StartTime` formatted `dd.MM.yyyy HH:mm` |
| Duration | Formatted `mm:ss` |
| Winner badge | Team name in colored pill (`RadzenBadge`) |
| Status badge | "Abgeschlossen" (completed) or "Abgebrochen" (stopped) |
| Scores row | Team names with final scores side-by-side using small colored bars |
| Hover state | Card elevation increase, cursor pointer |
| Click handler | Opens detail modal via `RadzenDialogService` |

### Filter Bar

Above the grid, a toolbar with:

| Control | Type | Purpose |
|---|---|---|
| Date range | `RadzenDatePicker` (range mode) | Filter by date |
| Game mode | `RadzenDropDown` (multi-select) | Filter by mode |
| Winner | `RadzenDropDown` | RED / BLUE / NONE / All |
| Sort | `RadzenDropDown` | Newest first, oldest first, longest duration |
| Search | Free text input | Search across team names |

### Detail Modal

Opened via `DialogService.OpenAsync<>()` when a card is clicked. Title: Game mode name + date.

**Content sections (stacked vertically):**

1. **Summary header:** Game mode, date, duration, winner name, end reason
2. **Score breakdown:** Two side-by-side panels (BLUE and RED) showing:
   - Team name + side (Wald/Stadt)
   - Final score
   - Deaths
   - Towers controlled
3. **Tower table:** `RadzenDataGrid` with columns: Letter, Final Color (colored pill), Captures count
4. **Config snapshot:** Collapsible section (`RadzenPanel`) with `RadzenTextBox` (read-only, monospace) showing formatted JSON

### Subscription Pattern

Follows existing UI pattern:
- `@implements IDisposable`
- Subscribes to `StateHasChangedAction` in `OnInitializedAsync` (to reflect active game changes in the current-game card if shown)
- Unsubscribes in `Dispose()`

## Files to Create

| File | Purpose |
|---|---|
| `Models/GameHistory.cs` | Entity class |
| `Models/GameHistoryTeam.cs` | Entity class |
| `Models/GameHistoryTower.cs` | Entity class |
| `Models/GameHistorySnapshot.cs` | Entity class |
| `Services/GameHistoryService.cs` | Service implementation |
| `Services/Interfaces/IGameHistoryService.cs` | Interface |
| `Components/Pages/AdminPages/GameHistoryPage.razor` | Blazor page component |
| `Components/Pages/AdminPages/GameHistoryPage.razor.cs` | Code-behind |

## Files to Modify

| File | Change |
|---|---|
| `Program.cs` | Register `IGameHistoryService` as singleton |
| `Context/DatabaseContext.cs` | Add 4 new `DbSet<>` properties |
| `Services/GameStateService.cs` | Inject `IGameHistoryService`; call `RecordGameStart()` in `StartGame()`, `RecordGameEnd()` in `HandleGameEnd()` and `StopGame()` |
| `Components/Pages/AdminPages/AdminStartPage.razor` | Add new tab for Game History |
| `Components/Layout/NavMenu.razor` | Add nav link (optional, since it's a sub-tab of Admin) |

## Edge Cases

- **No games recorded:** Card grid shows empty state with "Keine Spiele aufgezeichnet" message
- **Active game:** A live game does not appear in history until it ends; the card grid only shows completed/stopped games
- **Game stopped immediately after start:** Empty duration, all scores at 0 — still recorded with "Stopped" reason
- **Large history:** Pagination via `RadzenDataGrid` loader or virtual scrolling if history grows large (>100 games)
- **SQLite performance:** Single short-lived context per read operation; no long-lived connections
