# Bulk Tower Creation on DebugApi — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a bulk tower creation UI to the DebugApi page so devs can generate N test towers at once, with auto-incrementing MAC addresses that continue from the highest existing prefix.

**Architecture:** Single-file change to `DebugApi.razor`. A new `_bulkCount` field, a `BulkRegisterTowers()` method that scans existing MACs for the `DE:B0:00:00:00:XX` pattern, finds the highest suffix, then loops calling the existing `POST /api/RegisterTower` endpoint. Uses the same `_regIp` from the existing form.

**Tech Stack:** Blazor Interactive Server, Radzen.Blazor, .NET 8

---

### Task 1: Add `_bulkCount` field and `BulkRegisterTowers()` method

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/DebugApi.razor`

- [ ] **Step 1: Add `_bulkCount` field**

Add the following field near the existing `_regIp` declaration (around line 217):

```csharp
private int _bulkCount = 3;
```

- [ ] **Step 2: Add `BulkRegisterTowers()` method**

Add the method inside `@code { }` block, after `RegisterTower()` (around line 318):

```csharp
private async Task BulkRegisterTowers()
{
    if (_bulkCount <= 0) return;

    AddLog($"→ Bulk: Create {_bulkCount} Towers");

    var towers = GameStateService.TowerManagerService.Towers;
    int highestSuffix = 0;

    foreach (var key in towers.Keys)
    {
        if (key.StartsWith("DE:B0:00:00:00:") && key.Length == 17)
        {
            var suffix = key.Substring(15, 2);
            if (int.TryParse(suffix, System.Globalization.NumberStyles.HexNumber, null, out var val))
            {
                if (val > highestSuffix) highestSuffix = val;
            }
        }
    }

    var created = 0;
    for (int i = 1; i <= _bulkCount; i++)
    {
        var next = highestSuffix + i;
        if (next > 0xFF) break;

        var mac = $"DE:B0:00:00:00:{next:X2}";
        var ip = string.IsNullOrWhiteSpace(_regIp) ? "1.1.1.1" : _regIp;
        await ApiPost($"/api/RegisterTower?id={mac}&ip={ip}");
        created++;
    }

    AddLog($"  Created {created} tower{(created != 1 ? "s" : "")}");
    _bulkCount = 3;
}
```

- [ ] **Step 3: Build and verify compilation**

```bash
dotnet build OWLServer/OWLServer/OWLServer.csproj
```

Expected: Build succeeds with no errors.

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/DebugApi.razor
git commit -m "feat: add BulkRegisterTowers method with smart MAC continuation"
```

---

### Task 2: Add bulk create UI elements

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/DebugApi.razor`

- [ ] **Step 1: Add separator and bulk UI in the "Tower registrieren" card**

Replace the `RegisterTower` method binding (the closing `</RadzenStack>` at line 40) with the extended version. Change lines 38-41:

From:
```razor
                    <RadzenButton Icon="add" ButtonStyle="ButtonStyle.Success" Text="Raw"
                                  Click="RegisterTower"/>
                </RadzenStack>
```

To:
```razor
                    <RadzenButton Icon="add" ButtonStyle="ButtonStyle.Success" Text="Add"
                                  Click="RegisterTower"/>
                </RadzenStack>
                <RadzenStack Orientation="Orientation.Horizontal" Gap=".5rem" AlignItems="AlignItems.End"
                             Style="margin-top:.75rem;border-top:1px solid var(--rz-border-color);padding-top:.75rem">
                    <RadzenFormField Text="Anzahl">
                        <RadzenNumeric @bind-Value="_bulkCount" Min="1" Max="255" Style="width:70px"/>
                    </RadzenFormField>
                    <RadzenButton ButtonStyle="ButtonStyle.Success" Text="@($"Create {_bulkCount} Towers")"
                                  Click="BulkRegisterTowers"/>
                </RadzenStack>
```

Note: The existing button text was `"Raw"` (typo at line 38-39) — this fixes it to `"Add"` while adding the bulk section. If the original code says `"Raw"`, we fix that too.

- [ ] **Step 2: Build and verify compilation**

```bash
dotnet build OWLServer/OWLServer/OWLServer.csproj
```

Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add OWLServer/OWLServer/Components/Pages/DebugApi.razor
git commit -m "feat: add bulk tower creation UI to DebugApi page"
```
