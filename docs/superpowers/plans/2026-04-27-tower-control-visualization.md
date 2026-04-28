# Tower Control Visualization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the runtime-only (`[NotMapped]`) tower control/lock system with a persisted `TowerControlLayout`/`TowerControlLink` model that mirrors the ChainBreak pattern end-to-end.

**Architecture:** New persisted models mirroring `ChainLayout`/`ChainLink`; lock/unlock logic moves from `TowerManagerService.ProcessTowerStateMachine()` into `GameModeConquest.ProcessConquestStateMachine()`; `TowerControlConfig.razor` admin component parallels `GameModeChainBreakConfig.razor`; SVG visualization in `Map.razor` uses the same pattern as the ChainBreak arrow block.

**Tech Stack:** .NET 8 Blazor, EF Core 9 + SQLite, Radzen.Blazor 10.3.1

---

### Task 1: Create TowerControlLayout and TowerControlLink models

**Files:**
- Create: `OWLServer/OWLServer/Models/TowerControlLayout.cs`
- Create: `OWLServer/OWLServer/Models/TowerControlLink.cs`

- [ ] **Step 1: Create TowerControlLayout.cs**

```csharp
namespace OWLServer.Models;

public class TowerControlLayout
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TowerControlLink> Links { get; set; } = new();
}
```

- [ ] **Step 2: Create TowerControlLink.cs**

```csharp
namespace OWLServer.Models;

public class TowerControlLink
{
    public int Id { get; set; }
    public int TowerControlLayoutId { get; set; }
    public string ControllerTowerMacAddress { get; set; } = string.Empty;
    public string ControlledTowerMacAddress { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Models/TowerControlLayout.cs OWLServer/OWLServer/Models/TowerControlLink.cs
git commit -m "feat: add TowerControlLayout and TowerControlLink models"
```

---

### Task 2: Update DatabaseContext with TowerControl DbSets

**Files:**
- Modify: `OWLServer/OWLServer/Context/DatabaseContext.cs`

- [ ] **Step 1: Add DbSets and model configuration**

Add after line 11:
```csharp
public DbSet<TowerControlLayout> TowerControlLayouts { get; set; }
public DbSet<TowerControlLink> TowerControlLinks { get; set; }
```

In `OnModelCreating`, after the ChainLink config (line 44), add:
```csharp
builder.Entity<TowerControlLayout>(e =>
{
    e.HasKey(tcl => tcl.Id);
    e.HasMany(tcl => tcl.Links)
     .WithOne()
     .HasForeignKey(lnk => lnk.TowerControlLayoutId)
     .OnDelete(DeleteBehavior.Cascade);
});
builder.Entity<TowerControlLink>(e =>
{
    e.HasKey(lnk => lnk.Id);
});
```

- [ ] **Step 2: Commit**

```bash
git add OWLServer/OWLServer/Context/DatabaseContext.cs
git commit -m "feat: add TowerControlLayout/Link DbSets to DatabaseContext"
```

---

### Task 3: Update GameModeConquest with control state machine

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs`

- [ ] **Step 1: Add ActiveControlLayout property and runtime graph fields**

After line 28 (`private CancellationTokenSource _abort = new();`), add:
```csharp
public TowerControlLayout? ActiveControlLayout { get; set; }

