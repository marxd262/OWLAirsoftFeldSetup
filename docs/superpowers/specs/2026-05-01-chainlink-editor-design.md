# ChainLink Map Editor — Design Spec

**Date:** 2026-05-01
**Status:** Approved

## Overview

Replace the current list-based ChainLink editor with a full-page map-first editor that supports branching and shows real tower positions. A collapsible side panel provides layout management, a link list, and a live graph preview.

### Problems solved

1. **No branching support** — current sequential builder only does linear chains
2. **No visual feedback** — flat list of links, can't see how towers connect
3. **Hard to use on touchscreen** — the admin config runs on a touch display in the field
4. **Separate editing contexts** — creating layouts and selecting them for a game mode are entangled

---

## Interaction Model

### Touch interactions

| Action | Gesture | Context |
|---|---|---|
| Create link (tap) | Tap tower A → tap tower B | Draw mode |
| Create link (drag) | Long-press tower A → drag finger to tower B → release | Draw mode |
| Delete link | Tap link → confirm (highlight red, tap again) | Erase mode |
| Cycle direction | Tap link → `↔` → `A→B` → `B→A` | Direction mode |

### Tower appearance

- **Linked (in chain):** Full color, larger touch target (4rem), bold ring
- **Unlinked (outside chain):** 30% opacity, gray, standard size but still tappable

### Constraints

- Self-links prevented (tap same tower → toast)
- Duplicate links prevented (link already exists → toast)
- Links default to bidirectional on creation

---

## Screen Layout

### Full-page at `/Admin/ChainEditor`

```
┌──────────────────────────────────────────────┐
│                                              │
│                 MAP CANVAS                   │
│         (towers + links + touch)             │
│                                              │
│                                       ┌──────┤
│                                       │ Side │
│                                       │Panel │
│                                       │(300px│
│                                       │slide)│
├───────────────────────────────────────┴──────┤
│ [Draw] [Erase] [Dir]  │  ↶ ↷  │  [☰ Panel]  │  ← 48px toolbar
└──────────────────────────────────────────────┘
```

- Map: `100vw × calc(100vh - 48px)`
- Toolbar: fixed bottom, 48px height
- Side panel: slides in from right, 300px, with backdrop overlay

### Navigation entry

Admin Panel gets a "Chain Editor" button. `GameModeChainBreakConfig` no longer embeds an editor — it only shows base config + a dropdown to select an active layout.

---

## Component Architecture

```
ChainEditor.razor                    ← page host, owns _editorLinks state
  ├── ChainMapEditor.razor           ← interactive SVG map
  │     └── reads TowerManagerService.Towers
  │     └── touch state machine (Draw/Erase/Direction)
  ├── ChainEditorToolbar.razor       ← mode buttons, undo/redo, panel toggle
  └── ChainEditorPanel.razor         ← slide-in side panel
        ├── Layout CRUD (dropdown, name, save/update/delete)
        ├── Link list (synced with _editorLinks)
        └── ChainGraphPreview.razor  ← live SVG topology diagram
```

### Map canvas extraction

`MapCanvas.razor` extracted from `Map.razor` — renders tower circles + optional child overlay content. `Map.razor` (homepage) inherits it with game-state coloring. `ChainMapEditor.razor` (editor) extends it with touch interactivity.

### State ownership

`ChainEditor` owns `List<ChainLink> _editorLinks` in memory. All subcomponents read this list or receive callbacks. No separate state service — editor state is transient and page-scoped.

---

## Side Panel Sections

| Section | Content |
|---|---|
| **Layout Management** (top) | Saved layouts dropdown, name TextBox, Save New / Update / Delete buttons. If loaded layout is active in a game mode, show badge. |
| **Link List** (middle) | All `_editorLinks` as rows: `T1 ↔ T2` + delete. Tap highlights on map. Grouped by source tower. |
| **Graph Preview** (bottom) | Live SVG graph diagram. BFS-based layered layout. Entry towers at top, leaf towers at bottom. Nodes tappable to locate on map. |

---

## Data Flow

```
User gestures on map → ChainMapEditor touch events
  → ChainEditor updates _editorLinks
  → ChainMapEditor re-renders arrows
  → ChainEditorPanel link list + graph preview re-render

Save: _editorLinks → new/updated ChainLayout → DatabaseContext.SaveChangesAsync()
Load: DatabaseContext → ChainLayout (inc. Links) → _editorLinks
Activate: layout dropdown in GameModeChainBreakConfig → CurrentGame.ActiveChainLayout = layout
```

