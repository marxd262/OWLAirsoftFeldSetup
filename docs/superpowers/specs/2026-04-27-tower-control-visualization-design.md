# Tower Control Visualization — Design Spec

**Date:** 2026-04-27

## Objective

Replace the runtime-only (`[NotMapped]`) tower control/lock system with a persisted `TowerControlLayout`/`TowerControlLink` model that mirrors the ChainBreak pattern end-to-end: data model → database → game mode state → admin config → SVG visualization on map.

## 1. Data Model

**New files:** `Models/TowerControlLayout.cs`, `Models/TowerControlLink.cs`

```csharp
public class TowerControlLayout
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TowerControlLink> Links { get; set; } = new();
}

public class TowerControlLink
{
    public int Id { get; set; }
    public int TowerControlLayoutId { get; set; }
    public string ControllerTowerMacAddress { get; set; } = string.Empty;
    public string ControlledTowerMacAddress { get; set; } = string.Empty;
}
```

- No `IsBidirectional` — tower control is always one-way (controller → controlled)
- Controller towers are NOT locked; their controlled children ARE locked

## 2. Database

**File:** `Context/DatabaseContext.cs`

Add `DbSet<TowerControlLayout>` and `DbSet<TowerControlLink>` with cascade delete on the layout-to-links relationship — same pattern as ChainLayout/ChainLink.

## 3. GameModeConquest — ChainBreak-style state machine

**File:** `Models/GameModes/GameModeConquest.cs`

- Add `ActiveControlLayout` property (like ChainBreak's `ActiveChainLayout`)
- Build runtime graph at `RunGame()` via `BuildControlMaps()` (like `BuildChainMaps()`)
- `InitializeControlTowerStates()` — controllers + non-chain → NONE, controlled → LOCKED
- `ProcessConquestStateMachine()` replaces `TowerManagerService.ProcessTowerStateMachine()` call in runner
- Lock: controller reset timer expires → controller → NONE, its children → LOCKED
- Unlock: controller captured → controller → team color, its children → NONE
- Guard: if a controlled tower is being pressed but its controller isn't owned by the pressing team → cancel press

## 4. Tower.cs — remove runtime control state

**File:** `Models/Tower.cs`

Remove `[NotMapped]` properties: `IsControlled`, `IsControlledByID`, `IsForControlling`, `ControllsTowerID`.
Simplify `SetToStartColor()` to always set NONE.

## 5. TowerManagerService — cleanup

**File:** `Services/TowerManagerService.cs`

Remove `ProcessTowerStateMachine()` — its logic has moved to Conquest.

## 6. Admin config — TowerControlConfig

**New files:** `Components/ConfigComponents/GameModes/TowerControlConfig.razor` + code-behind

Mirrors `GameModeChainBreakConfig.razor`:
- Saved layouts list (Load/Activate/Delete)
- Active layout badge (green) / inactive warning (yellow)
- Layout editor: add/remove links via dropdowns
- Save as new / Update / Delete
- No `IsBidirectional` checkbox

Embedded in existing `GameModeConquestConfig.razor`.

## 7. TowerConfig.razor — remove control dropdown

Remove the "Wird kontrolliert von" dropdown and `ControllingTowerChanged()` method.

## 8. Map.razor — adapt visualization

Replace the existing SVG arrow block (using `ControllsTowerID`) with a ChainBreak-style block reading from `ActiveControlLayout.Links`. Arrow color = controller tower's team color.

## Cross-reference: ChainBreak parallel

| ChainBreak | Tower Control |
|---|---|
| `ChainLayout` | `TowerControlLayout` |
| `ChainLink` (From→To, IsBidirectional) | `TowerControlLink` (Controller→Controlled) |
| DbSets in `DatabaseContext` | DbSets in `DatabaseContext` |
| `ActiveChainLayout` on GameModeChainBreak | `ActiveControlLayout` on GameModeConquest |
| `BuildChainMaps()` + `InitializeTowerStates()` | `BuildControlMaps()` + `InitializeControlTowerStates()` |
| `ProcessChainBreakStateMachine()` | `ProcessConquestStateMachine()` |
| `GameModeChainBreakConfig.razor` | `TowerControlConfig.razor` |
| Conditional SVG block in Map.razor | Conditional SVG block in Map.razor |
