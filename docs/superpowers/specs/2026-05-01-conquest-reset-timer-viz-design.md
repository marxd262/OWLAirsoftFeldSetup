# Conquest Reset Timer Visualization

Show remaining reset timer on captured towers in Conquest mode on the map.

## Motivation

In Conquest, towers auto-reset to neutral after `ResetsAfterInSeconds`. This is invisible on the map. Show a countdown so players can see at a glance how long a tower will remain captured.

## Design

### Tower model — new computed property

```csharp
[NotMapped]
public int ResetSecondsRemaining =>
    CapturedAt.HasValue && (CurrentColor == TeamColor.RED || CurrentColor == TeamColor.BLUE)
        ? Math.Max(0, ResetsAfterInSeconds - (int)(DateTime.Now - CapturedAt.Value).TotalSeconds)
        : -1;
```

Returns `-1` when the tower is not in a captured state with a pending reset.

### TowerComponent — new parameter + conditional display

Add `[Parameter] public bool ShowResetTimer { get; set; }`. When `ShowResetTimer && resetsRemaining >= 0`:

- **Progress ring:** `Value` shows `(resetsRemaining / ResetsAfterInSeconds) * 100` (depleting ring) instead of `GetDisplayProgress()` which always returns 100 when not pressed
- **Template text:** DisplayLetter stacked above "42s" countdown

When `resetsRemaining < 0` or `ShowResetTimer == false`, falls back to existing behavior.

### MapCanvas — gate to Conquest mode only

Pass `ShowResetTimer` only when active game is Conquest:

```razor
<TowerComponent Tower="tower"
    ShowResetTimer="@(_gameStateService.CurrentGame?.GameMode == GameMode.Conquest)" />
```

MatchScoreBar uses default `false`, unaffected.

## Files changed

| File | Change |
|---|---|
| `Models/Tower.cs` | Add `ResetSecondsRemaining` computed property |
| `Components/Components/TowerComponent.razor` | Add `ShowResetTimer` param, conditional ring value + stacked text |
| `Components/MapComponents/MapCanvas.razor` | Inject `IGameStateService`, pass `ShowResetTimer` flag |