// Runtime control graph — built at RunGame()
private Dictionary<string, List<string>> _controlledChildren = new();
private Dictionary<string, string> _controllerByChild = new();
```

- [ ] **Step 2: Add BuildControlMaps()**

```csharp
private void BuildControlMaps()
{
    _controlledChildren = new Dictionary<string, List<string>>();
    _controllerByChild = new Dictionary<string, string>();

    if (ActiveControlLayout == null) return;

    foreach (var link in ActiveControlLayout.Links)
    {
        if (!_controlledChildren.ContainsKey(link.ControllerTowerMacAddress))
            _controlledChildren[link.ControllerTowerMacAddress] = new List<string>();
        _controlledChildren[link.ControllerTowerMacAddress].Add(link.ControlledTowerMacAddress);
        _controllerByChild[link.ControlledTowerMacAddress] = link.ControllerTowerMacAddress;
    }
}
```

- [ ] **Step 3: Add InitializeControlTowerStates()**

```csharp
private void InitializeControlTowerStates()
{
    var controlledMacs = ActiveControlLayout?.Links
        .Select(l => l.ControlledTowerMacAddress)
        .ToHashSet() ?? new HashSet<string>();

    foreach (var tower in GameStateService.TowerManagerService.Towers.Values)
    {
        if (controlledMacs.Contains(tower.MacAddress))
            tower.SetTowerColor(TeamColor.LOCKED);
        else
            tower.SetTowerColor(TeamColor.NONE);
    }
}
```

- [ ] **Step 4: Add ProcessConquestStateMachine()**

```csharp
private void ProcessConquestStateMachine()
{
    var towers = GameStateService.TowerManagerService.Towers;

    // Lock: controller reset timer expired
    foreach (var tower in towers.Values.Where(t =>
        _controlledChildren.ContainsKey(t.MacAddress)
        && t.CurrentColor != TeamColor.NONE
        && t.CapturedAt != null
        && t.CapturedAt?.AddSeconds(t.ResetsAfterInSeconds) < DateTime.Now).ToList())
    {
        tower.SetTowerColor(TeamColor.NONE);

        foreach (string childMac in _controlledChildren[tower.MacAddress])
        {
            if (towers.TryGetValue(childMac, out var child) && child.CurrentColor == TeamColor.NONE)
                child.SetTowerColor(TeamColor.LOCKED);
        }

        tower.CapturedAt = null;
    }

    // Capture in progress
    foreach (var tower in towers.Values.Where(t => t.IsPressed).ToList())
    {
        // Guard: if controlled, check controller's ownership
        if (_controllerByChild.TryGetValue(tower.MacAddress, out var controllerMac)
            && towers.TryGetValue(controllerMac, out var controllerTower)
            && controllerTower.CurrentColor != tower.PressedByColor)
        {
            tower.IsPressed = false;
            tower.LastPressed = null;
            tower.PressedByColor = TeamColor.NONE;
            tower.CaptureProgress = 0;
            continue;
        }

        if (tower.LastPressed?.AddSeconds(tower.TimeToCaptureInSeconds) < DateTime.Now)
        {
            tower.SetTowerColor(tower.PressedByColor);
            tower.CapturedAt = DateTime.Now;
            tower.IsPressed = false;
            tower.LastPressed = null;
            tower.PressedByColor = TeamColor.NONE;
            tower.CaptureProgress = 1;

            // Unlock children
            if (_controlledChildren.TryGetValue(tower.MacAddress, out var children))
            {
                foreach (string childMac in children)
                {
                    if (towers.TryGetValue(childMac, out var child))
                        child.SetTowerColor(TeamColor.NONE);
                }
            }
        }
        else
        {
            var elapsed = DateTime.Now - tower.LastPressed;
            tower.CaptureProgress = elapsed?.TotalSeconds / tower.TimeToCaptureInSeconds ?? 0;
        }
    }

    ExternalTriggerService.StateHasChangedAction?.Invoke();
}
```

- [ ] **Step 5: Update RunGame() to call BuildControlMaps + InitializeControlTowerStates**

Replace existing RunGame():
```csharp
public void RunGame()
{
    BuildControlMaps();
    InitializeControlTowerStates();
    StartTime = DateTime.Now;
    IsRunning = true;
    Task.Run(Runner, _abort.Token);
}
```

- [ ] **Step 6: Update Runner() to call ProcessConquestStateMachine**

In the runner loop, replace:
```
GameStateService.TowerManagerService.ProcessTowerStateMachine();
```
with:
```
ProcessConquestStateMachine();
```

- [ ] **Step 7: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs
git commit -m "feat: add control state machine to GameModeConquest"
```

---

### Task 4: Simplify Tower.cs — remove runtime control state

**Files:**
- Modify: `OWLServer/OWLServer/Models/Tower.cs`

- [ ] **Step 1: Remove `[NotMapped]` control properties**

Delete:
```csharp
[NotMapped]
public bool IsControlled { get; set; }

[NotMapped]
public string? IsControlledByID { get; set; }

[NotMapped]
public bool IsForControlling => ControllsTowerID.Any();

[NotMapped]
public List<string> ControllsTowerID { get; set; } = new();
```

- [ ] **Step 2: Simplify SetToStartColor()**

