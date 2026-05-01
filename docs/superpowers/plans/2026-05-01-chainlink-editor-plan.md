# ChainLink Map Editor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the list-based ChainLink editor with a full-page map-first editor supporting branching, touch-friendly link creation (tap-tap + long-press-drag), and a slide-in side panel for layout management.

**Architecture:** Extract `MapCanvas` base from `Map.razor` (reusable for other contexts). `ChainEditor` page owns all state — renders map image + SVG arrows inline + towers with click handlers. `ChainEditorPanel` slides in for layout CRUD. `ChainGraphPreview` renders a live topology diagram. `GameModeChainBreakConfig` stripped to base settings + layout dropdown. No new NuGet packages.

**Tech Stack:** .NET 8 Blazor Interactive Server, Radzen.Blazor, SVG, CSS transforms

**Deferred to future PRs:**
- Long-press-drag link creation (tap-tap works on touch; long-press requires JS interop for reliable `pointerdown`/`pointerup` timing)
- Stale tower reference detection on layout load
- Unsaved-changes browser confirmation dialog on navigate-away
- Delete-active-layout confirmation dialog
- Pinch-to-zoom / map pan

---

### Task 1: Extract `MapCanvas` base component

**Files:**
- Create: `OWLServer/OWLServer/Components/MapComponents/MapCanvas.razor`
- Create: `OWLServer/OWLServer/Components/MapComponents/MapCanvas.razor.cs`
- Modify: `OWLServer/OWLServer/Components/MapComponents/Map.razor`

- [ ] **Step 1: Create `MapCanvas.razor`**

```razor
@inject IGameStateService _GameStateService
@inject IMapService _MapService
@using OWLServer.Models
@namespace OWLServer.Components.MapComponents

<style>
    #map {
        width: 100%;
        height: calc(100% - 90px);
        object-fit: fill;
    }
</style>

<div style="position: relative">
    <img id="map" src="@_MapService.GetCurrentMapUrl()" />

    <svg viewBox="0 0 100 100" preserveAspectRatio="none"
         style="position:absolute;top:0;left:0;width:100%;height:100%;pointer-events:none;overflow:visible">
        @ChildContent
    </svg>

    @foreach (var tower in _GameStateService.TowerManagerService.Towers.Values.Where(t => t.Location != null))
    {
        <TowerComponent Tower="tower" />
    }
</div>
```

- [ ] **Step 2: Create `MapCanvas.razor.cs`**

```csharp
using Microsoft.AspNetCore.Components;

namespace OWLServer.Components.MapComponents;

public partial class MapCanvas : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 3: Refactor `Map.razor` to wrap content in `<MapCanvas>`**

Keep the existing `@code` block, chain animation CSS, JS interop, and arrow helper methods exactly as-is. Change the template to:

```razor
@inject IJSRuntime _jsRuntime;
@inject IExternalTriggerService ExternalTriggerService
@inject IGameStateService _GameStateService
@using OWLServer.Models.GameModes
@using OWLServer.Models
@implements IDisposable

<style>
    @keyframes flowDash {
        to { stroke-dashoffset: -8; }
    }
    @keyframes flowDashReverse {
        to { stroke-dashoffset: 8; }
    }
    .chain-flow {
        stroke-dasharray: 5 3;
        animation: flowDash 1.2s linear infinite;
    }
    .chain-flow-reverse {
        stroke-dasharray: 5 3;
        animation: flowDashReverse 1.2s linear infinite;
    }
    .arrow-flow {
        stroke-dasharray: 5 3;
        animation: flowDash 1.6s linear infinite;
    }
</style>

<MapCanvas>
    <defs style="overflow:visible">
        <filter id="arrowGlow" filterUnits="userSpaceOnUse" x="-10" y="-10" width="120" height="120">
            <feGaussianBlur in="SourceGraphic" stdDeviation="1.5" result="blur"/>
            <feMerge>
                <feMergeNode in="blur"/>
                <feMergeNode in="SourceGraphic"/>
            </feMerge>
        </filter>
        @foreach (var src in ControllerTowers)
        {
            <marker id="arrow-@src.MacAddress"
                    markerWidth="6" markerHeight="5" refX="0" refY="2.5"
                    orient="auto" markerUnits="userSpaceOnUse">
                <polygon points="0 0, 6 2.5, 0 5" fill="@ArrowColor(src)" />
            </marker>
        }
    </defs>

    @foreach (var link in ActiveControlLinks)
    {
        var src = _GameStateService.TowerManagerService.Towers.Values
            .FirstOrDefault(t => t.MacAddress == link.ControllerTowerMacAddress && t.Location != null);
        var dst = _GameStateService.TowerManagerService.Towers.Values
            .FirstOrDefault(t => t.MacAddress == link.ControlledTowerMacAddress && t.Location != null);

        if (src != null && dst != null)
        {
            var (ax1, ay1, ax2, ay2) = ArrowLine(src, dst);
            <line x1="@ax1" y1="@ay1" x2="@ax2" y2="@ay2"
                  stroke="@ArrowColor(src)" stroke-width="2"
                  class="arrow-flow" filter="url(#arrowGlow)"
                  marker-end="url(#arrow-@src.MacAddress)" />
        }
    }

    @if (IsChainBreakActive)
    {
        var gameMode = _GameStateService.CurrentGame as GameModeChainBreak;
        <defs>
            <marker id="chain-arrow-red" markerWidth="7" markerHeight="6" refX="0" refY="3"
                    orient="auto" markerUnits="userSpaceOnUse">
                <polygon points="0 0, 7 3, 0 6" fill="#fc1911"/>
            </marker>
            <marker id="chain-arrow-blue" markerWidth="7" markerHeight="6" refX="0" refY="3"
                    orient="auto" markerUnits="userSpaceOnUse">
                <polygon points="0 0, 7 3, 0 6" fill="#00b4f1"/>
            </marker>
            <marker id="chain-arrow-white" markerWidth="7" markerHeight="6" refX="0" refY="3"
                    orient="auto" markerUnits="userSpaceOnUse">
                <polygon points="0 0, 7 3, 0 6" fill="#FFFFFF"/>
            </marker>
        </defs>

        @foreach (var link in ActiveChainLinks)
        {
            var towerA = _GameStateService.TowerManagerService.Towers.Values
                .FirstOrDefault(t => t.MacAddress == link.TowerAMacAddress && t.Location != null);
            var towerB = _GameStateService.TowerManagerService.Towers.Values
                .FirstOrDefault(t => t.MacAddress == link.TowerBMacAddress && t.Location != null);

            @if (towerA != null && towerB != null && gameMode != null)
            {
                var (color, arrowA, arrowB, animated, bothWays) = gameMode.GetLinkVisualState(link);

                @if (bothWays)
                {
                    var (ax1, ay1, mx, my) = HalfLine(towerA, towerB);
                    var (bx1, by1, _, _) = HalfLine(towerB, towerA);
                    var colorA = TowerTeamColorHex(towerA);
                    var colorB = TowerTeamColorHex(towerB);
                    <line x1="@ax1" y1="@ay1" x2="@mx" y2="@my"
                          stroke="@colorA" stroke-width="2.5" stroke-dasharray="5 3"
                          class="chain-flow" filter="url(#arrowGlow)"/>
                    <line x1="@bx1" y1="@by1" x2="@mx" y2="@my"
                          stroke="@colorB" stroke-width="2.5" stroke-dasharray="5 3"
                          class="chain-flow" filter="url(#arrowGlow)"/>
                }
                else if (arrowB)
                {
                    var (x1, y1, x2, y2) = ArrowLine(towerA, towerB, srcGap: 4.0, destGap: 11.0);
                    var animClass = animated ? "chain-flow" : null;
                    <line x1="@x1" y1="@y1" x2="@x2" y2="@y2"
                          stroke="@color" stroke-width="2.5" stroke-dasharray="5 3"
                          class="@animClass" filter="url(#arrowGlow)"
                          marker-end="@ArrowRef(color)"/>
                }
                else if (arrowA)
                {
                    var (x1, y1, x2, y2) = ArrowLine(towerB, towerA, srcGap: 4.0, destGap: 11.0);
                    var animClass = animated ? "chain-flow" : null;
                    <line x1="@x1" y1="@y1" x2="@x2" y2="@y2"
                          stroke="@color" stroke-width="2.5" stroke-dasharray="5 3"
                          class="@animClass" filter="url(#arrowGlow)"
                          marker-end="@ArrowRef(color)"/>
                }
                else
                {
                    var (x1, y1, x2, y2) = ArrowLine(towerA, towerB, srcGap: 4.0, destGap: 4.0);
                    <line x1="@x1" y1="@y1" x2="@x2" y2="@y2"
                          stroke="@color" stroke-width="2.5" filter="url(#arrowGlow)"/>
                }
            }
        }
    }
