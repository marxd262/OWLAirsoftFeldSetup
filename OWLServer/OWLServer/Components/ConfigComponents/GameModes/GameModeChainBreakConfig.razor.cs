// OWLServer/Components/ConfigComponents/GameModes/GameModeChainBreakConfig.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class GameModeChainBreakConfig : ComponentBase
{
    [Parameter] public GameModeChainBreak CurrentGame { get; set; } = null!;

    [Inject] public GameStateService GameStateService { get; set; } = null!;
    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;

    private List<ChainLayout> _savedLayouts = new();

    // Editor state
    private List<ChainLink> _editorLinks = new();
    private int? _editingLayoutId;
    private string _newLayoutName = string.Empty;

    // New-link form
    private string _fromMac = string.Empty;
    private string _toMac = string.Empty;
    private bool _isBidirectional;

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
                FromTowerMacAddress = l.FromTowerMacAddress,
                ToTowerMacAddress   = l.ToTowerMacAddress,
                IsBidirectional     = l.IsBidirectional
            })
            .ToList();
    }

    private void AddLink()
    {
        if (string.IsNullOrEmpty(_fromMac) || string.IsNullOrEmpty(_toMac)) return;
        if (_fromMac == _toMac) return;
        _editorLinks.Add(new ChainLink
        {
            FromTowerMacAddress = _fromMac,
            ToTowerMacAddress   = _toMac,
            IsBidirectional     = _isBidirectional
        });
        _fromMac = string.Empty;
        _toMac   = string.Empty;
        _isBidirectional = false;
    }

    private void RemoveLink(ChainLink link) => _editorLinks.Remove(link);

    private async Task SaveAsNew()
    {
        if (string.IsNullOrWhiteSpace(_newLayoutName)) return;
        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = new ChainLayout
        {
            Name  = _newLayoutName,
            Links = _editorLinks.Select(l => new ChainLink
            {
                FromTowerMacAddress = l.FromTowerMacAddress,
                ToTowerMacAddress   = l.ToTowerMacAddress,
                IsBidirectional     = l.IsBidirectional
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
            FromTowerMacAddress = l.FromTowerMacAddress,
            ToTowerMacAddress   = l.ToTowerMacAddress,
            IsBidirectional     = l.IsBidirectional
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

    private string LinkLabel(ChainLink link)
    {
        var arrow = link.IsBidirectional ? "↔" : "→";
        return $"{TowerLabel(link.FromTowerMacAddress)} {arrow} {TowerLabel(link.ToTowerMacAddress)}";
    }
}