Replace:
```csharp
public void SetToStartColor()
{
    if (IsControlled)
    {
        SetTowerColor(TeamColor.LOCKED);
    }
    else
    {
        SetTowerColor(TeamColor.NONE);
    }
}
```
with:
```csharp
public void SetToStartColor()
{
    SetTowerColor(TeamColor.NONE);
}
```

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Models/Tower.cs
git commit -m "refactor: remove runtime-only control properties from Tower"
```

---

### Task 5: Clean up TowerManagerService

**Files:**
- Modify: `OWLServer/OWLServer/Services/TowerManagerService.cs`

- [ ] **Step 1: Remove ProcessTowerStateMachine()**

Delete the entire method (lines 19-73).

- [ ] **Step 2: Commit**

```bash
git add OWLServer/OWLServer/Services/TowerManagerService.cs
git commit -m "refactor: remove ProcessTowerStateMachine from TowerManagerService (moved to GameModeConquest)"
```

---

### Task 6: Create TowerControlConfig admin component

**Files:**
- Create: `OWLServer/OWLServer/Components/ConfigComponents/GameModes/TowerControlConfig.razor`
- Create: `OWLServer/OWLServer/Components/ConfigComponents/GameModes/TowerControlConfig.razor.cs`

- [ ] **Step 1: Create TowerControlConfig.razor**

```razor
@using OWLServer.Models
@using OWLServer.Models.GameModes
@inject GameStateService GameStateService
@inject IDbContextFactory<OWLServer.Context.DatabaseContext> DbFactory

<RadzenStack Orientation="Orientation.Vertical" Gap="1rem">

    @* ── Active layout badge ── *@
    @if (CurrentGame.ActiveControlLayout != null)
    {
        <RadzenAlert AlertStyle="AlertStyle.Success" AllowClose="false">
            Aktives Layout: <strong>@CurrentGame.ActiveControlLayout.Name</strong>
            (@(CurrentGame.ActiveControlLayout.Links.Count) Links)
        </RadzenAlert>
    }
    else
    {
        <RadzenAlert AlertStyle="AlertStyle.Warning" AllowClose="false">
            Kein Steuerungs-Layout aktiv — alle Tower frei einnehmbar.
        </RadzenAlert>
    }

    @* ── Saved layouts list ── *@
    <RadzenCard Variant="Variant.Outlined">
        <RadzenText TextStyle="TextStyle.Subtitle2" class="rz-mb-2">Gespeicherte Layouts</RadzenText>
        @if (!_savedLayouts.Any())
        {
            <RadzenText TextStyle="TextStyle.Body2">Noch keine Layouts gespeichert.</RadzenText>
        }
        else
        {
            @foreach (var savedLayout in _savedLayouts)
            {
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                             JustifyContent="JustifyContent.SpaceBetween" class="rz-mb-1">
                    <RadzenText>@savedLayout.Name (@(savedLayout.Links.Count) Links)</RadzenText>
                    <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem">
                        <RadzenButton Text="Laden" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Secondary"
                                      Click="@(() => LoadLayoutIntoEditor(savedLayout))"/>
                        <RadzenButton Text="Aktivieren" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Success"
                                      Click="@(() => ActivateLayout(savedLayout))"/>
                        <RadzenButton Text="Löschen" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Danger"
                                      Click="@(async () => await DeleteLayout(savedLayout))"/>
                    </RadzenStack>
                </RadzenStack>
            }
        }
    </RadzenCard>

    @* ── Layout editor ── *@
    <RadzenCard Variant="Variant.Outlined">
        <RadzenText TextStyle="TextStyle.Subtitle2" class="rz-mb-2">Layout-Editor</RadzenText>

        @if (_editorLinks.Any())
        {
            @foreach (var link in _editorLinks.ToList())
            {
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                             JustifyContent="JustifyContent.SpaceBetween" class="rz-mb-1">
                    <RadzenText>@LinkLabel(link)</RadzenText>
                    <RadzenButton Icon="delete" ButtonStyle="ButtonStyle.Danger" Size="ButtonSize.Small"
                                  Click="@(() => RemoveLink(link))"/>
                </RadzenStack>
            }
        }
        else
        {
            <RadzenText TextStyle="TextStyle.Body2" class="rz-mb-2">Noch keine Links im Editor.</RadzenText>
        }

        <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem" Wrap="FlexWrap.Wrap" class="rz-mt-2">
            <RadzenDropDown TValue="string" @bind-Value="_controllerMac"
                            Data="GameStateService.TowerManagerService.Towers.Values"
                            TextProperty="DisplayLetter"
                            ValueProperty="MacAddress"
                            Placeholder="Kontrolliert von"
                            Style="min-width:120px"/>
            <RadzenDropDown TValue="string" @bind-Value="_controlledMac"
                            Data="GameStateService.TowerManagerService.Towers.Values"
                            TextProperty="DisplayLetter"
                            ValueProperty="MacAddress"
                            Placeholder="Kontrollierter Tower"
                            Style="min-width:120px"/>
            <RadzenButton Text="Link hinzufügen" Icon="add" ButtonStyle="ButtonStyle.Primary"
                          Click="AddLink"/>
        </RadzenStack>

        <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem" class="rz-mt-3" Wrap="FlexWrap.Wrap">
            <RadzenTextBox @bind-Value="_newLayoutName" Placeholder="Layout-Name" Style="flex:1;min-width:150px"/>
            <RadzenButton Text="Neu speichern" Icon="save" ButtonStyle="ButtonStyle.Info"
                          Click="@(async () => await SaveAsNew())"/>
            @if (_editingLayoutId != null)
            {
                <RadzenButton Text="Aktualisieren" Icon="update" ButtonStyle="ButtonStyle.Warning"
                              Click="@(async () => await UpdateExisting())"/>
            }
        </RadzenStack>
    </RadzenCard>

    @* ── Towers outside layout info ── *@
    @{
        var allMacs = GameStateService.TowerManagerService.Towers.Keys.ToHashSet();
        var inLayout = _editorLinks
            .SelectMany(l => new[] { l.ControllerTowerMacAddress, l.ControlledTowerMacAddress })
            .ToHashSet();
        var outsideCount = allMacs.Except(inLayout).Count();
    }
    @if (outsideCount > 0)
    {
        <RadzenText TextStyle="TextStyle.Caption" Style="color:var(--rz-text-secondary-color)">
            @outsideCount Tower außerhalb des Layouts — frei einnehmbar.
        </RadzenText>
    }

