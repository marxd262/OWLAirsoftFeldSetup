# Game Control Dashboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an admin-facing game dashboard that replaces the "Startseite" tab in `/Admin`, showing pre-game readiness and in-game live monitoring with pause/end/reset controls.

**Architecture:** A new Blazor component `GameControlDashboard.razor` subscribes to `StateHasChangedAction` and renders a responsive card layout with stat cards, team panels (with spawn swap), game controls, a reused `Map` component, and a tower status list. Pause support is added to `IGameModeBase` with implementations in all four game modes.

**Tech Stack:** .NET 8 Blazor Interactive Server, Radzen.Blazor, existing `GameStateService` / `ExternalTriggerService` / `MapService`

---

## File Structure

| File | Action | Role |
|---|---|---|
| `Components/Pages/AdminPages/GameControlDashboard.razor` | Create | Dashboard UI component |
| `Components/Pages/AdminPages/AdminStartPage.razor` | Modify | Wire dashboard into tabs |
| `Models/GameModes/IGameModeBase.cs` | Modify | Add Pause/Resume to interface |
| `Models/GameModes/GameModeConquest.cs` | Modify | Implement Pause |
| `Models/GameModes/GameModeChainBreak.cs` | Modify | Implement Pause |
| `Models/GameModes/GameModeTeamDeathmatch.cs` | Modify | Implement Pause |
| `Models/GameModes/GameModeTimer.cs` | Modify | Implement Pause |

---

### Task 1: Add Pause support to IGameModeBase

**Files:**
- Modify: `OWLServer/Models/GameModes/IGameModeBase.cs`

- [ ] **Step 1: Add Pause methods and property to the interface**

Add `PauseGame()`, `ResumeGame()`, and `IsPaused` to the interface with default implementations so existing modes compile without changes. Also add `PausedDuration` for timer correction.

Replace the file content:

```csharp
namespace OWLServer.Models.GameModes;

public interface IGameModeBase
{
    public string Name { get; set; }
    public int GameDurationInMinutes { get; set; }
    public DateTime? StartTime { get; set; }
    public bool IsRunning { get; set; }
    public bool IsFinished { get; set; }
    public int MaxTickets { get; set; }
    public bool ShowRespawnButton { get; }
    public GameMode GameMode { get;  }
    
    public abstract void RunGame();
    public abstract void EndGame();
    public abstract void ResetGame();
    public abstract TeamColor GetWinner { get; }
    public abstract string ToString();
    public TimeSpan? GetTimer { get; }
    public int GetDisplayPoints(TeamColor color);

    public abstract void FillTeams(List<TeamBase> teams);

    public bool IsPaused { get; set; }
    public TimeSpan PausedDuration { get; set; }
    public DateTime? PauseStartedAt { get; set; }

    public void PauseGame()
    {
        if (!IsRunning || IsPaused) return;
        IsPaused = true;
        PauseStartedAt = DateTime.Now;
    }

    public void ResumeGame()
    {
        if (!IsRunning || !IsPaused) return;
        IsPaused = false;
        if (PauseStartedAt != null)
            PausedDuration += DateTime.Now - PauseStartedAt.Value;
        PauseStartedAt = null;
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Build succeeds. All four game modes compile with the new interface members (default implementations are used).

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/IGameModeBase.cs
git commit -m "feat: add PauseGame/ResumeGame to IGameModeBase with default implementations"
```

---

### Task 2: Implement Pause in GameModeConquest

**Files:**
- Modify: `OWLServer/Models/GameModes/GameModeConquest.cs`

- [ ] **Step 1: Add pause check to Runner loop and correct GetTimer**

Replace the `Runner()` method and `GetTimer` property:

```csharp
private void Runner()
{
    DateTime lastPointDistributed = DateTime.Now;
    while (true)
    {
        Thread.Sleep(200);

        if (_abort.IsCancellationRequested)
        {
            EndGame();
            break;
        }

        if (IsPaused) continue;

        if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now)
        {
            EndGame();
            break;
        }

        ProcessConquestStateMachine();

        if (lastPointDistributed.AddSeconds(PointDistributionFrequencyInSeconds) <= DateTime.Now)
        {
            DistributePoints();
            lastPointDistributed = DateTime.Now;
        }

        if (TeamPoints.Any(e => e.Value >= MaxTickets))
        {
            EndGame();
            break;
        }
    }
}
```

Replace `GetTimer` property:

```csharp
[NotMapped]
public TimeSpan? GetTimer 
{
    get
    {
        if (StartTime == null)
            return new TimeSpan(0, GameDurationInMinutes, 0);
        else if (IsFinished)
            return new TimeSpan(0, 0, 0);
        else if (IsPaused && PauseStartedAt != null)
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - PauseStartedAt.Value + PausedDuration;
        else
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now + PausedDuration;
    }
}
```

- [ ] **Step 2: Clear pause state in ResetGame and RunGame**

In `ResetGame()`, add after `_abort = new CancellationTokenSource();`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

In `RunGame()`, add after `IsRunning = true;`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs
git commit -m "feat: implement pause/resume in Conquest game mode"
```

---

### Task 3: Implement Pause in GameModeChainBreak

**Files:**
- Modify: `OWLServer/Models/GameModes/GameModeChainBreak.cs`

- [ ] **Step 1: Add pause check to Runner loop and correct GetTimer**

Replace the `Runner()` method:

```csharp
private void Runner()
{
    var lastPointDistributed = DateTime.Now;
    while (true)
    {
        Thread.Sleep(200);

        if (_abort.IsCancellationRequested) { EndGame(); break; }

        if (IsPaused) continue;

        if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now) { EndGame(); break; }

        ProcessChainBreakStateMachine();

        if (lastPointDistributed.AddSeconds(PointDistributionFrequencyInMinutes) <= DateTime.Now)
        {
            DistributePoints();
            lastPointDistributed = DateTime.Now;
        }

        if (TeamPoints.Any(e => e.Value >= MaxTickets)) { EndGame(); break; }
    }
}
```

Replace `GetTimer` property:

```csharp
[NotMapped]
public TimeSpan? GetTimer
{
    get
    {
        if (StartTime == null)
            return new TimeSpan(0, GameDurationInMinutes, 0);
        if (IsFinished)
            return new TimeSpan(0, 0, 0);
        if (IsPaused && PauseStartedAt != null)
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - PauseStartedAt.Value + PausedDuration;
        return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now + PausedDuration;
    }
}
```

- [ ] **Step 2: Clear pause state in ResetGame and RunGame**

In `ResetGame()`, add after `_abort = new CancellationTokenSource();`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

In `RunGame()`, add after `IsRunning = true;`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeChainBreak.cs
git commit -m "feat: implement pause/resume in ChainBreak game mode"
```

---

### Task 4: Implement Pause in GameModeTeamDeathmatch

**Files:**
- Modify: `OWLServer/Models/GameModes/GameModeTeamDeathmatch.cs`

- [ ] **Step 1: Add pause check to Runner and correct GetTimer**

Replace the `Runner()` method:

```csharp
private void Runner()
{
    while (true)
    {
        Thread.Sleep(500);

        if (_abort.IsCancellationRequested)
        {
            EndGame();
            break;
        }

        if (IsPaused) continue;

        if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now)
        {
            EndGame();
            break;
        }

        if (TeamDeaths.Any(e => e.Value >= MaxTickets))
        {
            EndGame();
            break;
        }
        
        try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
        catch { }
    }
}
```

Replace `GetTimer` property:

```csharp
public TimeSpan? GetTimer
{
    get
    {
        if (StartTime == null || IsFinished)
            return new TimeSpan(0, IsFinished ? 0 : GameDurationInMinutes, 0);
        if (IsPaused && PauseStartedAt != null)
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - PauseStartedAt.Value + PausedDuration;
        return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now + PausedDuration;
    }
}
```

- [ ] **Step 2: Clear pause state in ResetGame and RunGame**

In `ResetGame()`, add after `_abort = new CancellationTokenSource();`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

