# Capture Neutral Threshold

Add a configurable capture-neutral threshold to Conquest and ChainBreak game modes.

## Motivation

When a team attacks an enemy tower and releases the button after passing a configurable threshold, the tower becomes neutral (NONE) instead of reverting to the enemy. This creates a "point of no return" dynamic — attackers must commit past the threshold or the enemy retains control.

## Properties

Per game mode class (Apporach 1):

```
GameModeConquest.NeutralAtThresholdEnabled    bool   default: true
GameModeConquest.CaptureNeutralThresholdPercent  int  default: 50, range: 1-99

GameModeChainBreak.NeutralAtThresholdEnabled    bool   default: true
GameModeChainBreak.CaptureNeutralThresholdPercent  int  default: 50, range: 1-99
```

## Logic

In both capture-progress loops, after `CaptureProgress` is calculated:

```
if NeutralAtThresholdEnabled
   AND progress_percent >= CaptureNeutralThresholdPercent
   AND tower.CurrentColor != NONE              (already flipped)
   AND tower.CurrentColor != pressing team     (was enemy color)
then
    tower.SetTowerColor(TeamColor.NONE)
```

The `CurrentColor != NONE` guard prevents re-triggering on subsequent ticks.

Progress is unaffected — it continues from threshold% to 100% normally. Full capture at 100% still succeeds and assigns the tower to the pressing team.

## Files changed

| File | Change |
|---|---|
| `Models/GameModes/GameModeConquest.cs` | Add 2 properties + logic in `ProcessConquestStateMachine()` |
| `Models/GameModes/GameModeChainBreak.cs` | Add 2 properties + pass to engine |
| `Models/GameModes/ChainGraphEngine.cs` | Add property + logic in `ProcessTick()` |
| `Components/ConfigComponents/GameModes/GameModeConquestConfig.razor` | Add toggle + threshold numeric (hidden when toggle off) |
| `Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor` | Add toggle + threshold numeric (hidden when toggle off) |

No changes to IGameModeBase, Tower, TowerManagerService, or Database.
