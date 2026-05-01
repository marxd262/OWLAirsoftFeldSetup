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
