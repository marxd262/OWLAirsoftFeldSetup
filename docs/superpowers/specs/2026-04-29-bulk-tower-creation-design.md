# Bulk Tower Creation on DebugApi

**Date:** 2026-04-29
**Status:** Design Approved

## Purpose

Add a bulk tower creation feature to the DebugApi page so the dev can quickly populate the frontend with any number of test towers, without manually registering them one at a time.

## Scope

Single-file change: `Components/Pages/DebugApi.razor` only.

## Design

### UI Addition

Extend the existing "Tower registrieren" card with a separator and bulk section:

- A number input (`_bulkCount`) defaulting to `3`
- A green "Create N Towers" (`RadzenButton`) button
- Reuses the same `_regIp` field already present in the card (default `1.1.1.1`)

### Behavior: `BulkRegisterTowers()`

1. Scan `GameStateService.TowerManagerService.Towers` for existing MACs matching the pattern `DE:B0:00:00:00:XX` (two uppercase hex digits)
2. Extract the highest XX suffix found (or `0` if none)
3. Loop from `highestSuffix + 1` for `_bulkCount` iterations
4. Each iteration:
   - Format MAC as `DE:B0:00:00:00:{XX:2}` (zero-padded 2-digit hex, uppercase)
   - POST to the existing `POST /api/RegisterTower?id={mac}&ip={_regIp}` via the page's `_httpClient`
   - Log result to the event log
   - Re-read tower list update via `StateHasChangedAction` subscription (UI auto-refreshes)

### Constraints

- Max 255 towers per prefix (2 hex digits)
- If existing towers fill the prefix range, the button does nothing (logs "All MACs used")
- Duplicate MACs are silently ignored by `TowerManagerService.RegisterTower` (existing behavior)
- `DisplayLetter` auto-assigns alphabetically per existing `RegisterTower` logic

## Files Changed

| File | Change |
|---|---|
| `Components/Pages/DebugApi.razor` | Add `_bulkCount` field, `BulkRegisterTowers()` method, UI elements in card |

No other files, no new API endpoints, no DB changes.

## Data Flow

```
User enters count (e.g. 3)
  → clicks "Create N Towers"
  → BulkRegisterTowers() reads existing MACs from TowerManagerService
  → finds highest "DE:B0:00:00:00:0F" so starts at "DE:B0:00:00:00:10"
  → loop 3x:
      POST /api/RegisterTower?id=DE:B0:00:00:00:10&ip=1.1.1.1
      POST /api/RegisterTower?id=DE:B0:00:00:00:11&ip=1.1.1.1
      POST /api/RegisterTower?id=DE:B0:00:00:00:12&ip=1.1.1.1
  → each tower appears in the list via StateHasChangedAction
```