</RadzenStack>
```

- [ ] **Step 2: Create TowerControlConfig.razor.cs**

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class TowerControlConfig : ComponentBase
{
    [Parameter] public GameModeConquest CurrentGame { get; set; } = null!;

    [Inject] public GameStateService GameStateService { get; set; } = null!;
    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;

    private List<TowerControlLayout> _savedLayouts = new();

    // Editor state
    private List<TowerControlLink> _editorLinks = new();
    private int? _editingLayoutId;
    private string _newLayoutName = string.Empty;

    // New-link form
    private string _controllerMac = string.Empty;
    private string _controlledMac = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadSavedLayouts();
    }

    private async Task LoadSavedLayouts()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _savedLayouts = await db.TowerControlLayouts.Include(tcl => tcl.Links).ToListAsync();
    }

    private void LoadLayoutIntoEditor(TowerControlLayout layout)
    {
        _editingLayoutId = layout.Id;
        _newLayoutName = layout.Name;
        _editorLinks = layout.Links
            .Select(l => new TowerControlLink
            {
                ControllerTowerMacAddress = l.ControllerTowerMacAddress,
                ControlledTowerMacAddress = l.ControlledTowerMacAddress
            })
            .ToList();
    }

    private void AddLink()
    {
        if (string.IsNullOrEmpty(_controllerMac) || string.IsNullOrEmpty(_controlledMac)) return;
        if (_controllerMac == _controlledMac) return;
        _editorLinks.Add(new TowerControlLink
        {
            ControllerTowerMacAddress = _controllerMac,
            ControlledTowerMacAddress = _controlledMac
        });
        _controllerMac = string.Empty;
        _controlledMac = string.Empty;
    }

    private void RemoveLink(TowerControlLink link) => _editorLinks.Remove(link);

    private async Task SaveAsNew()
    {
        if (string.IsNullOrWhiteSpace(_newLayoutName)) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = new TowerControlLayout
        {
            Name = _newLayoutName,
            Links = _editorLinks.Select(l => new TowerControlLink
            {
                ControllerTowerMacAddress = l.ControllerTowerMacAddress,
                ControlledTowerMacAddress = l.ControlledTowerMacAddress
            }).ToList()
        };
        db.TowerControlLayouts.Add(layout);
        await db.SaveChangesAsync();
        _editingLayoutId = layout.Id;
        await LoadSavedLayouts();
    }

    private async Task UpdateExisting()
    {
        if (_editingLayoutId == null) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = await db.TowerControlLayouts.Include(tcl => tcl.Links)
                         .FirstOrDefaultAsync(tcl => tcl.Id == _editingLayoutId);
        if (layout == null) return;
        layout.Name = _newLayoutName;
        db.TowerControlLinks.RemoveRange(layout.Links);
        layout.Links = _editorLinks.Select(l => new TowerControlLink
        {
            ControllerTowerMacAddress = l.ControllerTowerMacAddress,
            ControlledTowerMacAddress = l.ControlledTowerMacAddress
        }).ToList();
        await db.SaveChangesAsync();
        await LoadSavedLayouts();
        if (CurrentGame.ActiveControlLayout?.Id == _editingLayoutId)
            CurrentGame.ActiveControlLayout = _savedLayouts.FirstOrDefault(sl => sl.Id == _editingLayoutId);
    }

    private async Task DeleteLayout(TowerControlLayout layout)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var entity = await db.TowerControlLayouts.FindAsync(layout.Id);
        if (entity != null)
        {
            db.TowerControlLayouts.Remove(entity);
            await db.SaveChangesAsync();
        }
        if (CurrentGame.ActiveControlLayout?.Id == layout.Id)
            CurrentGame.ActiveControlLayout = null;
        if (_editingLayoutId == layout.Id)
        {
            _editingLayoutId = null;
            _editorLinks.Clear();
            _newLayoutName = string.Empty;
        }
        await LoadSavedLayouts();
    }

    private void ActivateLayout(TowerControlLayout layout)
    {
        CurrentGame.ActiveControlLayout = layout;
    }

    private string TowerLabel(string mac)
    {
        if (GameStateService.TowerManagerService.Towers.TryGetValue(mac, out var t))
            return $"{t.DisplayLetter} – {t.Name}";
        return mac;
    }

    private string LinkLabel(TowerControlLink link)
    {
        return $"{TowerLabel(link.ControllerTowerMacAddress)} → {TowerLabel(link.ControlledTowerMacAddress)}";
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/GameModes/TowerControlConfig.razor OWLServer/OWLServer/Components/ConfigComponents/GameModes/TowerControlConfig.razor.cs
git commit -m "feat: add TowerControlConfig admin component"
```