In `RunGame()`, add after `IsRunning = true;`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeTeamDeathmatch.cs
git commit -m "feat: implement pause/resume in TeamDeathmatch game mode"
```

---

### Task 5: Implement Pause in GameModeTimer

**Files:**
- Modify: `OWLServer/Models/GameModes/GameModeTimer.cs`

- [ ] **Step 1: Add pause check to Runner and correct GetTimer**

Replace the `Runner()` method:

```csharp
private void Runner()
{
    while (true)
    {
        Thread.Sleep(500);

        if (_abort.IsCancellationRequested)
        {
            EndGame();
            break;
        }

        if (IsPaused) continue;

        if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now)
        {
            EndGame();
            break;
        }
        
        try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
        catch { }
    }
}
```

Replace `GetTimer` property:

```csharp
public TimeSpan? GetTimer
{
    get
    {
        if (StartTime == null || IsFinished)
            return new TimeSpan(0, IsFinished ? 0 : GameDurationInMinutes, 0);
        if (IsPaused && PauseStartedAt != null)
            return StartTime.Value.AddMinutes(GameDurationInMinutes) - PauseStartedAt.Value + PausedDuration;
        return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now + PausedDuration;
    }
}
```

- [ ] **Step 2: Clear pause state in ResetGame and RunGame**

In `ResetGame()`, add after `_abort = new CancellationTokenSource();`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

In `RunGame()`, add after `IsRunning = true;`:

```csharp
IsPaused = false;
PauseStartedAt = null;
PausedDuration = TimeSpan.Zero;
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeTimer.cs
git commit -m "feat: implement pause/resume in Timer game mode"
```

---

### Task 6: Create GameControlDashboard component

**Files:**
- Create: `OWLServer/Components/Pages/AdminPages/GameControlDashboard.razor`

- [ ] **Step 1: Write the dashboard component**

```csharp
@using OWLServer.Models
@using OWLServer.Services
@inject GameStateService GSS
@inject ExternalTriggerService ETS
@implements IDisposable