</MapCanvas>

<script>
    function GetElementLocation(elementId) {
        var element = $(elementId);
        var position = element.position();
        var offset   = element.offset();
        return {
            Width:      element.outerWidth(),
            Height:     element.outerHeight(),
            offsetTop:  offset.top,
            offsetLeft: offset.left,
            Top:        position.top,
            Left:       position.left,
        };
    }
</script>
```

The existing `@code` block in `Map.razor` (lines 187-352) remains unchanged. Remove the old `<div style="position:relative">` + `<img>` + tower `@foreach` + old SVG since MapCanvas now handles those.

- [ ] **Step 4: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds, zero errors.

- [ ] **Step 5: Commit**

```bash
git add OWLServer/OWLServer/Components/MapComponents/MapCanvas.razor OWLServer/OWLServer/Components/MapComponents/MapCanvas.razor.cs OWLServer/OWLServer/Components/MapComponents/Map.razor
git commit -m "refactor: extract MapCanvas base component from Map.razor"
```

---

### Task 2: Add `Editable` and `OnTowerClicked` parameters to `TowerComponent`

**Files:**
- Modify: `OWLServer/OWLServer/Components/Components/TowerComponent.razor`

- [ ] **Step 1: Update `TowerComponent.razor`**

```razor
<style>
    .@TowerClass {
        .rz-progressbar-circular-value {
            stroke: @Tower.DisplaycolorAsHTML() !important;
        }
        @(Smaller && !Editable ? "" : ".rz-progressbar-circular-background{fill: rgba(255,255,255,0.5);}")
    }
    .absoluteTower { position: absolute }
</style>

<RadzenProgressBarCircular class="@TowerClass"
    style="@GetTowerStyle()"
    Value="@(Tower.GetDisplayProgress())"
    Size="@(Editable ? ProgressBarCircularSize.Medium :
           Smaller ? ProgressBarCircularSize.Small : ProgressBarCircularSize.Medium)"
    Attributes="@GetAttributes()">
    <Template>
        <RadzenText
            TextAlign="TextAlign.Center"
            TextStyle="@(Smaller && !Editable ? TextStyle.DisplayH6 : TextStyle.DisplayH4)"
            Style="@($"color: {ColorTranslator.ToHtml(FontColor)}; margin-top: 7px;")">
            @Tower.DisplayLetter
        </RadzenText>
    </Template>
</RadzenProgressBarCircular>

