# ChainBreak: Fix LockDescendants Over-Locking Neutral Junction Towers — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix `LockDescendants` so neutral towers at chain junctions are not incorrectly locked when one branch collapses but they still have a valid predecessor from another branch.

**Architecture:** Add a "safe predecessor" check in `LockDescendants`: before locking a neutral tower, verify no non-visited predecessor is held by an active team (not NONE, not LOCKED, not `previousOwner`). If such a predecessor exists, skip locking. New xUnit test validates the 5-tower bidirectional junction scenario.

**Tech Stack:** C#, .NET 8, xUnit, Moq

---

### Task 0: Commit untracked ChainGraphEngine.cs

**Files:**
- Stage: `OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs`

ChainGraphEngine.cs is currently untracked (`??` in git status). It must be committed before we can make changes to it.

- [ ] **Step 1: Stage and commit ChainGraphEngine.cs**

```bash
git add OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs
git commit -m "feat: add ChainGraphEngine for ChainBreak chain mechanics"
```

---

### Task 1: Write the failing test

**Files:**
- Modify: `OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs`

- [ ] **Step 1: Add the test method**

Add the following test method inside the `ChainGraphEngineTests` class, in the "CompleteCapture → LockDescendants" section (after the existing `CompleteCapture_NoSuccessors_NoLocking` test, around line 373):

```csharp
    [Fact]
    public void CompleteCapture_CounterCaptureEntry_NeutralJunctionNotLocked()
    {
        var t1 = T("1"); t1.SetTowerColor(TeamColor.BLUE);
        var t2 = T("2"); t2.SetTowerColor(TeamColor.BLUE);
        var t3 = T("3"); t3.SetTowerColor(TeamColor.NONE);
        var t4 = T("4"); t4.SetTowerColor(TeamColor.RED);
        var t5 = T("5"); t5.SetTowerColor(TeamColor.RED);
        var towers = Towers(t1, t2, t3, t4, t5);
        var layout = Layout(
            Link("1", "2", both: true),
            Link("2", "3", both: true),
            Link("3", "4", both: true),
            Link("4", "5", both: true)
        );
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("5", TeamColor.BLUE);

        Assert.Equal(TeamColor.BLUE, t5.CurrentColor);
        Assert.Equal(TeamColor.NONE, t4.CurrentColor);
        Assert.Equal(TeamColor.NONE, t3.CurrentColor);
        Assert.Equal(TeamColor.BLUE, t2.CurrentColor);
        Assert.Equal(TeamColor.BLUE, t1.CurrentColor);
    }
```

- [ ] **Step 2: Run the test — expect FAILURE**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "CompleteCapture_CounterCaptureEntry_NeutralJunctionNotLocked"
```

Expected: Test **FAILS** because `t3.CurrentColor` is `LOCKED` instead of `NONE`.

```
Assert.Equal() Failure: Expected: NONE, Actual: LOCKED
```

- [ ] **Step 3: Run all existing tests — expect they still PASS**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "ChainGraphEngineTests"
```

Expected: All 36 existing tests pass, only the new test fails.

- [ ] **Step 4: Commit the failing test**

```bash
git add OWLServer/OWLServer.Tests/Unit/GameModes/ChainGraphEngineTests.cs
git commit -m "test: add failing test for neutral junction over-locking in LockDescendants"
```

---

### Task 2: Implement the fix

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs`

- [ ] **Step 1: Add safe predecessor check in LockDescendants**

In the `LockDescendants` method, locate the lock decision point. Currently (around line 134):

```csharp
        bool ownedByPrev = succTower.CurrentColor == previousOwner;
        bool neutral = succTower.CurrentColor == TeamColor.NONE;
        if (!ownedByPrev && !neutral) continue;

        succTower.SetTowerColor(TeamColor.LOCKED);
```

Add the safe-predecessor check between the `if` and the `SetTowerColor` line. The result:

```csharp
        bool ownedByPrev = succTower.CurrentColor == previousOwner;
        bool neutral = succTower.CurrentColor == TeamColor.NONE;
        if (!ownedByPrev && !neutral) continue;

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

        succTower.SetTowerColor(TeamColor.LOCKED);
```

The `System.Linq` import is needed for `.Any()` — this is already available via the existing test imports and standard .NET tooling.

- [ ] **Step 2: Build to verify compilation**

```bash
dotnet build OWLServer/OWLServer/OWLServer.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Run the new test — expect PASS**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "CompleteCapture_CounterCaptureEntry_NeutralJunctionNotLocked"
```

Expected: Test **PASSES**.

- [ ] **Step 4: Run ALL ChainGraphEngine tests — expect all pass**

```bash
dotnet test OWLServer/OWLServer.Tests/OWLServer.Tests.csproj --filter "ChainGraphEngineTests"
```

Expected: All 37 tests pass (36 existing + 1 new). No regressions.

- [ ] **Step 5: Commit the fix**

```bash
git add OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs
git commit -m "fix: LockDescendants skips neutral towers with safe alternative predecessors"
```