<RadzenStack Gap="0.75rem">

    @* ─── Top stat cards ─── *@
    <RadzenRow JustifyContent="JustifyContent.Start" AlignItems="AlignItems.Stretch" Gap="0.75rem">
        <RadzenColumn Size="12" SizeMD="4" SizeLG="4">
            <RadzenCard Variant="Variant.Flat">
                <RadzenStack AlignItems="AlignItems.Center" Gap="0.25rem">
                    <RadzenText TextStyle="TextStyle.Caption" Style="color: var(--rz-text-tertiary-color)">Status</RadzenText>
                    <RadzenText TextStyle="TextStyle.H3" Style="@(IsRunning ? "color: var(--rz-success)" : "color: var(--rz-warning)")">
                        @(IsRunning ? "Laufend" : "Warten")
                    </RadzenText>
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="4" SizeLG="4">
            <RadzenCard Variant="Variant.Flat">
                <RadzenStack AlignItems="AlignItems.Center" Gap="0.25rem">
                    <RadzenText TextStyle="TextStyle.Caption" Style="color: var(--rz-text-tertiary-color)">Modus</RadzenText>
                    <RadzenText TextStyle="TextStyle.H4" Style="color: var(--rz-primary)">
                        @(GSS.CurrentGame?.Name ?? "—")
                    </RadzenText>
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeMD="4" SizeLG="4">
            <RadzenCard Variant="Variant.Flat">
                <RadzenStack AlignItems="AlignItems.Center" Gap="0.25rem">
                    <RadzenText TextStyle="TextStyle.Caption" Style="color: var(--rz-text-tertiary-color)">Towers Online</RadzenText>
                    <RadzenText TextStyle="TextStyle.H3" Style="@(TowerOnlineRatio >= 0.75 ? "color: var(--rz-success)" : TowerOnlineRatio >= 0.5 ? "color: var(--rz-warning)" : "color: var(--rz-danger)")">
                        @OnlineTowerCount/@TotalTowerCount
                    </RadzenText>
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    @* ─── Team panels with swap button ─── *@
    <RadzenRow JustifyContent="JustifyContent.SpaceBetween" AlignItems="AlignItems.Stretch" Gap="0">
        <RadzenColumn Size="5" SizeMD="5">
            <RadzenCard Variant="Variant.Flat"
                        Style="@($"border-left: 4px solid {GSS.Teams[TeamColor.BLUE].ColorCssImportant}; border-radius: var(--rz-border-radius); padding: 1rem;")">
                <RadzenStack Gap="0.25rem">
                    <RadzenText TextStyle="TextStyle.Caption" Style="color: var(--rz-text-tertiary-color)">
                        @GSS.Teams[TeamColor.BLUE].Name &middot; @(GSS.TeamInWald == TeamColor.BLUE ? "Wald" : "Stadt")
                    </RadzenText>
                    <RadzenText TextStyle="TextStyle.H2" Style="@($"color: {GSS.Teams[TeamColor.BLUE].ColorCssImportant}")">
                        @if (IsRunning)
                        {
                            @GSS.CurrentGame?.GetDisplayPoints(TeamColor.BLUE)
                        }
                        else
                        {
                            @(GSS.WaldSpawnReady && GSS.TeamInWald == TeamColor.BLUE ? "BEREIT"
                            : GSS.StadtSpawnReady && GSS.TeamInStadt == TeamColor.BLUE ? "BEREIT"
                            : "NICHT BEREIT")
                        }
                    </RadzenText>
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="2" SizeMD="2" Style="display: flex; align-items: center; justify-content: center;">
            @if (!IsRunning)
            {
                <RadzenButton Icon="swap_horiz" ButtonStyle="ButtonStyle.Secondary"
                              Click="@SwapSpawns" Size="ButtonSize.Medium"
                              Style="width: 44px; height: 44px;" />
            }
        </RadzenColumn>
        <RadzenColumn Size="5" SizeMD="5">
            <RadzenCard Variant="Variant.Flat"
                        Style="@($"border-right: 4px solid {GSS.Teams[TeamColor.RED].ColorCssImportant}; border-radius: var(--rz-border-radius); padding: 1rem; text-align: right;")">
                <RadzenStack Gap="0.25rem" AlignItems="AlignItems.End">
                    <RadzenText TextStyle="TextStyle.Caption" Style="color: var(--rz-text-tertiary-color)">
                        @GSS.Teams[TeamColor.RED].Name &middot; @(GSS.TeamInStadt == TeamColor.RED ? "Stadt" : "Wald")
                    </RadzenText>
                    <RadzenText TextStyle="TextStyle.H2" Style="@($"color: {GSS.Teams[TeamColor.RED].ColorCssImportant}")">
                        @if (IsRunning)
                        {
                            @GSS.CurrentGame?.GetDisplayPoints(TeamColor.RED)
                        }
                        else
                        {
                            @(GSS.StadtSpawnReady && GSS.TeamInStadt == TeamColor.RED ? "BEREIT"
                            : GSS.WaldSpawnReady && GSS.TeamInWald == TeamColor.RED ? "BEREIT"
                            : "NICHT BEREIT")
                        }
                    </RadzenText>
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    @* ─── Timer (in-game only) ─── *@
    @if (IsRunning)
    {
        <RadzenRow>
            <RadzenColumn Size="12">
                <RadzenCard Variant="Variant.Flat">
                    <RadzenStack AlignItems="AlignItems.Center" Gap="0.25rem">
                        <RadzenText TextStyle="TextStyle.Caption" Style="color: var(--rz-text-tertiary-color)">Game Time</RadzenText>
                        <RadzenText TextStyle="TextStyle.H3" Style="font-family: monospace;">
                            @GSS.CurrentGame?.GetTimer?.ToString(@"mm\:ss")
                        </RadzenText>
                        @if (GSS.CurrentGame?.IsPaused == true)
                        {
                            <RadzenBadge BadgeStyle="BadgeStyle.Warning" Text="PAUSED" />
                        }
                    </RadzenStack>
                </RadzenCard>
            </RadzenColumn>
        </RadzenRow>
    }

    @* ─── Controls ─── *@
    <RadzenRow JustifyContent="JustifyContent.Center" Gap="0.5rem">
        @if (IsRunning)
        {
            <RadzenButton ButtonStyle="ButtonStyle.Warning"
                          Text="@(GSS.CurrentGame?.IsPaused == true ? "Fortsetzen" : "Pause")"
                          Click="@TogglePause" />
            <RadzenButton ButtonStyle="ButtonStyle.Danger" Text="Spiel Beenden"
                          Click="@EndGame" />
            <RadzenButton ButtonStyle="ButtonStyle.Secondary" Text="Reset"
                          Click="@ResetGame" />
        }
        else
        {
            <RadzenButton ButtonStyle="ButtonStyle.Success" Text="Spiel Starten"
                          Click="@StartGame" />
        }
    </RadzenRow>

    @* ─── Map + Tower status ─── *@
    <RadzenRow AlignItems="AlignItems.Start" Gap="0.75rem">
        <RadzenColumn Size="12" SizeLG="7">
            <RadzenCard Variant="Variant.Flat" Style="padding: 0.5rem;">
                <Map />
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="12" SizeLG="5">
            <RadzenCard Variant="Variant.Flat" Style="max-height: 400px; overflow-y: auto;">
                <RadzenStack Gap="0.25rem">
                    <RadzenText TextStyle="TextStyle.H6">Tower Status</RadzenText>
                    @foreach (var tower in GSS.TowerManagerService.Towers.Values.OrderBy(t => t.DisplayLetter))
                    {
                        <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                                     Gap="0.5rem" Style="padding: 0.4rem 0.5rem; border-radius: var(--rz-border-radius); background: var(--rz-surface-hover-color);">
                            <span style="@($"width: 8px; height: 8px; border-radius: 50%; display: inline-block; background: {(tower.TowerOnline ? "var(--rz-success)" : "var(--rz-danger)")};")"></span>
                            <RadzenText TextStyle="TextStyle.Caption" Style="min-width: 60px;">
                                @(string.IsNullOrEmpty(tower.DisplayLetter) ? tower.MacAddress : $"Tower {tower.DisplayLetter}")
                            </RadzenText>
                            @if (IsRunning && tower.IsPressed)
                            {
                                <span style="margin-left: auto; width: 50px; height: 8px; border-radius: 4px; background: var(--rz-surface-color);">
                                    <span style="@($"display: block; width: {tower.CaptureProgress * 100}%; height: 100%; border-radius: 4px; background: {GetTeamColorHex(tower.PressedByColor)};")"></span>
                                </span>
                            }
                            else if (!tower.TowerOnline)
                            {
                                <RadzenText TextStyle="TextStyle.Caption" Style="margin-left: auto; color: var(--rz-text-tertiary-color);">offline</RadzenText>
                            }
                            else
                            {
                                <span style="@($"margin-left: auto; width: 12px; height: 12px; border-radius: 2px; background: {GetTeamColorHex(tower.CurrentColor)};")"></span>
                            }
                        </RadzenStack>
                    }
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

