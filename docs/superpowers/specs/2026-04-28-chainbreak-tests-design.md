# ChainBreak Game Mode — Test Suite Design

**Date:** 2026-04-28
**Status:** Approved

---

## Problem

`GameModeChainBreak` has complex graph-based chain logic (directional dependencies, entry points, locking/unlocking, depth-based scoring) but only 2 trivial tests (`FillTeams`, `GetWinner`). All core chain methods (`BuildChainMaps`, `CanPress`, `LockDescendants`, `UnlockSuccessors`, `CompleteCaptureChain`, `ProcessChainBreakStateMachine`, `GetChainPoints`, `GetLinkVisualState`) are untested.

## Approach

**B — Extract `ChainGraphEngine` + unit test.**

Pull all graph/chain logic into a new `ChainGraphEngine` class. Test it in isolation via xUnit + Moq. The remaining `GameModeChainBreak` thins to orchestration: `Runner()` loop lifecycle, `InitializeTowerStates()`, `DistributePoints()`, win condition checks, and pause/reset.

---

## ChainGraphEngine — Public API

```csharp
public class ChainGraphEngine
{
    // Construction — builds graph eagerly from ChainLayout
    public ChainGraphEngine(ChainLayout layout, Dictionary<string, Tower> towers);

    // Readable graph state (for debugging / UI)
    public IReadOnlyDictionary<string, List<string>> Successors { get; }
    public IReadOnlyDictionary<string, List<string>> Predecessors { get; }
    public IReadOnlySet<string> EntryPoints { get; }
    public IReadOnlyDictionary<string, int> DepthMap { get; }

    // Queries — pure, no side effects
    public bool CanPress(string mac, TeamColor team);
    public int GetChainPoints(TeamColor team, double chainFactor);
    public (string colorHex, string arrowA, string arrowB, bool animated, bool bothWays)
        GetLinkVisualState(ChainLink link);

    // Mutations — modify Towers dictionary directly
    public void CompleteCapture(string mac, TeamColor capturingTeam);

    // Processes all pressed towers for one tick, returns updates
    public List<TowerCaptureUpdate> ProcessTick();
}

public class TowerCaptureUpdate
{
    public Tower Tower { get; set; }
    public bool CaptureCompleted { get; set; }
    public double CaptureProgress { get; set; }
}
```

### Internal (private) methods

- `BuildChainMaps()` — called by constructor
- `AddEdge(string from, string to)` — builds both `_successors` and `_predecessors`
- `LockDescendants(string mac, TeamColor previousOwner, HashSet<string> visited)` — recursive
- `UnlockSuccessors(string mac, TeamColor capturingTeam)` — one level deep per invocation

These remain private. Tests verify their behavior indirectly through the public API (`CompleteCapture`, `ProcessTick`, `GetLinkVisualState`) and by inspecting tower states afterward. No `InternalsVisibleTo` needed.

### Dependencies

- Takes `ChainLayout` and `Dictionary<string, Tower>` by value — no DI, no interface wrappers
- `Tower` is a mutable model object (not an interface) — tests instantiante real `Tower` objects

---

## GameModeChainBreak Changes

**Keeps:** `Runner()`, `RunGame()`, `EndGame()`, `ResetGame()`, `InitializeTowerStates()`, `DistributePoints()`, `FillTeams()`, properties

**Deletes:** `BuildChainMaps()`, `AddEdge()`, `CanPress()`, `LockDescendants()`, `UnlockSuccessors()`, `CompleteCaptureChain()`, `ProcessChainBreakStateMachine()`, `GetChainPoints()`, `GetLinkVisualState()`, `_successors`, `_predecessors`, `_chainEntryPoints`, `_depthMap`

**New field:** `private ChainGraphEngine? _engine` — null before `RunGame()`, rebuilt on each `RunGame()`, nulled on `ResetGame()`.

**Consumers to update:**
- `Home.razor` / `Map.razor` — call `_engine.GetLinkVisualState()` instead of `gameMode.GetLinkVisualState()` — OR expose a pass-through on `GameModeChainBreak`
- `AdminPanel.razor` / `GameModeChainBreakConfig.razor` — no changes (they interact with `ChainLayout` / `ActiveChainLayout`, not the engine)

---

## Test Plan

### File: `Unit/GameModes/ChainGraphEngineTests.cs` (new, ~48 tests)

| Category | Count | Scenarios |
|---|---|---|
| Graph construction | 8 | Linear A→B→C, branch A→{B,C}, merge {A,B}→C, bidirectional A↔B, diamond A→{B,C}→D, disconnected tower, single tower, empty layout |
| CanPress | 8 | Entry point pressable, successor with predecessor held ✅, successor without predecessor ❌, counter-capture enemy tower ✅, already own tower ❌, bidirectional entry, outsider tower, unknown MAC |
| LockDescendants | 6 | Mid-chain capture → all downstream locked, entry point skipped, branch all locked, previous owner NONE locks all, recursive cascade, no successors = no-op |
| UnlockSuccessors | 5 | Unlock with prerequisite met, stuck with other prereq unmet, no predecessors = auto-unlock, branch unlock, no successors = no-op |
| CompleteCapture | 5 | Forward capture full cascade, counter-capture locks previous owner's descendants, missing prerequisites → NONE + lock, multi-hop unlock chain, entry point capture |
| GetChainPoints | 5 | Single tower, linear depth scaling, chainFactor exponent, no owned towers, mixed team ownership |
| GetLinkVisualState | 7 | Same team solid, both neutral grey, one end owned → animated, contested white animated, locked → yellow, bidirectional edges, fallback dark grey |
| ProcessTick | 4 | No pressed towers = empty, progress update only, timer expires → capture, disallowed press mid-hold → reset |

### File: `Unit/GameModes/GameModeChainBreakTests.cs` (expand existing, ~10 tests)

| Category | Count | Scenarios |
|---|---|---|
| InitializeTowerStates | 3 | Entry points → NONE, chain non-entry → LOCKED, outsider → NONE |
| DistributePoints | 2 | Points added to both teams, zero when no engine |
| Win conditions | 3 | Time expiry, ticket depletion, tie |
| Lifecycle | 2 | EndGame stops runner, ResetGame nulls engine |

### Mocking strategy

- `ChainGraphEngineTests`: **No mocking.** Create real `ChainLayout` → `ChainLink` objects and real `Tower` objects. Assert graph state and tower mutations directly.
- `GameModeChainBreakTests`: Mock `IExternalTriggerService`, `IGameStateService`, `ITowerManagerService` via Moq. `ChainGraphEngine` is constructed with real towers from the mock.

---

## Implementation Order

1. Create `ChainGraphEngine` class with full API (extract existing logic from `GameModeChainBreak`)
2. Write `ChainGraphEngineTests.cs` — graph construction tests first (foundation), then capture flow
4. Refactor `GameModeChainBreak` to delegate to `ChainGraphEngine`
5. Update `Map.razor` to consume `ChainGraphEngine` (pass-through property on `GameModeChainBreak`)
6. Expand `GameModeChainBreakTests.cs`
7. Update existing tests that reference `GameModeChainBreak` if API surface changed
8. Run full test suite, verify no regressions

---

## Risks

- **Refactoring regression:** Moving chain logic could break existing behavior. Mitigated by writing tests first where possible and running the existing test suite after each step.
- **Map.razor dependency:** Currently calls `gameMode.GetLinkVisualState()` directly. Add a pass-through property on `GameModeChainBreak` (`ChainGraphEngine? Engine => _engine`) to minimize consumer changes.
