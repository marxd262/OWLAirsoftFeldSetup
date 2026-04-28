using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class TowerControlConfig : ComponentBase
{
    [Parameter] public GameModeConquest CurrentGame { get; set; } = null!;

    [Inject] public IGameStateService GameStateService { get; set; } = null!;
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
