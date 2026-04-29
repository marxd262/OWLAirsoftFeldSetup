# Auto-Start Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add real-time auto-start countdown feedback to the admin dashboard and a player-facing overlay on the map.

**Architecture:** Add computed countdown properties to `GameStateService`, fire `StateHasChangedAction` from the existing `AutoStartGame()` polling loop, create a new `AutoStartOverlay` component (following `GameEndOverlay` patterns), and add conditional UI to `Home.razor` and `AdminPanel.razor`.

**Tech Stack:** .NET 8 Blazor Interactive Server, Radzen.Blazor, inline CSS

---

### Task 1: Add computed properties to GameStateService and interface

**Files:**
- Modify: `OWLServer/OWLServer/Services/GameStateService.cs:27-27`
- Modify: `OWLServer/OWLServer/Services/Interfaces/IGameStateService.cs:20-20`

- [ ] **Step 1: Add computed properties to GameStateService.cs**

Insert after `public int SecondsTillAutoStartAfterReady { get; set; } = 20;` (line 27):

```csharp
        public int? AutoStartSecondsRemaining => 
            AutoStartProcessStarted.HasValue 
                ? Math.Max(0, SecondsTillAutoStartAfterReady - (int)(DateTime.Now - AutoStartProcessStarted.Value).TotalSeconds)
                : null;

        public bool AutoStartCountdownActive => 
            AutoStartAfterReady 
            && AutoStartProcessStarted.HasValue 
            && (DateTime.Now - AutoStartProcessStarted.Value).TotalSeconds < SecondsTillAutoStartAfterReady;

        public bool AutoStartWaitingForSpawns =>
            AutoStartAfterReady && !AutoStartProcessStarted.HasValue;
```

- [ ] **Step 2: Add read-only declarations to IGameStateService.cs**

Insert after `DateTime? AutoStartProcessStarted { get; set; }` (line 20):

```csharp
    int? AutoStartSecondsRemaining { get; }
    bool AutoStartCountdownActive { get; }
    bool AutoStartWaitingForSpawns { get; }
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Services/GameStateService.cs OWLServer/OWLServer/Services/Interfaces/IGameStateService.cs
git commit -m "feat: add auto-start computed properties for countdown feedback"
```

---

### Task 2: Fire StateHasChangedAction in auto-start loop and fix AutoStartProcessStarted reset

**Files:**
- Modify: `OWLServer/OWLServer/Services/GameStateService.cs:59-83`
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/AdminPanel.razor.cs:92-105`

- [ ] **Step 1: Add StateHasChangedAction invocations to AutoStartGame() in GameStateService.cs**

Replace the `AutoStartGame` method (lines 59-83):

```csharp
        public async void AutoStartGame()
        {
            while ((!StadtSpawnReady || !WaldSpawnReady))
            {
                await Task.Delay(100);
                try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }

                if (AutoStartCancellationTokenSrc.IsCancellationRequested) return;
            }
            
            AutoStartProcessStarted = DateTime.Now;

            while ((DateTime.Now - AutoStartProcessStarted).Value.TotalSeconds < SecondsTillAutoStartAfterReady && StadtSpawnReady && WaldSpawnReady)
            {
                if (AutoStartCancellationTokenSrc.IsCancellationRequested) return;
                await Task.Delay(100);
                try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }
            }

            if (!StadtSpawnReady || !WaldSpawnReady)
                return;
            
            StartGame();
            StadtSpawnReady = false;
            WaldSpawnReady = false;
            AutoStartAfterReady = false;
        }
```

Changed: Added `try { ExternalTriggerService.StateHasChangedAction?.Invoke(); } catch { }` inside both while loops, before the cancellation checks (to ensure UI updates are always attempted).

- [ ] **Step 2: Reset AutoStartProcessStarted when toggling auto-start OFF in AdminPanel.razor.cs**

In `ToggleAutoStart`, the `else` branch (lines 99-103), add `GameStateService.AutoStartProcessStarted = null;`:

Change lines 99-103 from:
```csharp
        else
        {
            GameStateService.AutoStartCancellationTokenSrc.Cancel();
            GameStateService.StadtSpawnReady = false;
            GameStateService.WaldSpawnReady = false;
        }
```
To:
```csharp
        else
        {
            GameStateService.AutoStartCancellationTokenSrc.Cancel();
            GameStateService.AutoStartProcessStarted = null;
            GameStateService.StadtSpawnReady = false;
            GameStateService.WaldSpawnReady = false;
        }
```

This ensures `AutoStartWaitingForSpawns` returns correctly when auto-start is re-enabled after being turned off.

- [ ] **Step 3: Build to verify**

```bash
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Services/GameStateService.cs OWLServer/OWLServer/Components/Pages/AdminPages/AdminPanel.razor.cs
git commit -m "feat: fire StateHasChangedAction in auto-start loop, reset AutoStartProcessStarted on toggle off"
```

---

### Task 3: Create AutoStartOverlay.razor player component

**Files:**
- Create: `OWLServer/OWLServer/Components/Pages/AutoStartOverlay.razor`

- [ ] **Step 1: Create the component file**

Create `OWLServer/OWLServer/Components/Pages/AutoStartOverlay.razor`:

```razor
@inject IGameStateService GameStateService
@inject IExternalTriggerService ExternalTriggerService
@implements IDisposable