---

## Existing Code Changes

### New files

| File | Purpose |
|---|---|
| `Components/Pages/AdminPages/ChainEditor.razor` | Full-page editor |
| `Components/Pages/AdminPages/ChainEditor.razor.cs` | Code-behind |
| `Components/MapComponents/MapCanvas.razor` | Base reusable map |
| `Components/MapComponents/ChainMapEditor.razor` | Interactive map for editor |
| `Components/MapComponents/ChainMapEditor.razor.cs` | Touch interaction logic |
| `Components/ConfigComponents/ChainEditorToolbar.razor` | Bottom toolbar |
| `Components/ConfigComponents/ChainEditorPanel.razor` | Side panel |
| `Components/ConfigComponents/ChainEditorPanel.razor.cs` | Panel logic |
| `Components/ConfigComponents/ChainGraphPreview.razor` | SVG graph diagram |

### Modified files

| File | Changes |
|---|---|
| `Map.razor` | Extract common rendering into `MapCanvas`, use composition |
| `TowerComponent.razor` | Add `Editable` parameter (larger size when true) |
| `AdminPanel.razor` | Add "Chain Editor" nav button |
| `GameModeChainBreakConfig.razor` | Remove embedded editor; add layout dropdown + "Manage Layouts" link |
| `GameModeChainBreakConfig.razor.cs` | Remove editor methods; add layout dropdown binding |

### Deleted code

| Location | What |
|---|---|
| `GameModeChainBreakConfig.razor` lines 68-187 | Inline editor UI (link builder, manual link form, layout CRUD) |
| `GameModeChainBreakConfig.razor.cs` lines 47-244 | Editor methods (LoadLayoutIntoEditor, StartChainBuilding, AddChainLink, etc.) |

---

## Edge Cases

| State | Behavior |
|---|---|
| No towers in system | Placeholder: "No towers configured. Set up towers in Map Tests first." Link to `/MapTests`. |
| No tower positions saved | Placeholder: "Tower positions not set. Please configure positions first." Link to `/MapTests`. |
| Empty layout | All towers grayed. "Save New" disabled until ≥1 link exists. |
| Self-link | Prevented. Toast: "Cannot link a tower to itself." |
| Duplicate link | Prevented. Toast: "Link already exists between [A] and [B]." |
| Unsaved changes on navigate | Browser confirm dialog. Save/Update buttons pulse. |
| Delete active layout | Confirm: "This layout is active. Deleting it will unset it from ChainBreak mode." |
| Stale tower references in saved layout | Warning on load: "Towers X, Y no longer exist. Links will be removed." Auto-clean. |

---

## Non-Functional

| Concern | Approach |
|---|---|
| Dependencies | No new NuGet packages. Pure Blazor + SVG + CSS. |
| Touch support | CSS `touch-action: manipulation`. `@ontouchstart`/`@onmousedown` handlers. |
| Target size | Towers render at 4rem (64px) in editor mode. Toolbar buttons ≥ 44px. |
| Responsive | Map fills available space. Side panel fixed 300px, slides over map. |
| Performance | Small tower count (<50). SVG re-render via `StateHasChanged`. No virtualization needed. |
| Graph layout | Custom BFS-based layered layout. Small N means simple algorithm suffices. |

---

## Decisions Log

| Decision | Rationale |
|---|---|
| Full-page, not embedded | Touch needs max map space. Existing admin panel is already dense. |
| Both tap-tap and long-press-drag | Tap for precision, drag for speed. Both work on touchscreens. |
| Simple multiple arrows for branching | No new concepts. Visual clarity at small scale. |
| Tap link to cycle direction | Fastest on touch. Three states (↔ / A→B / B→A) cover all needs. |
| Collapsible side panel | Keeps map full-screen by default. Slide-in for management tasks. |
| Extract MapCanvas base component | Reuse tower positioning for both gameplay map and editor map. |
| SVG graph preview, not ASCII | More readable. Same SVG skills already used in Map.razor. |
| Separate editor from game config | Clean separation: create layouts in one place, pick one in another. |
