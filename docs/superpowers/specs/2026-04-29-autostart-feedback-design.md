# Auto-Start Feedback Design

**Date:** 2026-04-29
**Status:** Approved

## Overview

Currently `GameStateService.AutoStartProcessStarted` is set when the auto-start countdown begins but is never read by any UI. Admins see no countdown, players see nothing at all. This spec adds real-time feedback to both audiences.

## Requirements

1. **Admin dashboard** (`AdminPanel.razor`): Live countdown timer showing seconds remaining when auto-start is counting down
2. **Player overlay** (`Home.razor`): Semi-transparent overlay over the map showing countdown timer, status text, spawn readiness, and cancel info
3. Minimal new infrastructure — leverage existing `StateHasChangedAction` event pattern

## Design

### GameStateService changes

Add three read-only computed properties:

```csharp
// Remaining seconds in the countdown (0 when expired)
public int? AutoStartSecondsRemaining => 
    AutoStartProcessStarted.HasValue 
        ? Math.Max(0, SecondsTillAutoStartAfterReady - (int)(DateTime.Now - AutoStartProcessStarted.Value).TotalSeconds)
        : null;

// True when auto-start is ON and actively counting down (both spawns ready, timer not expired)
public bool AutoStartCountdownActive => 
    AutoStartAfterReady 
    && AutoStartProcessStarted.HasValue 
    && (DateTime.Now - AutoStartProcessStarted.Value).TotalSeconds < SecondsTillAutoStartAfterReady;

// True when auto-start is ON but still waiting for one or both spawns to become ready
public bool AutoStartWaitingForSpawns =>
    AutoStartAfterReady && !AutoStartProcessStarted.HasValue;
```

Add these to `IGameStateService` interface.

### Modify AutoStartGame() loop

In both the "wait for spawns" and "countdown" polling loops of `AutoStartGame()`, add `ExternalTriggerService.StateHasChangedAction?.Invoke()` on each iteration. This causes all subscribing Blazor components to re-render every 100ms during the auto-start window.

The `ExternalTriggerService` is already injected into `GameStateService` (it constructs `TowerManagerService` internally which takes `IExternalTriggerService`; confirm it has access — or pass it via constructor).

### Admin dashboard (AdminPanel.razor)

In the existing auto-start settings card, below the toggle and numeric input, add conditional elements:

| State | UI |
|---|---|
| `AutoStartCountdownActive` | `RadzenBadge` with `BadgeStyle.Warning`: "Auto-start in: **Xs**" with a large countdown number |
| `AutoStartWaitingForSpawns` | "Waiting for both teams to be ready..." text |
| Neither (off or game started) | Nothing (existing toggle + input only) |

The existing spawn readiness badges (Wald/Stadt Ready/Not Ready) are already rendered just above this card, so spawn status is already visible to the admin.

### Player overlay (AutoStartOverlay.razzr)

New component, placed in `Home.razor` after the `Map` component.

**When visible:** Only when `AutoStartCountdownActive` is true AND game is not running.

**Content (top to bottom, centered):**
1. **Status text**: "Game starting soon" in large font
2. **Countdown**: Very large number (e.g., "12") updating in real-time, white or team-agnostic color
3. **Spawn readiness row**: Two small badges — "Wald: Ready/Waiting", "Stadt: Ready/Waiting"
4. **Cancel info**: Small text — "Countdown resets if a team un-readies"

**Styling:** Follow `GameEndOverlay.razor` conventions — `position: fixed`, dark semi-transparent background (`rgba(0,0,0,0.85)`), centered card, inline `<style>` block.

### Home.razor change

Add conditional rendering after the `Map` component:

```razor
@if (!(GameStateService.CurrentGame?.IsRunning ?? false) && GameStateService.AutoStartCountdownActive)
{
    <AutoStartOverlay />
}
```

Since `Home` is embedded inside `SpawnPage.razor`, both `/` and `/Spawn-Wald`/`/Spawn-Stadt` get the overlay automatically.

### Edge cases

| State | Admin sees | Player sees |
|---|---|---|
| Auto-start OFF | Toggle + numeric (unchanged) | Nothing |
| Auto-start ON, spawns not both ready | "Waiting for both teams..." text | Nothing |
| Auto-start ON, both ready, counting down | Countdown badge | Full overlay |
| Countdown aborted (spawn un-ready) | "Waiting for both teams..." text | Overlay disappears |
| Game started | Auto-start flips OFF, all gone | Overlay gone |

## Files changed

| File | Change |
|---|---|
| `Services/GameStateService.cs` | Add 3 computed properties, fire `StateHasChangedAction` in auto-start loops |
| `Services/Interfaces/IGameStateService.cs` | Add 3 new read-only property declarations |
| `Components/Pages/AdminPages/AdminPanel.razor` | Add conditional countdown display in auto-start card |
| `Components/Pages/Home.razor` | Add conditional `<AutoStartOverlay />` |
| `Components/Pages/AutoStartOverlay.razor` | **New** — player-facing overlay component |

## Non-goals

- No changes to how auto-start is triggered/cancelled
- No new events on `ExternalTriggerService`
- No changes to `SpawnPage.razor` (the ready button already works as-is)
- No new CSS files (inline styles per existing pattern)
