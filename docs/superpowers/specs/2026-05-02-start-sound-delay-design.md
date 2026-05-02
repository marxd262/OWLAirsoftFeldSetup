# Start Sound Delay

Add a configurable delay between the Start sound playing and the game actually starting. The delay is persisted and settable via the Sound Settings page.

## Design

### Config file

`wwwroot/Sounds/sounddelays.json` — persisted alongside `soundconfig.json`:

```json
{"Start": 5}
```

Default: `{"Start": 0}` — no delay, backward compatible.

### AudioService

New methods on `IAudioService`:

```csharp
int GetDelay(Sounds sound);
void SetDelay(Sounds sound, int seconds);
```

Loaded from `sounddelays.json` at startup, saved on change. Generic interface (any `Sounds` accepts a delay) but only Start has a value.

### GameStateService

`StartGame()` now async: plays Start sound, waits configured seconds, then runs game. `HandleGameEnd()` unchanged.

### SoundTest UI

Delay column (numeric input, Min=0, Max=60) shown only for the Start row.

## Files changed

| File | Change |
|---|---|
| `Services/Interfaces/IAudioService.cs` | Add `GetDelay`, `SetDelay` |
| `Services/AudioService.cs` | Implement delay load/save/add new methods |
| `Services/GameStateService.cs` | `StartGame()` async with delay |
| `Pages/AdminPages/SoundTest.razor` | Delay column |
| `Pages/AdminPages/SoundTest.razor.cs` | Pending delays dict + save logic |
