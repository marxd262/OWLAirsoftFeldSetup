# Game Control Dashboard — Design Spec

**Date:** 2026-04-28
**Status:** Design approved

## Overview

Replace the "Startseite" tab in `/Admin` with a unified admin dashboard that serves both pre-game readiness monitoring and in-game match control. The dashboard provides a professional, tablet-friendly overview with always-visible tower status, map, and game controls.

## Placement

**File:** `OWLServer/Components/Pages/AdminPages/AdminStartPage.razor`

The dashboard permanently replaces the current content (`<Home ShowGameEndOverlay="false"/>`) in the first tab of `AdminStartPage.razor`. The tab label changes to "Spiel-Übersicht" when a game is active.

The existing "Spiel-Einstellungen" tab (AdminPanel) remains the configuration hub (game mode selection, mode-specific settings, auto-start). The dashboard becomes the operation hub.

**Admin workflow:** Configure in "Spiel-Einstellungen" → monitor and operate in "Startseite"/Dashboard.

## Layout

```
┌─────────────────────────────────────────────────────┐
│  [Timer/Status]   │  [Game Mode]   │  [Towers Online] │  ← Top stat cards
├──────────────────┬──┬───────────────────────────────┤
│  Blue Team Card  │⇄│   Red Team Card                │  ← Score or readiness
├──────────────────┴──┴───────────────────────────────┤
│  [Start Game]  or  [Pause] [End Game] [Reset]       │  ← Controls
├─────────────────────────┬───────────────────────────┤
│  Map                    │  Tower Status List         │  ← Map + towers
│  (flex ratio, respects  │  (fills remaining space)   │
│   map aspect ratio)     │                            │
└─────────────────────────┴───────────────────────────┘
```

The map card and tower status are side-by-side on desktop (3:2 ratio). On tablets they stack vertically — map on top, tower list below.

## Two States

### Pre-Game (no game running)

| Card | Content |
|---|---|
| Top left | "Warten" status badge (amber) |
| Top center | Selected game mode name |
| Top right | Tower online count (e.g. "7/10") |
| Team left | Blue readiness + "Wald" spawn label |
| Swap button | Visible and active — swaps `TeamInWald`/`TeamInStadt` |
| Team right | Red readiness + "Stadt" spawn label |
| Controls | Single **Start Game** button (green) |
| Map | Interactive map with tower markers |
| Tower list | Online/offline dots + neutral color squares |

### In-Game (game running)

| Card | Content |
|---|---|
| Top left | Game clock (mm:ss, counts up or down per mode) |
| Top center | Game mode name |
| Top right | Tower online count |
| Team left | Blue score + "Wald" spawn label |
| Swap button | **Hidden** |
| Team right | Red score + "Stadt" spawn label |
| Controls | **Pause**, **End Game**, **Reset** buttons |
| Map | Interactive map with live tower colors |
| Tower list | Online/offline dots + capture progress bars (color-coded) |

Team cards switch between "BEREIT/NICHT BEREIT" (pre-game) and numeric score (in-game). Readiness is shown in green (#34d399) / red (#ef4444). Scores use team colors (#00b4f1 blue, #fc1911 red).

## Interactions & Controls

### Pre-game

| Control | Action |
|---|---|
| **Start Game** | Calls `GameStateService.StartGame()` |
| **Swap spawns (⇄)** | Swaps `GameStateService.TeamInWald` and `TeamInStadt`, invokes `StateHasChangedAction` |

### In-game

| Control | Action |
|---|---|
| **Pause** | Toggles — calls `CurrentGame.PauseGame()` / `ResumeGame()`. Button label changes. Requires new `PauseGame()`/`ResumeGame()` methods on `IGameModeBase` with default no-op implementation. |
| **End Game** | Calls `GameStateService.StopGame()` |
| **Reset** | Calls `GameStateService.Reset()` — zeros scores, resets towers, clears readiness |

### Interactive areas

- **Map card** — reused `Map.razor` component, already handles tower click rendering
- **Tower status items** — display-only in this spec. Future expansion: click to expand details or force capture. Not in scope.
- **Team score cards** — display-only. Manual score adjustment not in scope.

## Data Flow

The dashboard subscribes to `ExternalTriggerService.StateHasChangedAction` (standard Blazor pattern — subscribe in `OnInitialized`, unsubscribe in `Dispose`).

On each state change, it reads from existing services:

| Data | Source |
|---|---|
| Game mode, scores, timer, IsRunning, IsFinished | `GameStateService.CurrentGame` |
| Spawn assignments | `GameStateService.TeamInWald`, `TeamInStadt` |
| Readiness | `GameStateService.WaldSpawnReady`, `StadtSpawnReady` |
| Tower list, online status, capture progress, colors | `GameStateService.TowerManagerService.Towers` |

**No polling.** Push-based via the existing C# event system.

## Responsive Design

- Top stat cards: 3-column flex row — wraps naturally on narrow screens
- Team cards + swap button: `flex-nowrap`, always side-by-side
- Map + tower list: `flex-wrap` — side-by-side on desktop, stacked on tablets
- Map card: uses `object-fit: contain` on the map image, respects its natural aspect ratio — height follows the image, not a fixed size
- Tower status list: fills remaining vertical space when stacked below the map; scrollable if towers exceed viewport
- No hardcoded media query breakpoints — relies on flexbox wrapping

## New Code Required

| What | Where | Notes |
|---|---|---|
| `GameControlDashboard.razor` | New `Components/` (or `Components/Pages/AdminPages/`) | The dashboard component |
| `PauseGame()` / `ResumeGame()` | `IGameModeBase` interface + all 4 implementations | Default no-op; ChainBreak and Conquest pause the runner loop via `CancellationTokenSource` |
| Update tab content | `AdminStartPage.razor` | Replace `<Home/>` with `<GameControlDashboard/>`; conditional tab label |

## Out of Scope

- Manual score adjustment during game
- Tower force-capture from dashboard
- Match history / past game review
- Fixing known bugs in ToDos.md (DBSet mismatch, tower relock, reset timer) — separate effort
- Audio service platform support
- Tower network error handling improvements