</RadzenStack>

@code {
    private Action _stateChangedHandler = null!;

    private bool IsRunning => GSS.CurrentGame?.IsRunning ?? false;

    private int OnlineTowerCount =>
        GSS.TowerManagerService.Towers.Values.Count(t => t.TowerOnline);

    private int TotalTowerCount =>
        GSS.TowerManagerService.Towers.Count;

    private double TowerOnlineRatio =>
        TotalTowerCount == 0 ? 1.0 : (double)OnlineTowerCount / TotalTowerCount;

    protected override void OnInitialized()
    {
        _stateChangedHandler = () => InvokeAsync(StateHasChanged);
        ETS.StateHasChangedAction += _stateChangedHandler;
    }

    public void Dispose()
    {
        ETS.StateHasChangedAction -= _stateChangedHandler;
    }

    private void StartGame()
    {
        GSS.StartGame();
    }

    private void EndGame()
    {
        GSS.StopGame();
    }

    private void ResetGame()
    {
        GSS.Reset();
    }

    private void TogglePause()
    {
        if (GSS.CurrentGame?.IsPaused == true)
            GSS.CurrentGame.ResumeGame();
        else
            GSS.CurrentGame?.PauseGame();
    }

    private void SwapSpawns()
    {
        var wald = GSS.TeamInWald;
        GSS.TeamInWald = GSS.TeamInStadt;
        GSS.TeamInStadt = wald;
        ETS.StateHasChangedAction.Invoke();
    }

    private string GetTeamColorHex(TeamColor color) => color switch
    {
        TeamColor.RED => "#fc1911",
        TeamColor.BLUE => "#00b4f1",
        TeamColor.LOCKED => "#FFD700",
        TeamColor.NONE => "#6b7280",
        _ => "#6b7280"
    };
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/GameControlDashboard.razor
git commit -m "feat: add GameControlDashboard component with admin game overview"
```

---

### Task 7: Wire dashboard into AdminStartPage

**Files:**
- Modify: `OWLServer/Components/Pages/AdminPages/AdminStartPage.razor`

- [ ] **Step 1: Replace Home component with GameControlDashboard**

Replace:

```csharp
<RadzenTabsItem Text="Startseite">
    <Home ShowGameEndOverlay="false"/>
</RadzenTabsItem>
```

With:

```csharp
<RadzenTabsItem Text="@(GameStateService.CurrentGame?.IsRunning == true ? "Spiel-Übersicht" : "Startseite")">
    <GameControlDashboard/>
</RadzenTabsItem>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/AdminStartPage.razor
git commit -m "feat: wire GameControlDashboard into AdminStartPage tabs"
```

---

### Task 8: Final verification build

- [ ] **Step 1: Full build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`

Expected: Zero errors.

- [ ] **Step 2: Verify all files are committed**

Run: `git status`

Expected: Clean working tree (no uncommitted changes).
```