---

### Task 7: Embed TowerControlConfig in GameModeConquestConfig

**Files:**
- Modify: `OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeConquestConfig.razor`

- [ ] **Step 1: Add TowerControlConfig reference**

Replace the existing content:
```razor
@using OWLServer.Models.GameModes

<RadzenStack Orientation="Orientation.Vertical" Gap="1rem">
    <RadzenStack>
        <RadzenFormField Text="Game Duration" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="@CurrentGame.GameDurationInMinutes"/>
        </RadzenFormField>
        <RadzenFormField Text="Max Tickets" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="@CurrentGame.MaxTickets"/>
        </RadzenFormField>
        <RadzenFormField Text="Is Ticket" Variant="Variant.Flat">
            <RadzenCheckBox @bind-Value="@CurrentGame.IsTicket"/>
        </RadzenFormField>
    </RadzenStack>

    <RadzenCard Variant="Variant.Outlined">
        <RadzenText TextStyle="TextStyle.Subtitle2" class="rz-mb-2">Tower-Steuerung (Control-Layout)</RadzenText>
        <TowerControlConfig CurrentGame="@CurrentGame" />
    </RadzenCard>
</RadzenStack>
```

- [ ] **Step 2: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeConquestConfig.razor
git commit -m "feat: embed TowerControlConfig in GameModeConquestConfig"
```

---

### Task 8: Update Map.razor visualization

**Files:**
- Modify: `OWLServer/OWLServer/Components/MapComponents/Map.razor`

- [ ] **Step 1: Replace the existing control-arrow SVG block**

Remove lines 46-76 (the `@foreach (var src in ControllingSortedTowers)` block and its marker definitions).

Remove the `ControllingSortedTowers` property (lines 189-192).

Remove the `ArrowColor` method (lines 203-209).

Add new control layout block in the SVG (between the `</defs>` closing tag and the `@if (IsChainBreakActive)` block):

```razor
@* Control-layout overlay — only when Conquest has an active TowerControlLayout. *@
@if (IsControlLayoutActive)
{
    var conquest = (GameModeConquest)_GameStateService.CurrentGame;
    var links = conquest.ActiveControlLayout!.Links;

    @foreach (var link in links)
    {
        var srcTower = _GameStateService.TowerManagerService.Towers.Values
            .FirstOrDefault(t => t.MacAddress == link.ControllerTowerMacAddress && t.Location != null);
        var dstTower = _GameStateService.TowerManagerService.Towers.Values
            .FirstOrDefault(t => t.MacAddress == link.ControlledTowerMacAddress && t.Location != null);

        if (srcTower != null && dstTower != null)
        {
            var (ax1, ay1, ax2, ay2) = ArrowLine(srcTower, dstTower);
            var linkColor = ControlLinkColorForTower(srcTower);
            <line x1="@ax1" y1="@ay1" x2="@ax2" y2="@ay2"
                  stroke="@linkColor" stroke-width="2"
                  class="arrow-flow"
                  filter="url(#arrowGlow)" />
        }
    }
}
```

Add helper properties in the `@code` block:

```csharp
private bool IsControlLayoutActive =>
    _GameStateService.CurrentGame is GameModeConquest gc
    && gc.ActiveControlLayout != null;