@if (!(GameStateService.CurrentGame?.IsRunning ?? false) && GameStateService.AutoStartCountdownActive)
{
    <div class="autostart-overlay">
        <div class="autostart-content">
            <div class="autostart-title">Game starting soon</div>
            <div class="autostart-countdown">@GameStateService.AutoStartSecondsRemaining</div>
            <div class="autostart-spawns">
                <span class="spawn-badge @(GameStateService.WaldSpawnReady ? "ready" : "waiting")">
                    Wald: @(GameStateService.WaldSpawnReady ? "Ready" : "Waiting...")
                </span>
                <span class="spawn-badge @(GameStateService.StadtSpawnReady ? "ready" : "waiting")">
                    Stadt: @(GameStateService.StadtSpawnReady ? "Ready" : "Waiting...")
                </span>
            </div>
            <div class="autostart-hint">Countdown resets if a team un-readies</div>
        </div>
    </div>
}

<style>
    .autostart-overlay {
        position: fixed; top: 0; left: 0;
        width: 100vw; height: 100vh;
        background: rgba(0,0,0,0.85);
        z-index: 9998;
        display: flex; align-items: center; justify-content: center;
    }
    .autostart-content {
        background: #0d1f2d; border-radius: 1rem;
        padding: 3rem 4rem; text-align: center; color: white;
        min-width: 360px;
    }
    .autostart-title {
        font-size: 1.4rem; font-weight: bold;
        letter-spacing: .15em; color: #aaa; margin-bottom: .75rem;
        text-transform: uppercase;
    }
    .autostart-countdown {
        font-size: 6rem; font-weight: 900; margin-bottom: 1.5rem;
        color: #ffc107;
    }
    .autostart-spawns {
        display: flex; gap: 1rem; justify-content: center; margin-bottom: 1rem;
    }
    .spawn-badge {
        font-size: 1.2rem; font-weight: bold;
        padding: .4rem 1.2rem; border-radius: .4rem;
    }
    .spawn-badge.ready { background: rgba(0,180,0,.3); color: #4caf50; }
    .spawn-badge.waiting { background: rgba(255,255,255,.1); color: #999; }
    .autostart-hint { font-size: .85rem; color: #666; margin-top: .5rem; }
</style>

@code {
    private Action _stateChangedHandler = null!;

    protected override void OnInitialized()
    {
        _stateChangedHandler = () => InvokeAsync(StateHasChanged);
        ExternalTriggerService.StateHasChangedAction += _stateChangedHandler;
    }

    public void Dispose()
    {
        ExternalTriggerService.StateHasChangedAction -= _stateChangedHandler;
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AutoStartOverlay.razor
git commit -m "feat: add AutoStartOverlay player-facing countdown overlay"
```

---

### Task 4: Integrate overlay into Home.razor and add admin countdown to AdminPanel.razor

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/Home.razor:17-20`
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/AdminPanel.razor:79-81`

- [ ] **Step 1: Add AutoStartOverlay to Home.razor**

After the `GameEndOverlay` block (line 19-20), add:

```razor
<AutoStartOverlay />
```

The resulting Home.razor should have lines 17-21 look like:

```razor
@if (ShowGameEndOverlay)
{
    <GameEndOverlay />
}

<AutoStartOverlay />
```

Note: `AutoStartOverlay` owns its own visibility condition (checks `AutoStartCountdownActive && !game running`), so it renders nothing when not needed. No conditional wrapper in Home.razor.

- [ ] **Step 2: Add admin countdown display to AdminPanel.razor**

After the `RadzenNumeric` for `SecondsTillAutoStartAfterReady` (line 80) and before the closing `</RadzenStack>` (line 81), insert:

```razor
                        @if (GameStateService.AutoStartCountdownActive)
                        {
                            <RadzenBadge BadgeStyle="BadgeStyle.Warning" class="rz-w-auto" Style="font-size: 1.5rem; text-align: center; padding: 0.5rem;">
                                Autostart in: @(GameStateService.AutoStartSecondsRemaining)s
                            </RadzenBadge>
                        }
                        else if (GameStateService.AutoStartWaitingForSpawns)
                        {
                            <RadzenText TextStyle="TextStyle.Body2">Warte bis beide Teams bereit sind...</RadzenText>
                        }
```

Full context for the edit — lines 71-81 should become:

```razor
                <RadzenCard Variant="Variant.Flat" class="rz-my-2">
                    <RadzenStack Orientation="Orientation.Vertical">
                        <RadzenText TextStyle="TextStyle.H6"> Autostarteinstellungen</RadzenText>
                        <RadzenToggleButton
                            @bind-Value="@GameStateService.AutoStartAfterReady"
                            ButtonStyle="ButtonStyle.Danger"
                            Change=ToggleAutoStart
                            ToggleButtonStyle="ButtonStyle.Success"> @(GameStateService.AutoStartAfterReady ? "Autostart" : "kein Autostart")</RadzenToggleButton>
                        <RadzenNumeric @bind-Value="@GameStateService.SecondsTillAutoStartAfterReady">Zeit bis Autostart
                        </RadzenNumeric>
                        @if (GameStateService.AutoStartCountdownActive)
                        {
                            <RadzenBadge BadgeStyle="BadgeStyle.Warning" class="rz-w-auto" Style="font-size: 1.5rem; text-align: center; padding: 0.5rem;">
                                Autostart in: @(GameStateService.AutoStartSecondsRemaining)s
                            </RadzenBadge>
                        }
                        else if (GameStateService.AutoStartWaitingForSpawns)
                        {
                            <RadzenText TextStyle="TextStyle.Body2">Warte bis beide Teams bereit sind...</RadzenText>
                        }
                    </RadzenStack>
                </RadzenCard>
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/Home.razor OWLServer/OWLServer/Components/Pages/AdminPages/AdminPanel.razor
git commit -m "feat: integrate auto-start overlay in Home and countdown badge in AdminPanel"
```