@code {
    [Parameter] public required Tower Tower { get; set; }
    [Parameter] public bool Smaller { get; set; }
    [Parameter] public bool Editable { get; set; }
    [Parameter] public Color FontColor { get; set; } = Color.Black;
    [Parameter] public EventCallback OnTowerClicked { get; set; }

    private string TowerClass => $"tower_{Tower.MacAddress.Replace(":", "-")}";

    private string GetTowerStyle()
    {
        if (Editable)
            return (Tower.Location?.ToLocationString() ?? "") + ";transform:scale(1.6);cursor:pointer;";
        if (Smaller)
            return "";
        return Tower.Location?.ToLocationString() ?? "";
    }

    private Dictionary<string, object>? GetAttributes()
    {
        if (!Editable || !OnTowerClicked.HasDelegate) return null;
        return new Dictionary<string, object>
        {
            ["onclick"] = EventCallback.Factory.Create(this, () => OnTowerClicked.InvokeAsync())
        };
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Components/TowerComponent.razor
git commit -m "feat: add Editable and OnTowerClicked params to TowerComponent"
```

---

### Task 3: Create `ChainGraphPreview` component

**Files:**
- Create: `OWLServer/OWLServer/Components/ConfigComponents/ChainGraphPreview.razor`
- Create: `OWLServer/OWLServer/Components/ConfigComponents/ChainGraphPreview.razor.cs`

- [ ] **Step 1: Create `ChainGraphPreview.razor`**

```razor
@using OWLServer.Models
@namespace OWLServer.Components.ConfigComponents

<svg viewBox="0 0 @(_width) @(_height)"
     style="width:100%;max-height:250px;overflow:visible;background:#0a0a1a;border-radius:6px;">
    @foreach (var (mac, x, y) in _nodePositions)
    {
        <circle cx="@x" cy="@y" r="14" fill="@NodeFill(mac)" stroke="#555" stroke-width="2" />
        <text x="@x" y="@(y+5)" text-anchor="middle" fill="white" font-size="11" font-weight="bold">
            @ShortLabel(mac)
        </text>
    }
    @foreach (var (aMac, bMac, bothWays) in _renderLinks)
    {
        if (!_nodePositions.Any(np => np.mac == aMac) || !_nodePositions.Any(np => np.mac == bMac))
            continue;
        var (ax, ay) = _nodePositions.First(np => np.mac == aMac);
        var (bx, by) = _nodePositions.First(np => np.mac == bMac);
        var midX = (ax + bx) / 2;
        var midY = (ay + by) / 2;
        var dx = bx - ax;
        var dy = by - ay;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var nx = dx / len;
        var ny = dy / len;
        var startX = ax + nx * 14;
        var startY = ay + ny * 14;
        var endX = bx - nx * 14;
        var endY = by - ny * 14;

        @if (bothWays)
        {
            <line x1="@Fmt(startX)" y1="@Fmt(startY)" x2="@Fmt(endX)" y2="@Fmt(endY)"
                  stroke="#00b4f1" stroke-width="1.5" />
        }
        else
        {
            <line x1="@Fmt(startX)" y1="@Fmt(startY)" x2="@Fmt(endX)" y2="@Fmt(endY)"
                  stroke="#00b4f1" stroke-width="1.5"
                  marker-end="url(#preview-arrow)" />
        }
        <text x="@Fmt(midX)" y="@Fmt(midY - 6)" text-anchor="middle" fill="#888" font-size="10">
            @(bothWays ? "↔" : "→")
        </text>
    }
    <defs>
        <marker id="preview-arrow" markerWidth="6" markerHeight="5" refX="6" refY="2.5"
                orient="auto" markerUnits="userSpaceOnUse">
            <polygon points="0 0, 6 2.5, 0 5" fill="#00b4f1"/>
        </marker>
    </defs>
</svg>
```

- [ ] **Step 2: Create `ChainGraphPreview.razor.cs`**

```csharp
using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.ConfigComponents;

public partial class ChainGraphPreview : ComponentBase
{
    [Parameter] public List<ChainLink> Links { get; set; } = new();

    [Inject] public IGameStateService GameStateService { get; set; } = null!;

    private List<(string mac, double x, double y)> _nodePositions = new();
    private List<(string aMac, string bMac, bool bothWays)> _renderLinks = new();
    private double _width = 300;
    private double _height = 200;
    private const double Padding = 36;
    private const double LevelSpacing = 55;
    private const double NodeSpacing = 52;

    protected override void OnParametersSet() => LayoutGraph();

    private void LayoutGraph()
    {
        _nodePositions.Clear();
        _renderLinks.Clear();

        var links = Links;
        if (!links.Any()) { _width = 300; _height = 100; return; }

        var towers = GameStateService.TowerManagerService.Towers;

        // Build adjacency and in-degree
        var adj = new Dictionary<string, List<string>>();
        var inDeg = new Dictionary<string, int>();
        var allMacs = new HashSet<string>();

        foreach (var l in links)
        {
            allMacs.Add(l.TowerAMacAddress);
            allMacs.Add(l.TowerBMacAddress);
        }

        foreach (var mac in allMacs)
        {
            if (!towers.ContainsKey(mac)) continue;
            adj[mac] = new List<string>();
            inDeg[mac] = 0;
        }

        foreach (var l in links)
        {
            if (!adj.ContainsKey(l.TowerAMacAddress) || !adj.ContainsKey(l.TowerBMacAddress)) continue;
            adj[l.TowerAMacAddress].Add(l.TowerBMacAddress);
            if (!inDeg.ContainsKey(l.TowerBMacAddress)) inDeg[l.TowerBMacAddress] = 0;
            inDeg[l.TowerBMacAddress]++;

            if (l.EntryAtBothEnds)
            {
                adj[l.TowerBMacAddress].Add(l.TowerAMacAddress);
                if (!inDeg.ContainsKey(l.TowerAMacAddress)) inDeg[l.TowerAMacAddress] = 0;
                inDeg[l.TowerAMacAddress]++;
            }

            _renderLinks.Add((l.TowerAMacAddress, l.TowerBMacAddress, l.EntryAtBothEnds));
        }

        // BFS from entry points
        var levels = new Dictionary<string, int>();
        var queue = new Queue<string>();
        foreach (var (mac, deg) in inDeg)
        {
            if (deg == 0)
            {
                levels[mac] = 0;
                queue.Enqueue(mac);
            }
        }
        if (!queue.Any() && inDeg.Any())
        {
            var first = inDeg.Keys.First();
            levels[first] = 0;
            queue.Enqueue(first);
        }

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!adj.ContainsKey(cur)) continue;
            foreach (var nxt in adj[cur])
            {
                if (!levels.ContainsKey(nxt))
                {
                    levels[nxt] = levels[cur] + 1;
                    queue.Enqueue(nxt);
                }
                else
                {
                    levels[nxt] = Math.Max(levels[nxt], levels[cur] + 1);
                }
            }
        }

        // Group by level
        var groups = levels.GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key).ToList();

        var maxLevel = groups.Any() ? groups.Max(g => g.Key) : 0;
        var maxPerLevel = groups.Any() ? groups.Max(g => g.Count()) : 1;

        _width = Math.Max(300, Padding * 2 + maxPerLevel * NodeSpacing);
        _height = Padding * 2 + maxLevel * LevelSpacing;

        foreach (var g in groups)
        {
            var nodes = g.ToList();
            var totalWidth = nodes.Count * NodeSpacing;
            var startX = (_width - totalWidth) / 2 + NodeSpacing / 2;
            for (int i = 0; i < nodes.Count; i++)
                _nodePositions.Add((nodes[i].Key, startX + i * NodeSpacing,
                    Padding + g.Key * LevelSpacing));
        }
    }

    private static string NodeFill(string mac) => "#16213e";

    private string ShortLabel(string mac)
    {
        if (GameStateService.TowerManagerService.Towers.TryGetValue(mac, out var t))
            return t.DisplayLetter;
        return mac.Length > 4 ? mac[..4] : mac;
    }

    private static string Fmt(double v) =>
        v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/ChainGraphPreview.razor OWLServer/OWLServer/Components/ConfigComponents/ChainGraphPreview.razor.cs
