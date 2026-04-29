# ChainBreak: Fix LockDescendants Over-Locking Neutral Junction Towers

**Date:** 2026-04-29
**Status:** Design Approved

## Purpose

Fix a bug in `ChainGraphEngine.LockDescendants` where neutral towers at chain junctions get incorrectly locked when one side of their chain is destroyed, even though they have a valid alternative predecessor from another chain direction.

## Bug Scenario

**Setup:** Bidirectional chain 1↔2↔3↔4↔5 with entry points at towers 1 and 5.

- Blue holds towers 1, 2
- Red holds towers 4, 5
- Tower 3 is NONE (both teams can attack — blue from tower 2, red from tower 4)

**Action:** Blue captures tower 5 (counter-captures red's entry point).

**Expected:** Tower 3 stays NONE because it still has a valid blue predecessor (tower 2) from the other side of the chain.

**Actual (bug):** Tower 3 becomes LOCKED because `LockDescendants` recursively locks it as part of the collapsed RED chain (5→4→3), without checking that tower 3 has an alternative healthy predecessor (tower 2 is BLUE).

## Root Cause

`LockDescendants` has two paths for locking:
1. `ownedByPrev` — tower was owned by the previous owner → always lock
2. `neutral` — tower is NONE → always lock

Path 2 is too aggressive: it locks neutral towers that sit at chain junctions and have healthy predecessors from other directions.

## Fix

In `LockDescendants`, before locking a neutral tower, check if any non-visited predecessor is held by an active team (not NONE, not LOCKED, not `previousOwner`). If such a safe predecessor exists, skip locking — the tower still has a valid capture path.

```csharp
// NEW: For neutral towers, skip locking if there's an alternative valid predecessor
if (neutral && _predecessors.TryGetValue(succMac, out var preds))
{
    bool hasSafePred = preds.Any(p =>
        _towers.TryGetValue(p, out var pt) &&
        pt.CurrentColor != TeamColor.NONE &&
        pt.CurrentColor != TeamColor.LOCKED &&
        pt.CurrentColor != previousOwner &&
        !visited.Contains(p));
    if (hasSafePred) continue;
}
```

Inserted between the `if (!ownedByPrev && !neutral) continue;` line and the `SetTowerColor(LOCKED)` line.

## Test Plan

Add a new xUnit test `CompleteCapture_CounterCaptureEntry_NeutralJunctionNotLocked` in `ChainGraphEngineTests.cs`:

```
Setup:
  5-tower bidirectional chain 1↔2↔3↔4↔5, entries at 1 and 5
  Tower states: 1=BLUE, 2=BLUE, 3=NONE, 4=RED, 5=RED

Action:
  engine.CompleteCapture("5", TeamColor.BLUE)

Assert:
  Tower 5 = BLUE (captured by blue)
  Tower 4 = NONE (unlocked, chain link from 5 broken)
  Tower 3 = NONE (NOT locked — has safe predecessor tower 2=BLUE)
  Tower 2 = BLUE (unchanged)
  Tower 1 = BLUE (unchanged)
```

### Existing tests must still pass

The fix must not break existing tests, especially:
- `CompleteCapture_RecursiveCascade_DeepChain` — neutral towers in a simple linear chain without alternative predecessors should still be locked
- `CompleteCapture_MidChainLocksDescendants` — same, simple linear chain
- `CompleteCapture_PreviousOwnerNONE_LocksAllDescendants` — previous owner is NONE, all descendants locked
- `CompleteCapture_CounterCaptureLocksPreviousOwnersDescendants` — basic lock behavior

## Files Changed

| File | Change |
|---|---|
| `Models/GameModes/ChainGraphEngine.cs` | Add safe-predecessor check in `LockDescendants` (~6 lines) |
| `OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs` | Add test for the junction scenario (~20 lines) |

## Data Flow

```
Blue captures tower 5 (entry point)
  → CompleteCapture("5", BLUE)
  → LockDescendants("5", RED)  // previous owner
      → Lock 4 (owned by RED)
      → Check 3 (neutral): preds [2, 4]
          → 2 = BLUE, not visited, not RED → SAFE PRED → SKIP locking 3
  → SetTowerColor(5, BLUE)
  → UnlockSuccessors("5", BLUE)
      → Unlock 4 (LOCKED → NONE)
```
