# Capture Neutral Threshold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-game-mode configurable neutral threshold to Conquest and ChainBreak: when capturing from an enemy tower, the tower flips to neutral (NONE) at a configurable % threshold instead of staying the enemy's color.

**Architecture:** Add two properties to each game mode class (`NeutralAtThresholdEnabled`, `CaptureNeutralThresholdPercent`), modify the capture loop in each to flip tower color at the threshold, and add UI toggles in the existing config components.

**Tech Stack:** C# / .NET 8 Blazor, Radzen.Blazor

---

### Task 1: GameModeConquest — Add properties and logic

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs`

- [ ] **Step 1: Add properties to GameModeConquest**

After line 26 (`public int PointDistributionFrequencyInSeconds { get; set; } = 5;`), insert:

```csharp
public bool NeutralAtThresholdEnabled { get; set; } = true;
public int CaptureNeutralThresholdPercent { get; set; } = 50;
```

- [ ] **Step 2: Add neutral threshold logic in ProcessConquestStateMachine**

In the `else` block of the capture-in-progress loop (after `tower.CaptureProgress = ...` line ~243, before the closing `}`), insert:

```csharp
if (NeutralAtThresholdEnabled
    && tower.CaptureProgress * 100 >= CaptureNeutralThresholdPercent
    && tower.CurrentColor != TeamColor.NONE
    && tower.CurrentColor != tower.PressedByColor)
{
    tower.SetTowerColor(TeamColor.NONE);
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`

Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs
git commit -m "feat: add neutral threshold support to Conquest game mode"
```

---

### Task 2: ChainGraphEngine — Add property and logic

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs`

- [ ] **Step 1: Add properties to ChainGraphEngine**

After line 22 (`public IReadOnlyDictionary<string, int> DepthMap => _depthMap;`), insert:

```csharp
public bool NeutralAtThresholdEnabled { get; set; } = true;
public int CaptureNeutralThresholdPercent { get; set; } = 50;
```

- [ ] **Step 2: Add neutral threshold logic in ProcessTick**

In the `else` block of ProcessTick (after `tower.CaptureProgress = ...` line ~276, before the `updates.Add(...)` on line 277), insert:

```csharp
if (NeutralAtThresholdEnabled
    && tower.CaptureProgress * 100 >= CaptureNeutralThresholdPercent
    && tower.CurrentColor != TeamColor.NONE
    && tower.CurrentColor != pressingTeam)
{
    tower.SetTowerColor(TeamColor.NONE);
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/ChainGraphEngine.cs
git commit -m "feat: add neutral threshold support to ChainGraphEngine"
```

---

### Task 3: GameModeChainBreak — Add properties and pass to engine

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeChainBreak.cs`

- [ ] **Step 1: Add properties to GameModeChainBreak**

After line 18 (`public int PointDistributionFrequencyInSeconds { get; set; } = 5;`), insert:

```csharp
public bool NeutralAtThresholdEnabled { get; set; } = true;
public int CaptureNeutralThresholdPercent { get; set; } = 50;
```

- [ ] **Step 2: Pass properties to engine in RunGame**

In `RunGame()`, after line 88 (`Engine = new ChainGraphEngine(ActiveChainLayout, GameStateService.TowerManagerService.Towers);`), insert:

```csharp
Engine.NeutralAtThresholdEnabled = NeutralAtThresholdEnabled;
Engine.CaptureNeutralThresholdPercent = CaptureNeutralThresholdPercent;
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeChainBreak.cs
git commit -m "feat: add neutral threshold support to ChainBreak game mode"
```

---

### Task 4: UI — Conquest config

**Files:**
- Modify: `OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeConquestConfig.razor`

- [ ] **Step 1: Add toggle and threshold fields**

After the existing tickbox section (lines 13-14: `RadzenCheckBox` for IsTicket), insert:

```razor
<RadzenFormField Text="Neutral at Threshold" Variant="Variant.Flat">
    <RadzenCheckBox @bind-Value="@CurrentGame.NeutralAtThresholdEnabled"/>
</RadzenFormField>

@if (CurrentGame.NeutralAtThresholdEnabled)
{
    <RadzenFormField Text="Neutral Threshold (%)" Variant="Variant.Flat">
        <RadzenNumeric @bind-Value="@CurrentGame.CaptureNeutralThresholdPercent" Min="1" Max="99"/>
    </RadzenFormField>
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeConquestConfig.razor
git commit -m "feat: add neutral threshold UI to Conquest config"
```

---

### Task 5: UI — ChainBreak config

**Files:**
- Modify: `OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor`

- [ ] **Step 1: Add toggle and threshold fields**

After the ChainFactor numeric (lines 20-22), insert:

```razor
<RadzenFormField Text="Neutral at Threshold" Variant="Variant.Flat">
    <RadzenCheckBox @bind-Value="@CurrentGame.NeutralAtThresholdEnabled"/>
</RadzenFormField>

@if (CurrentGame.NeutralAtThresholdEnabled)
{
    <RadzenFormField Text="Neutral Threshold (%)" Variant="Variant.Flat">
        <RadzenNumeric @bind-Value="@CurrentGame.CaptureNeutralThresholdPercent" Min="1" Max="99"/>
    </RadzenFormField>
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build --project OWLServer/OWLServer`

Expected: Build succeeds.

- [ ] **Step 3 (final): Final verification build and commit**

```bash
dotnet build --project OWLServer/OWLServer
git add OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor
git commit -m "feat: add neutral threshold UI to ChainBreak config"
```