git commit -m "feat: add ChainGraphPreview SVG graph diagram component"
```

---

### Task 4: Create `ChainEditorToolbar` component

**Files:**
- Create: `OWLServer/OWLServer/Components/ConfigComponents/ChainEditorToolbar.razor`

- [ ] **Step 1: Create `ChainEditorToolbar.razor`**

```razor
@namespace OWLServer.Components.ConfigComponents

<style>
    .editor-toolbar {
        position: fixed;
        bottom: 0;
        left: 0;
        right: 0;
        height: 48px;
        background: var(--rz-base-background-color, #1a1a2e);
        border-top: 1px solid var(--rz-base-border-color, #333);
        display: flex;
        align-items: center;
        padding: 0 12px;
        gap: 8px;
        z-index: 100;
    }
    .mode-group { display: flex; gap: 4px; }
    .spacer { flex: 1; }
    .mode-btn {
        padding: 6px 14px;
        border-radius: 6px;
        border: 1px solid transparent;
        background: transparent;
        color: #aaa;
        cursor: pointer;
        font-size: 13px;
        font-weight: 500;
        transition: all 0.15s;
    }
    .mode-btn.active { background: var(--rz-primary); color: white; border-color: var(--rz-primary); }
    .mode-btn:hover:not(.active) { color: #ccc; border-color: #555; }
    .icon-btn {
        padding: 6px 10px;
        border-radius: 6px;
        border: 1px solid #555;
        background: transparent;
        color: #ccc;
        cursor: pointer;
        font-size: 16px;
        line-height: 1;
    }
    .icon-btn:hover { background: #333; }
    .icon-btn:disabled { opacity: 0.4; cursor: default; }
    .panel-toggle {
        padding: 6px 14px;
        border-radius: 6px;
        border: 1px solid #555;
        background: transparent;
        color: #ccc;
        cursor: pointer;
        font-size: 13px;
    }
    .panel-toggle:hover { background: #333; }
</style>

<div class="editor-toolbar">
    <div class="mode-group">
        <button class="mode-btn @(Mode == EditorMode.Draw ? "active" : "")"
                @onclick="() => SetMode(EditorMode.Draw)">+ Draw</button>
        <button class="mode-btn @(Mode == EditorMode.Erase ? "active" : "")"
                @onclick="() => SetMode(EditorMode.Erase)">✕ Erase</button>
        <button class="mode-btn @(Mode == EditorMode.Direction ? "active" : "")"
                @onclick="() => SetMode(EditorMode.Direction)">↔ Direction</button>
    </div>
    <div class="spacer"></div>
    <button class="icon-btn" disabled="@(!CanUndo)" @onclick="OnUndo" title="Undo">↶</button>
    <button class="icon-btn" disabled="@(!CanRedo)" @onclick="OnRedo" title="Redo">↷</button>
    <button class="panel-toggle" @onclick="OnTogglePanel">
        @(PanelVisible ? "✕ Panel" : "☰ Panel")
    </button>
</div>

@code {
    [Parameter] public EditorMode Mode { get; set; } = EditorMode.Draw;
    [Parameter] public EventCallback<EditorMode> ModeChanged { get; set; }
    [Parameter] public bool CanUndo { get; set; }
    [Parameter] public bool CanRedo { get; set; }
    [Parameter] public EventCallback OnUndo { get; set; }
    [Parameter] public EventCallback OnRedo { get; set; }
    [Parameter] public bool PanelVisible { get; set; }
    [Parameter] public EventCallback<bool> PanelVisibleChanged { get; set; }

    private async Task SetMode(EditorMode m) { Mode = m; await ModeChanged.InvokeAsync(m); }
    private async Task OnTogglePanel() { PanelVisible = !PanelVisible; await PanelVisibleChanged.InvokeAsync(PanelVisible); }

    public enum EditorMode { Draw, Erase, Direction }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/ChainEditorToolbar.razor
git commit -m "feat: add ChainEditorToolbar component"
```

---

### Task 5: Create `ChainEditorPanel` component

**Files:**
- Create: `OWLServer/OWLServer/Components/ConfigComponents/ChainEditorPanel.razor`
- Create: `OWLServer/OWLServer/Components/ConfigComponents/ChainEditorPanel.razor.cs`

- [ ] **Step 1: Create `ChainEditorPanel.razor`**

```razor
@using OWLServer.Models
@using OWLServer.Context
@using Microsoft.EntityFrameworkCore
@namespace OWLServer.Components.ConfigComponents

<style>
    .side-panel {
        position: fixed;
        top: 0;
        right: 0;
        width: 300px;
        height: calc(100vh - 48px);
        background: var(--rz-base-background-color, #1a1a2e);
        border-left: 1px solid #333;
        z-index: 200;
        overflow-y: auto;
        padding: 12px;
        transition: transform 0.25s ease;
    }
    .side-panel.hidden { transform: translateX(100%); }
    .side-panel.visible { transform: translateX(0); }
    .panel-backdrop {
        position: fixed;
        top: 0; left: 0; right: 0; bottom: 0;
        background: rgba(0,0,0,0.5);
        z-index: 199;
    }
    .panel-section { margin-bottom: 16px; }
    .panel-section h4 {
        color: #aaa;
        font-size: 11px;
        text-transform: uppercase;
        letter-spacing: .5px;
        margin: 0 0 8px 0;
        padding-bottom: 4px;
        border-bottom: 1px solid #333;
    }
    .link-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 4px 6px;
        background: #16213e;
        border-radius: 4px;
        margin-bottom: 4px;
        font-size: 12px;
        color: #ccc;
    }
    .link-row button {
        background: transparent;
        border: none;
        color: #e74c3c;
        cursor: pointer;
        font-size: 14px;
        padding: 2px 6px;
    }
</style>

@if (Visible)
{
    <div class="panel-backdrop" @onclick="ClosePanel" @onclick:stopPropagation="true"></div>
}

<div class="side-panel @(Visible ? "visible" : "hidden")" @onclick:stopPropagation="true">

    <div class="panel-section">
        <h4>Layout Management</h4>
        <RadzenStack Gap=".5rem">
            <RadzenDropDown TValue="int?" Data="@_layoutItems"
                            TextProperty="Name" ValueProperty="Id"
                            @bind-Value="_selectedLayoutId"
                            Placeholder="Layout laden..."
                            Style="width:100%" />
            <RadzenTextBox @bind-Value="_layoutName" Placeholder="Layout-Name" Style="width:100%" />
            <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem">
                <RadzenButton Text="Neu speichern" Size="ButtonSize.Small"
                              ButtonStyle="ButtonStyle.Primary"
                              Disabled="@(!Links.Any() || string.IsNullOrWhiteSpace(_layoutName))"
                              Click="@SaveAsNew" />
                @if (_selectedLayoutId != null)
                {
                    <RadzenButton Text="Aktualisieren" Size="ButtonSize.Small"
                                  ButtonStyle="ButtonStyle.Warning"
                                  Click="@UpdateExisting" />
                }
            </RadzenStack>
            @if (_selectedLayoutId != null)
            {
                <RadzenButton Text="Löschen" Size="ButtonSize.Small"
                              ButtonStyle="ButtonStyle.Danger"
                              Click="@DeleteLayout" />
            }
        </RadzenStack>
    </div>

    <div class="panel-section">
        <h4>Links (@Links.Count)</h4>
        @if (!Links.Any())
        {
            <p style="color:#666;font-size:12px;">Keine Links vorhanden</p>
        }
        else
        {
            @foreach (var link in Links.ToList())
            {
                <div class="link-row">
                    <span>@LinkLabel(link)</span>
                    <button @onclick="() => RemoveLink(link)">✕</button>
                </div>
            }
        }
    </div>

    <div class="panel-section">
        <h4>Chain Preview</h4>
        <ChainGraphPreview Links="Links" />
    </div>
</div>
```

- [ ] **Step 2: Create `ChainEditorPanel.razor.cs`**

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.ConfigComponents;

public partial class ChainEditorPanel : ComponentBase
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public List<ChainLink> Links { get; set; } = new();
    [Parameter] public EventCallback<List<ChainLink>> LinksChanged { get; set; }
    [Parameter] public EventCallback OnLayoutChanged { get; set; }

    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;
    [Inject] public IGameStateService GameStateService { get; set; } = null!;

    private List<ChainLayout> _layoutItems = new();
    private int? _selectedLayoutId;
    private int? _lastSelectedLayoutId;
    private string _layoutName = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadLayouts();

    public async Task LoadLayouts()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _layoutItems = await db.ChainLayouts.Include(cl => cl.Links).ToListAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_selectedLayoutId != _lastSelectedLayoutId && _selectedLayoutId != null)
        {
            _lastSelectedLayoutId = _selectedLayoutId;
            await using var db = await DbFactory.CreateDbContextAsync();
            var layout = await db.ChainLayouts.Include(cl => cl.Links)
                .FirstOrDefaultAsync(cl => cl.Id == _selectedLayoutId);
            if (layout != null)
            {
                _layoutName = layout.Name;
                Links.Clear();
                foreach (var l in layout.Links)
                    Links.Add(new ChainLink {
                        TowerAMacAddress = l.TowerAMacAddress,
                        TowerBMacAddress = l.TowerBMacAddress,
                        EntryAtBothEnds = l.EntryAtBothEnds
                    });
                await LinksChanged.InvokeAsync(Links);
                StateHasChanged();
            }
        }
    }

    private string LinkLabel(ChainLink l)
    {
        var arrow = l.EntryAtBothEnds ? "↔" : "→";
        return $"{ShortLabel(l.TowerAMacAddress)} {arrow} {ShortLabel(l.TowerBMacAddress)}";
    }

    private string ShortLabel(string mac)
    {
        if (GameStateService.TowerManagerService.Towers.TryGetValue(mac, out var t))
            return t.DisplayLetter;
        return mac;
    }

    private async Task RemoveLink(ChainLink link)
    {
        Links.Remove(link);
        await LinksChanged.InvokeAsync(Links);
    }

    private async Task SaveAsNew()
    {
        if (string.IsNullOrWhiteSpace(_layoutName) || !Links.Any()) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = new ChainLayout
        {
            Name = _layoutName,
            Links = Links.Select(l => new ChainLink
            {
                TowerAMacAddress = l.TowerAMacAddress,
                TowerBMacAddress = l.TowerBMacAddress,
                EntryAtBothEnds = l.EntryAtBothEnds
            }).ToList()
        };
        db.ChainLayouts.Add(layout);
        await db.SaveChangesAsync();
        _selectedLayoutId = layout.Id;
        _lastSelectedLayoutId = layout.Id;
        await LoadLayouts();
        await OnLayoutChanged.InvokeAsync();
    }

    private async Task UpdateExisting()
    {
        if (_selectedLayoutId == null) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = await db.ChainLayouts.Include(cl => cl.Links)
            .FirstOrDefaultAsync(cl => cl.Id == _selectedLayoutId);
        if (layout == null) return;
        layout.Name = _layoutName;
        db.ChainLinks.RemoveRange(layout.Links);
        layout.Links = Links.Select(l => new ChainLink
        {
            TowerAMacAddress = l.TowerAMacAddress,
            TowerBMacAddress = l.TowerBMacAddress,
            EntryAtBothEnds = l.EntryAtBothEnds
        }).ToList();
        await db.SaveChangesAsync();
        await LoadLayouts();
        await OnLayoutChanged.InvokeAsync();
    }

    private async Task DeleteLayout()
    {
        if (_selectedLayoutId == null) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = await db.ChainLayouts.FindAsync(_selectedLayoutId);
        if (layout != null)
        {
            db.ChainLayouts.Remove(layout);
            await db.SaveChangesAsync();
        }
        _selectedLayoutId = null;
        _lastSelectedLayoutId = null;
        _layoutName = string.Empty;
        Links.Clear();
        await LinksChanged.InvokeAsync(Links);
        await LoadLayouts();
        await OnLayoutChanged.InvokeAsync();
    }

    private async Task ClosePanel()
    {
        Visible = false;
        await VisibleChanged.InvokeAsync(false);
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/ChainEditorPanel.razor OWLServer/OWLServer/Components/ConfigComponents/ChainEditorPanel.razor.cs
git commit -m "feat: add ChainEditorPanel with layout CRUD, link list, and graph preview"
```

---

### Task 6: Create `ChainEditor` page (full implementation)

**Files:**
- Create: `OWLServer/OWLServer/Components/Pages/AdminPages/ChainEditor.razor`
- Create: `OWLServer/OWLServer/Components/Pages/AdminPages/ChainEditor.razor.cs`

This is the core component — map rendering, SVG arrows for editor links, tower click handling, undo/redo, and composition of toolbar + panel.

- [ ] **Step 1: Create `ChainEditor.razor`**

```razor
@page "/Admin/ChainEditor"
@using OWLServer.Models
@using OWLServer.Services.Interfaces
@using OWLServer.Components.ConfigComponents
@using static OWLServer.Components.ConfigComponents.ChainEditorToolbar
@implements IDisposable

<PageTitle>Chain Link Editor</PageTitle>

<div style="height: 100vh; width: 100vw; position: relative; overflow: hidden;">

    @* Map *@
    <div style="position: relative; height: 100%;">
        <img src="@_MapService.GetCurrentMapUrl()"
             style="width:100%; height:100%; object-fit:fill;"
             @onclick="BackgroundTapped" />

        @* SVG overlay for editor links *@
        <svg viewBox="0 0 100 100" preserveAspectRatio="none"
             style="position:absolute;top:0;left:0;width:100%;height:100%;overflow:visible;pointer-events:none;">
            @foreach (var link in _editorLinks)
            {
                var towerA = GameStateService.TowerManagerService.Towers.Values
                    .FirstOrDefault(t => t.MacAddress == link.TowerAMacAddress && t.Location != null);
                var towerB = GameStateService.TowerManagerService.Towers.Values
                    .FirstOrDefault(t => t.MacAddress == link.TowerBMacAddress && t.Location != null);

                @if (towerA != null && towerB != null)
                {
                    var isSelected = _selectedLink == link;
                    var color = isSelected ? "#e74c3c" : "#00b4f1";
                    var width = isSelected ? "3.5" : "2.5";

                    if (link.EntryAtBothEnds)
                    {
                        var (ax1, ay1, mx, my) = Half(towerA, towerB);
                        var (bx1, by1, _, _) = Half(towerB, towerA);
                        <line @key="link" x1="@ax1" y1="@ay1" x2="@mx" y2="@my"
                              stroke="@color" stroke-width="@width" stroke-dasharray="5 3"
                              style="pointer-events:auto;cursor:pointer;"
                              @onclick="() => LinkClicked(link)" @onclick:stopPropagation="true" />
                        <line x1="@bx1" y1="@by1" x2="@mx" y2="@my"
                              stroke="@color" stroke-width="@width" stroke-dasharray="5 3"
                              style="pointer-events:none;" />
                    }
                    else
                    {
                        var (x1, y1, x2, y2) = Arrow(towerA, towerB);
                        <line @key="link" x1="@x1" y1="@y1" x2="@x2" y2="@y2"
                              stroke="@color" stroke-width="@width"
                              marker-end="url(#edit-arrow)"
                              style="pointer-events:auto;cursor:pointer;"
                              @onclick="() => LinkClicked(link)" @onclick:stopPropagation="true" />
                    }
                }
            }
            <defs>
                <marker id="edit-arrow" markerWidth="7" markerHeight="6" refX="7" refY="3"
                        orient="auto" markerUnits="userSpaceOnUse">
                    <polygon points="0 0, 7 3, 0 6" fill="#00b4f1"/>
                </marker>
            </defs>
        </svg>

        @* Towers *@
        @foreach (var tower in GameStateService.TowerManagerService.Towers.Values.Where(t => t.Location != null))
        {
            var inChain = _editorLinks.Any(l =>
                l.TowerAMacAddress == tower.MacAddress || l.TowerBMacAddress == tower.MacAddress);
            var isSource = _selectedSource?.MacAddress == tower.MacAddress;

            <div @key="tower.MacAddress"
                 style="@(tower.Location?.ToLocationString() ?? "") cursor:pointer; position:absolute; @(inChain ? "" : "opacity:0.35;")"
                 @onclick="() => TowerClicked(tower)" @onclick:stopPropagation="true">
                <TowerComponent Tower="tower" Editable="true" />
                @if (isSource)
                {
                    <div style="position:absolute;top:-4px;left:-4px;right:-4px;bottom:-4px;
                                border:3px solid #ff0;border-radius:50%;pointer-events:none;"></div>
                }
            </div>
        }
    </div>

    @* Toolbar *@
    <ChainEditorToolbar @bind-Mode="_mode"
                        CanUndo="@(_undoHistory.Count > 0)"
                        CanRedo="@(_redoHistory.Count > 0)"
                        OnUndo="@Undo" OnRedo="@Redo"
                        @bind-PanelVisible="_panelVisible" />

    @* Side Panel *@
    <ChainEditorPanel @bind-Visible="_panelVisible"
                      @ref="_panel"
                      Links="_editorLinks"
                      LinksChanged="OnPanelLinksChanged"
                      OnLayoutChanged="OnLayoutChanged" />
</div>
```

- [ ] **Step 2: Create `ChainEditor.razor.cs`**

```csharp
using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services.Interfaces;
using static OWLServer.Components.ConfigComponents.ChainEditorToolbar;

namespace OWLServer.Components.Pages.AdminPages;

public partial class ChainEditor : ComponentBase, IDisposable
{
    [Inject] public IGameStateService GameStateService { get; set; } = null!;
    [Inject] public IMapService _MapService { get; set; } = null!;

    private ChainEditorPanel? _panel;
    private List<ChainLink> _editorLinks = new();
    private EditorMode _mode = EditorMode.Draw;
    private bool _panelVisible;
    private Tower? _selectedSource;
    private ChainLink? _selectedLink;
    private Timer? _longPressTimer;
    private bool _isDragging;

    private readonly Stack<Snapshot> _undoHistory = new();
    private readonly Stack<Snapshot> _redoHistory = new();

    private record Snapshot(List<ChainLink> Links);

    private async Task OnPanelLinksChanged(List<ChainLink> links)
    {
        _editorLinks = links;
        StateHasChanged();
    }

    private async Task OnLayoutChanged()
    {
        _undoHistory.Clear();
        _redoHistory.Clear();
        _selectedSource = null;
        _selectedLink = null;
        StateHasChanged();
        await _panel!.LoadLayouts();
    }

    private void TowerClicked(Tower tower)
    {
        switch (_mode)
        {
            case EditorMode.Draw:
                if (_selectedSource == null)
                {
                    _selectedSource = tower;
                }
                else if (_selectedSource.MacAddress == tower.MacAddress)
                {
                    _selectedSource = null;
                }
                else
                {
                    if (!LinkExists(_selectedSource.MacAddress, tower.MacAddress))
                    {
                        PushUndo();
                        _editorLinks.Add(new ChainLink
                        {
                            TowerAMacAddress = _selectedSource.MacAddress,
                            TowerBMacAddress = tower.MacAddress,
                            EntryAtBothEnds = true
                        });
                    }
                    _selectedSource = null;
                }
                break;
        }
        StateHasChanged();
    }

    private void LinkClicked(ChainLink link)
    {
        switch (_mode)
        {
            case EditorMode.Erase:
                if (_selectedLink == link)
                {
                    PushUndo();
                    _editorLinks.Remove(link);
                    _selectedLink = null;
                }
                else
                {
                    _selectedLink = link;
                }
                break;

            case EditorMode.Direction:
                PushUndo();
                if (link.EntryAtBothEnds)
                {
                    link.EntryAtBothEnds = false;
                }
                else
                {
                    link.EntryAtBothEnds = true;
                    (link.TowerAMacAddress, link.TowerBMacAddress) =
                        (link.TowerBMacAddress, link.TowerAMacAddress);
                }
                break;
        }
        StateHasChanged();
    }

    private void BackgroundTapped()
    {
        _selectedSource = null;
        _selectedLink = null;
        StateHasChanged();
    }

    private bool LinkExists(string macA, string macB) =>
        _editorLinks.Any(l =>
            (l.TowerAMacAddress == macA && l.TowerBMacAddress == macB) ||
            (l.EntryAtBothEnds && l.TowerBMacAddress == macA && l.TowerAMacAddress == macB));

    private void PushUndo()
    {
        _undoHistory.Push(CloneLinks());
        _redoHistory.Clear();
    }

    private void Undo()
    {
        if (!_undoHistory.Any()) return;
        _redoHistory.Push(CloneLinks());
        _editorLinks = _undoHistory.Pop().Links;
        _selectedSource = null;
        _selectedLink = null;
        StateHasChanged();
    }

    private void Redo()
    {
        if (!_redoHistory.Any()) return;
        _undoHistory.Push(CloneLinks());
        _editorLinks = _redoHistory.Pop().Links;
        _selectedSource = null;
        _selectedLink = null;
        StateHasChanged();
    }

    private Snapshot CloneLinks() => new(_editorLinks.Select(l => new ChainLink
    {
        TowerAMacAddress = l.TowerAMacAddress,
        TowerBMacAddress = l.TowerBMacAddress,
        EntryAtBothEnds = l.EntryAtBothEnds
    }).ToList());

    // ── SVG helpers ──

    private static (string x1, string y1, string x2, string y2) Arrow(
        Tower src, Tower dst, double srcGap = 4.0, double destGap = 10.0)
    {
        double sx = src.Location!.Left * 100, sy = src.Location!.Top * 100;
        double ex = dst.Location!.Left * 100, ey = dst.Location!.Top * 100;
        double dx = ex - sx, dy = ey - sy;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) return (F(sx), F(sy), F(ex), F(ey));
        srcGap = Math.Min(srcGap, len * 0.4);
        destGap = Math.Min(destGap, len * 0.4);
        double nx = dx / len, ny = dy / len;
        return (F(sx + nx * srcGap), F(sy + ny * srcGap),
                F(ex - nx * destGap), F(ey - ny * destGap));
    }

    private static (string x1, string y1, string mx, string my) Half(
        Tower src, Tower dst, double gap = 4.0)
    {
        double sx = src.Location!.Left * 100, sy = src.Location!.Top * 100;
        double ex = dst.Location!.Left * 100, ey = dst.Location!.Top * 100;
        double dx = ex - sx, dy = ey - sy;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) return (F(sx), F(sy), F(ex), F(ey));
        gap = Math.Min(gap, len * 0.4);
        double nx = dx / len, ny = dy / len;
        return (F(sx + nx * gap), F(sy + ny * gap), F((sx + ex) / 2), F((sy + ey) / 2));
    }

    private static string F(double v) =>
        v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    public void Dispose() => _longPressTimer?.Dispose();
}
```

- [ ] **Step 3: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/ChainEditor.razor OWLServer/OWLServer/Components/Pages/AdminPages/ChainEditor.razor.cs
git commit -m "feat: add ChainEditor page with map-based link editing"
```

---

### Task 7: Simplify `GameModeChainBreakConfig`

**Files:**
- Modify: `OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor`
- Modify: `OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs`

- [ ] **Step 1: Replace `GameModeChainBreakConfig.razor`** (remove lines 68-187, add layout dropdown)

```razor
@using OWLServer.Models
@using OWLServer.Models.GameModes

<RadzenStack Orientation="Orientation.Vertical" Gap="1rem">

    @* Base config *@
    <RadzenStack>
        <RadzenFormField Text="Game Duration (min)" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.GameDurationInMinutes" Min="1"/>
        </RadzenFormField>
        <RadzenFormField Text="Max Tickets" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.MaxTickets" Min="1"/>
        </RadzenFormField>
        <RadzenFormField Text="Is Ticket" Variant="Variant.Flat">
            <RadzenCheckBox @bind-Value="CurrentGame.IsTicket"/>
        </RadzenFormField>
        <RadzenFormField Text="Point Distribution (s)" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.PointDistributionFrequencyInSeconds" Min="1"/>
        </RadzenFormField>
        <RadzenFormField Text="Chain Factor" Variant="Variant.Flat">
            <RadzenNumeric @bind-Value="CurrentGame.ChainFactor" TValue="double"/>
        </RadzenFormField>
    </RadzenStack>

    @* Active layout badge *@
    @if (CurrentGame.ActiveChainLayout != null)
    {
        <RadzenAlert AlertStyle="AlertStyle.Success" AllowClose="false">
            Aktives Layout: <strong>@CurrentGame.ActiveChainLayout.Name</strong>
            (@(CurrentGame.ActiveChainLayout.Links.Count) Links)
        </RadzenAlert>
    }
    else
    {
        <RadzenAlert AlertStyle="AlertStyle.Warning" AllowClose="false">
            Kein Chain-Layout aktiv — alle Tower frei einnehmbar.
        </RadzenAlert>
    }

    @* Layout Selector *@
    <RadzenCard Variant="Variant.Outlined">
        <RadzenText TextStyle="TextStyle.Subtitle2" class="rz-mb-2">Chain Layout</RadzenText>
        <RadzenStack Gap=".5rem">
            <RadzenDropDown TValue="int?" Data="@_layouts"
                            TextProperty="Name" ValueProperty="Id"
                            @bind-Value="_activeLayoutId"
                            Placeholder="Kein Layout ausgewählt"
                            Style="width:100%" />
            <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem">
                <RadzenButton Text="Aktivieren" Size="ButtonSize.Small"
                              ButtonStyle="ButtonStyle.Success"
                              Click="ActivateSelected"
                              Disabled="@(_activeLayoutId == null)" />
                <RadzenButton Text="Editor öffnen" Size="ButtonSize.Small"
                              ButtonStyle="ButtonStyle.Secondary"
                              Click="OpenEditor" />
            </RadzenStack>
        </RadzenStack>
    </RadzenCard>

</RadzenStack>
```

- [ ] **Step 2: Replace `GameModeChainBreakConfig.razor.cs`** (remove all editor methods, keep layout selector logic)

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class GameModeChainBreakConfig : ComponentBase
{
    [Parameter] public GameModeChainBreak CurrentGame { get; set; } = null!;

    [Inject] public IGameStateService GameStateService { get; set; } = null!;
    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private List<ChainLayout> _layouts = new();
    private int? _activeLayoutId;

    protected override async Task OnInitializedAsync()
    {
        await LoadLayouts();
        if (CurrentGame.ActiveChainLayout != null)
            _activeLayoutId = CurrentGame.ActiveChainLayout.Id;
    }

    private async Task LoadLayouts()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _layouts = await db.ChainLayouts.Include(cl => cl.Links).ToListAsync();
    }

    private async Task ActivateSelected()
    {
        if (_activeLayoutId == null) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = await db.ChainLayouts.Include(cl => cl.Links)
            .FirstOrDefaultAsync(cl => cl.Id == _activeLayoutId);
        if (layout != null)
            CurrentGame.ActiveChainLayout = layout;
    }

    private void OpenEditor() => Navigation.NavigateTo("/Admin/ChainEditor");
}
```

- [ ] **Step 3: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor OWLServer/OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs
git commit -m "refactor: simplify GameModeChainBreakConfig to layout selector + editor link"
```

---

### Task 8: Add Chain Editor nav to AdminPanel

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/AdminPanel.razor`

- [ ] **Step 1: Check if `NavigationManager` is injected in `AdminPanel.razor.cs`**

Read `AdminPanel.razor.cs`. If `NavigationManager` is not injected, add:
```csharp
[Inject] public NavigationManager Navigation { get; set; } = null!;
```

- [ ] **Step 2: Add the nav button near the ChainBreak config area**

In `AdminPanel.razor`, find the `case GameModeChainBreak:` block (around line 147) and add above or near it:

```razor
<RadzenButton Text="Chain Editor" Icon="link" ButtonStyle="ButtonStyle.Info"
              Size="ButtonSize.Small"
              Click="@(() => Navigation.NavigateTo("/Admin/ChainEditor"))"
              class="rz-mb-2" />
```

- [ ] **Step 3: Build**

Run: `dotnet build --project OWLServer/OWLServer/OWLServer.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/AdminPages/AdminPanel.razor OWLServer/OWLServer/Components/Pages/AdminPages/AdminPanel.razor.cs
git commit -m "feat: add Chain Editor nav button to AdminPanel"
```

---

### Task 9: Integration verification

- [ ] **Step 1: Full solution build**

```bash
dotnet build --project OWLServer/OWLServer/OWLServer.csproj
```

Expected: Zero errors. Warnings are acceptable if they pre-exist.

- [ ] **Step 2: Run dev server**

```bash
dotnet run --project OWLServer/OWLServer/OWLServer
```

Expected: Listening on `https://localhost:65058` and `http://localhost:65059`.

- [ ] **Step 3: Smoke test**

| Test | Expected |
|---|---|
| Open `/Admin/ChainEditor` | Page loads, map displays with towers |
| Tap tower in Draw mode | Tower gets yellow ring (selected source) |
| Tap another tower | Blue dashed link appears between them |
| Switch to Direction mode, tap link | Link cycles: ↔ → → → ← (back to ↔) |
| Switch to Erase mode, tap link | Link turns red; tap again → deleted |
| Press ☰ Panel | Side panel slides in from right |
| Enter name, press "Neu speichern" | Layout saved, appears in dropdown |
| Select layout from dropdown | Layout loads into editor |
| Make changes, press "Aktualisieren" | Changes saved |
| Navigate to Admin | Game config layout dropdown lists saved layouts |
| Select layout, press "Aktivieren" | Green alert shows active layout |
| Undo/Redo buttons | Undo reverts last action, Redo restores |
| Unlinked towers | Appear at 35% opacity, still tappable |
| Self-link (tap same tower twice) | Selection clears, no link created |
| Duplicate link (tap linked pair again) | No duplicate added |

- [ ] **Step 4: Commit any fixes**

```bash
git add -A
git commit -m "chore: integration fixes for chainlink editor"
```
