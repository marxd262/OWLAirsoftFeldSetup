# Conquest Reset Timer Visualization

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a depleting progress ring and seconds countdown on captured controller towers in Conquest mode to visualize how long until each auto-resets to neutral.

**Architecture:** Add a computed `ResetSecondsRemaining` property to Tower, an `IsControllerTower()` method to GameModeConquest, a `ShowResetTimer` parameter to TowerComponent that conditionally renders the countdown, and wire it up per-tower in MapCanvas.

**Tech Stack:** C# / .NET 8 Blazor, Radzen.Blazor

---

### Task 1: Tower — Add ResetSecondsRemaining computed property

**Files:**
- Modify: `OWLServer/OWLServer/Models/Tower.cs`

- [ ] **Step 1: Add computed property to Tower**

After `public DateTime? CapturedAt { get; set; }` (around line 38), insert:

```csharp
[NotMapped]
public int ResetSecondsRemaining =>
    CapturedAt.HasValue
        ? Math.Max(0, ResetsAfterInSeconds - (int)(DateTime.Now - CapturedAt.Value).TotalSeconds)
        : -1;
```

Required `using` (already present): `System.ComponentModel.DataAnnotations.Schema` is already imported.

- [ ] **Step 2: Build and verify**

Run: `dotnet build`

Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Models/Tower.cs
git commit -m "feat: add ResetSecondsRemaining computed property to Tower"
```

---

### Task 2: GameModeConquest — Add IsControllerTower() method

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs`

- [ ] **Step 1: Add public method to check if a tower is a controller**

After `private Dictionary<string, string> _controllerByChild = new();` (around line 37), insert:

```csharp
public bool IsControllerTower(string macAddress) => _controlledChildren.ContainsKey(macAddress);
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs
git commit -m "feat: add IsControllerTower() method to GameModeConquest"
```

---

### Task 3: TowerComponent — Add ShowResetTimer parameter + conditional display

**Files:**
- Modify: `OWLServer/OWLServer/Components/Components/TowerComponent.razor`

- [ ] **Step 1: Add parameter in code block**

In the `@code` block, after `[Parameter] public EventCallback OnTowerClicked { get; set; }` (line 48), insert:

```csharp
[Parameter] public bool ShowResetTimer { get; set; }
```

- [ ] **Step 2: Add reset display logic in template area**

In the `else` branch (non-editable path, line 27), replace the existing `<Template>` content with a conditional that shows reset info when applicable.

Replace lines 33-39:

```razor
        <Template>
            <RadzenText TextAlign="TextAlign.Center"
                TextStyle="@(Smaller ? TextStyle.DisplayH6 : TextStyle.DisplayH4)"
                Style="@($"color: {ColorTranslator.ToHtml(FontColor)}; margin-top: 7px;")">
                @Tower.DisplayLetter
            </RadzenText>
        </Template>
```

With:

```razor
        <Template>
            @{
                var resetsRemaining = Tower.ResetSecondsRemaining;
                var showTimer = ShowResetTimer && resetsRemaining >= 0;
            }
            @if (showTimer)
            {
                <div style="display:flex;flex-direction:column;align-items:center;margin-top:2px;">
                    <span style="color:@ColorTranslator.ToHtml(FontColor);font-size:14px;line-height:1;">
                        @Tower.DisplayLetter
                    </span>
                    <span style="color:@ColorTranslator.ToHtml(FontColor);font-size:9px;line-height:1;">
                        @(resetsRemaining)s
                    </span>
                </div>
            }
            else
            {
                <RadzenText TextAlign="TextAlign.Center"
                    TextStyle="@(Smaller ? TextStyle.DisplayH6 : TextStyle.DisplayH4)"
                    Style="@($"color: {ColorTranslator.ToHtml(FontColor)}; margin-top: 7px;")">
                    @Tower.DisplayLetter
                </RadzenText>
            }
        </Template>
```

- [ ] **Step 3: Override progress ring Value when showing reset timer**

In the non-editable `<RadzenProgressBarCircular>`, change `Value="@(Tower.GetDisplayProgress())"` (line 31) to compute the reset timer percentage when applicable.

Replace:

```razor
        Value="@(Tower.GetDisplayProgress())"
```

With:

```razor
        Value="@(ShowResetTimer && Tower.ResetSecondsRemaining >= 0
            ? (int)((double)Tower.ResetSecondsRemaining / Tower.ResetsAfterInSeconds * 100)
            : Tower.GetDisplayProgress())"
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build`

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add OWLServer/OWLServer/Components/Components/TowerComponent.razor
git commit -m "feat: add ShowResetTimer param and countdown display to TowerComponent"
```

---

### Task 4: MapCanvas — Wire ShowResetTimer per-tower from Conquest mode

**Files:**
- Modify: `OWLServer/OWLServer/Components/MapComponents/MapCanvas.razor`

- [ ] **Step 1: Add necessary usings and game mode check**

MapCanvas already injects `_GameStateService`. In the `@foreach` loop for towers (line 22), replace:

```razor
     @foreach (var tower in _GameStateService.TowerManagerService.Towers.Values.Where(t => t.Location != null))
     {
         <TowerComponent Tower="tower" />
     }
```

With:

```razor
     @{
         var conquest = _GameStateService.CurrentGame as GameModeConquest;
     }
     @foreach (var tower in _GameStateService.TowerManagerService.Towers.Values.Where(t => t.Location != null))
     {
         <TowerComponent Tower="tower"
             ShowResetTimer="@(conquest?.IsControllerTower(tower.MacAddress) ?? false)" />
     }
```

Add the using directive at the top of the file, after existing `@namespace`:

```razor
@using OWLServer.Models.GameModes
```

(Check if already imported — it's `MapCanvas.razor` not `Map.razor`, so it may not be. Add if missing.)

- [ ] **Step 2: Build and verify**

Run: `dotnet build`

Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Final commit**

```bash
git add OWLServer/OWLServer/Components/MapComponents/MapCanvas.razor
git commit -m "feat: wire conquest reset timer to tower map display"
```
