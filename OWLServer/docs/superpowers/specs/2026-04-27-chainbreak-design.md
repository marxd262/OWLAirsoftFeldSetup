# ChainBreak Game Mode — Design Spec

**Date:** 2026-04-27  
**Status:** Approved

---

## Overview

ChainBreak is a standalone game mode based on Conquest. Teams must capture towers in a predefined order (a directed graph), building a chain from one or more entry points. The opposing team can disrupt progress by capturing towers mid-chain, which resets the chain forward from that point. The admin predefines chain topologies as named layouts that can be loaded and edited before a game.

---

## Data Model

### `ChainLayout`
Persisted entity representing a named set of tower links.

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Name` | `string` | Admin-defined label |
| `Links` | `List<ChainLink>` | Navigation property |

### `ChainLink`
Persisted entity representing one directed (or bidirectional) dependency between two towers.

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `ChainLayoutId` | `int` | FK to ChainLayout |
| `FromTowerMacAddress` | `string` | Predecessor tower |
| `ToTowerMacAddress` | `string` | Successor tower |
| `IsBidirectional` | `bool` | If true, link is traversable in both directions |

A `ChainLayout` is a directed acyclic graph (DAG) that may contain multiple disconnected subgraphs (multiple independent chains on the same map). Towers not referenced in any link of the active layout are freely capturable by either team.

### `GameModeChainBreak` config fields
All Conquest fields plus:

| Field | Type | Default | Notes |
|---|---|---|---|
| `ChainFactor` | `double` | `1.0` | Exponential depth multiplier |
| `ActiveChainLayoutId` | `int?` | `null` | Null = no chain, all towers free |

---

## Chain Rules

### Entry points
- Unidirectional chain: the root tower (no predecessors) is always capturable by either team.
- Bidirectional chain: both end towers are always capturable by either team.
- Towers outside any chain: always capturable by either team.

### Press rules (capture attempt)
A button press on a tower is accepted when:

| Tower state | Condition to press |
|---|---|
| Not in any chain | Always |
| Chain entry point | Always |
| Chain non-entry, currently held by the opponent | Always (disruptive action) |
| Chain non-entry, NEUTRAL or LOCKED | Pressing team must hold all required predecessors |

**Predecessor requirement — unidirectional:** hold the direct predecessor tower.  
**Predecessor requirement — bidirectional:** hold either adjacent neighbor in the chain (left OR right). If both neighbors are NEUTRAL or LOCKED, the tower is inaccessible (LOCKED) until a neighbor is recaptured.

### On capture complete
1. Check if the capturing team holds all required predecessors (per rules above):
   - **Yes** → tower flips to team color. Walk successors: any successor whose prerequisites are now fully met by this team → set to NEUTRAL (capturable).
   - **No** → tower goes **NEUTRAL** (not team-colored). All descendants in the chain → **LOCKED**.
2. In both cases: the previous owner's descendants all reset to **LOCKED** immediately.

### Chain break
When an opponent captures a tower mid-chain:
- The captured tower goes NEUTRAL (unless the capturing team already holds valid prerequisites, in which case it flips to their color).
- All descendants of the captured tower reset to LOCKED regardless.
- Entry points always remain capturable (they are never LOCKED).

### Bidirectional example
Chain: 1↔2↔3↔4 (all links bidirectional). Team A holds 3,4. Team B holds 1,2.

- Team A presses tower 2: valid (they hold tower 3, the right neighbor). Capture completes → tower 2 flips to Team A. Team B's tower 1 now has no valid neighbor held by Team B (tower 2 is gone) → tower 1 goes NEUTRAL.
- Team A presses tower 1 (entry, always pressable): capture completes → Team A doesn't hold tower 2 → tower 1 goes NEUTRAL. Team B loses tower 2 (no neighbor held) → tower 2 goes NEUTRAL.

---

## Scoring

Point calculation replaces `TowerManagerService.GetPoints()` for ChainBreak:

```
score += Math.Pow(ChainFactor, depth) × tower.Multiplier
```

- **Depth** is the shortest path (BFS) from any entry point to that tower, computed once at game start.
- Towers outside any chain have depth 0: `ChainFactor^0 = 1`, so plain `TowerMultiplier` applies.
- Chain roots / entry points also have depth 0.
- `ChainFactor = 1.0` produces flat scoring identical to standard Conquest.

**Example with ChainFactor = 2.0:**

| Tower | Depth | Score per tick |
|---|---|---|
| Tower 1 (root) | 0 | 1 × Multiplier |
| Tower 2 | 1 | 2 × Multiplier |
| Tower 3 | 2 | 4 × Multiplier |
| Tower 4 | 3 | 8 × Multiplier |

Point distribution frequency and ticket/point mode are inherited from Conquest config.

---

## Admin UI

### ChainBreak config card (`GameModeChainBreakConfig.razor`)
- Game Duration, Max Tickets, IsTicket, Point Frequency (same as Conquest)
- ChainFactor (double numeric input, min 1.0)
- **Chain Layout Manager** (inline below config fields):
  - Saved layout list: Name + Load / Delete buttons per entry
  - Active layout editor:
    - Link list: `Tower A → Tower B [↔]` with remove button per link
    - Add link: From dropdown + To dropdown + Bidirectional checkbox + Add button
    - Save As (new name) and Update (overwrite) buttons
  - Info note listing towers not in any chain: "N towers outside chain — freely capturable"

### Map overlay
- When ChainBreak is active, chain links are drawn over the map between tower positions.
- Unidirectional link: arrow from predecessor to successor.
- Bidirectional link: plain line (no arrowhead).

---

## Architecture

### New files

| File | Purpose |
|---|---|
| `Models/ChainLayout.cs` | Persisted entity |
| `Models/ChainLink.cs` | Persisted entity |
| `Models/GameModes/GameModeChainBreak.cs` | Game mode: state machine, depth map, scoring |
| `Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor` | Config + layout manager UI |

### Modified files

| File | Change |
|---|---|
| `Models/Enums.cs` | Add `ChainBreak` to `GameMode` enum |
| `Context/DatabaseContext.cs` | Add `DbSet<ChainLayout>`, `DbSet<ChainLink>` |
| `Components/Pages/AdminPages/AdminPanel.razor.cs` | Add `case GameMode.ChainBreak` |
| `Components/Pages/AdminPages/AdminPanel.razor` | Show ChainBreak config card |
| `Components/MapComponents/Map.razor` | Render chain link overlay when ChainBreak active |

### Unchanged files
`Tower.cs`, `TowerManagerService.cs`, `GameModeConquest.cs` — no modifications required.

---

## State Machine — GameModeChainBreak

`Runner()` calls `ProcessChainBreakStateMachine()` (instead of `TowerManagerService.ProcessTowerStateMachine()`) every 200 ms.

`ProcessChainBreakStateMachine()` steps:
1. For each tower being pressed: validate prerequisites (per chain rules above).
2. For each press that completes (elapsed ≥ TimeToCaptureInSeconds): apply capture result (flip to team color or NEUTRAL) and cascade locks to descendants.
3. Unlock successors where the capturing team now meets prerequisites.
4. Invoke `StateHasChangedAction`.

Depth map is built once in `RunGame()` via BFS from all entry points across all chains in the active layout.