private static string ControlLinkColorForTower(Tower src) => src.CurrentColor switch
{
    TeamColor.RED  => "#fc1911",
    TeamColor.BLUE => "#00b4f1",
    _              => "#000000"
};
```

- [ ] **Step 2: Commit**

```bash
git add OWLServer/OWLServer/Components/MapComponents/Map.razor
git commit -m "feat: adapt Map.razor to render control-layout arrows from persisted model"
```

---

### Task 9: Remove control dropdown from TowerConfig

**Files:**
- Modify: `OWLServer/OWLServer/Components/ConfigComponents/TowerConfig.razor`
- Modify: `OWLServer/OWLServer/Components/ConfigComponents/TowerConfig.razor.cs`

- [ ] **Step 1: Remove the "Wird kontrolliert von" dropdown**

Remove lines 78-87 from `TowerConfig.razor`:
```razor
<RadzenFormField Text="Wird kontrolliert von" Style="flex:2;min-width:140px">
    <RadzenDropDown TValue="string?"
                    Value="@tower.IsControlledByID"
                    Data="GameStateService.TowerManagerService.Towers.Values
                              .Where(t => t.MacAddress != tower.MacAddress)
                              .Select(t => t.MacAddress)"
                    AllowClear="true"
                    Placeholder="(keine)"
                    Change="@(args => ControllingTowerChanged(tower, $"{args}"))"/>
</RadzenFormField>
```

- [ ] **Step 2: Remove ControllingTowerChanged method**

Remove the entire `ControllingTowerChanged` method from `TowerConfig.razor.cs`:
```csharp
private void ControllingTowerChanged(Tower tower, string newControllingTowerID)
{
    if (tower.IsControlled)
    {
        var previousController = tower.IsControlledByID;
        tower.IsControlled = false;
        tower.IsControlledByID = null;
        if (previousController != null && GameStateService.TowerManagerService.Towers.ContainsKey(previousController))
            GameStateService.TowerManagerService.Towers[previousController].ControllsTowerID.Remove(tower.MacAddress);
    }

    if (!string.IsNullOrEmpty(newControllingTowerID))
    {
        tower.IsControlled = true;
        tower.IsControlledByID = newControllingTowerID;
        if (GameStateService.TowerManagerService.Towers.ContainsKey(newControllingTowerID))
            GameStateService.TowerManagerService.Towers[newControllingTowerID].ControllsTowerID.Add(tower.MacAddress);
    }

    tower.SetToStartColor();
    ExternalTriggerService.StateHasChangedAction?.Invoke();
}
```

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/TowerConfig.razor OWLServer/OWLServer/Components/ConfigComponents/TowerConfig.razor.cs
git commit -m "refactor: remove per-tower control dropdown, now managed via TowerControlConfig"
```

---

### Task 10: Build and verify

- [ ] **Step 1: Build the project**

Run: `dotnet build`
Expected: Build succeeded with no errors or warnings.

- [ ] **Step 2: Verify all modified files compile**

Check that:
- `TowerControlLayout`/`TowerControlLink` resolve in `DatabaseContext`
- `GameModeConquest.ActiveControlLayout` references the correct type
- `Map.razor` casts to `GameModeConquest` correctly
- No dangling references to `IsControlled`, `IsControlledByID`, `ControllsTowerID`, `IsForControlling`
- No dangling references to `ProcessTowerStateMachine`, `ControllingSortedTowers`, `ArrowColor`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "fix: resolve build errors"
```
