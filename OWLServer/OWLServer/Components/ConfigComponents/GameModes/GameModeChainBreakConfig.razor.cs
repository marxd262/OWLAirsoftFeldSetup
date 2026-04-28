// OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs
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

    private List<ChainLayout> _savedLayouts = new();

    // Editor state
    private List<ChainLink> _editorLinks = new();
    private int? _editingLayoutId;
    private string _newLayoutName = string.Empty;

    // Manual single-link form
    private string _manualTowerA = string.Empty;
    private string _manualTowerB = string.Empty;
    private bool _manualEntryAtBothEnds = true;

    // Sequential chain builder state
    private bool _chainBuilding;
    private string? _chainLastMac;
    private string _chainNextMac = string.Empty;
    private bool _chainEntryAtBothEnds = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadSavedLayouts();
    }

    private async Task LoadSavedLayouts()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _savedLayouts = await db.ChainLayouts.Include(cl => cl.Links).ToListAsync();
    }

    private void LoadLayoutIntoEditor(ChainLayout layout)
    {
        _editingLayoutId = layout.Id;
        _newLayoutName = layout.Name;
        _editorLinks = layout.Links
            .Select(l => new ChainLink
            {
                TowerAMacAddress = l.TowerAMacAddress,
                TowerBMacAddress = l.TowerBMacAddress,
                EntryAtBothEnds  = l.EntryAtBothEnds
            })
            .ToList();
        _chainBuilding = false;
        _chainLastMac = null;
    }

    // ── Sequential chain builder ──

    private void StartChainBuilding()
    {
        _chainBuilding = true;
        _chainLastMac = null;
        _chainNextMac = string.Empty;
        _chainEntryAtBothEnds = true;
    }

    private void EndChainBuilding()
    {
        _chainBuilding = false;
        _chainLastMac = null;
        _chainNextMac = string.Empty;
    }

    private void CancelChainBuilding()
    {
        // Remove links added during this building session
        _editorLinks.Clear();
        EndChainBuilding();
    }

    private string ChainPreview
    {
        get
        {
            if (!_editorLinks.Any()) return "—";
            var macs = new List<string>();
            foreach (var link in _editorLinks)
            {
                if (!macs.Any())
                    macs.Add(link.TowerAMacAddress);
                macs.Add(link.TowerBMacAddress);
            }
            return string.Join(" → ", macs.Select(TowerShortLabel));
        }
    }

    private List<Tower> AvailableChainTowers =>
        GameStateService.TowerManagerService.Towers.Values
            .Where(t => !_editorLinks.Any() || t.MacAddress != _chainLastMac)
            .ToList();

    private void AddChainLink()
    {
        if (string.IsNullOrEmpty(_chainNextMac)) return;

        if (_chainLastMac == null)
        {
            // First tower selected, just record it
            _chainLastMac = _chainNextMac;
            _chainNextMac = string.Empty;
            return;
        }

        if (_chainNextMac == _chainLastMac) return;

        _editorLinks.Add(new ChainLink
        {
            TowerAMacAddress = _chainLastMac,
            TowerBMacAddress = _chainNextMac,
            EntryAtBothEnds  = _chainEntryAtBothEnds
        });
        _chainLastMac = _chainNextMac;
        _chainNextMac = string.Empty;
        _chainEntryAtBothEnds = true;
    }

    // ── Manual single-link form ──

    private void AddManualLink()
    {
        if (string.IsNullOrEmpty(_manualTowerA) || string.IsNullOrEmpty(_manualTowerB)) return;
        if (_manualTowerA == _manualTowerB) return;
        _editorLinks.Add(new ChainLink
        {
            TowerAMacAddress = _manualTowerA,
            TowerBMacAddress = _manualTowerB,
            EntryAtBothEnds  = _manualEntryAtBothEnds
        });
        _manualTowerA = string.Empty;
        _manualTowerB = string.Empty;
        _manualEntryAtBothEnds = true;
    }

    private void RemoveLink(ChainLink link)
    {
        _editorLinks.Remove(link);
        if (!_editorLinks.Any())
        {
            _chainBuilding = false;
            _chainLastMac = null;
        }
    }

    private async Task SaveAsNew()
    {
        if (string.IsNullOrWhiteSpace(_newLayoutName)) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = new ChainLayout
        {
            Name  = _newLayoutName,
            Links = _editorLinks.Select(l => new ChainLink
            {
                TowerAMacAddress = l.TowerAMacAddress,
                TowerBMacAddress = l.TowerBMacAddress,
                EntryAtBothEnds  = l.EntryAtBothEnds
            }).ToList()
        };
        db.ChainLayouts.Add(layout);
        await db.SaveChangesAsync();
        _editingLayoutId = layout.Id;
        await LoadSavedLayouts();
    }

    private async Task UpdateExisting()
    {
        if (_editingLayoutId == null) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = await db.ChainLayouts.Include(cl => cl.Links)
                             .FirstOrDefaultAsync(cl => cl.Id == _editingLayoutId);
        if (layout == null) return;
        layout.Name = _newLayoutName;
        db.ChainLinks.RemoveRange(layout.Links);
        layout.Links = _editorLinks.Select(l => new ChainLink
        {
            TowerAMacAddress = l.TowerAMacAddress,
            TowerBMacAddress = l.TowerBMacAddress,
            EntryAtBothEnds  = l.EntryAtBothEnds
        }).ToList();
        await db.SaveChangesAsync();
        await LoadSavedLayouts();
        if (CurrentGame.ActiveChainLayout?.Id == _editingLayoutId)
            CurrentGame.ActiveChainLayout = _savedLayouts.FirstOrDefault(sl => sl.Id == _editingLayoutId);
    }

    private async Task DeleteLayout(ChainLayout layout)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var entity = await db.ChainLayouts.FindAsync(layout.Id);
        if (entity != null)
        {
            db.ChainLayouts.Remove(entity);
            await db.SaveChangesAsync();
        }
        if (CurrentGame.ActiveChainLayout?.Id == layout.Id)
            CurrentGame.ActiveChainLayout = null;
        if (_editingLayoutId == layout.Id)
        {
            _editingLayoutId = null;
            _editorLinks.Clear();
            _newLayoutName = string.Empty;
        }
        await LoadSavedLayouts();
    }

    private void ActivateLayout(ChainLayout layout)
    {
        CurrentGame.ActiveChainLayout = layout;
    }

    private string TowerLabel(string mac)
    {
        if (GameStateService.TowerManagerService.Towers.TryGetValue(mac, out var t))
            return $"{t.DisplayLetter} – {t.Name}";
        return mac;
    }

    private string TowerShortLabel(string mac)
    {
        if (GameStateService.TowerManagerService.Towers.TryGetValue(mac, out var t))
            return t.DisplayLetter;
        return mac;
    }

    private string LinkLabel(ChainLink link)
    {
        var arrow = link.EntryAtBothEnds ? "↔" : "→";
        return $"{TowerShortLabel(link.TowerAMacAddress)} {arrow} {TowerShortLabel(link.TowerBMacAddress)}";
    }
}
