using Microsoft.AspNetCore.Components;
using OWLServer.Components.ConfigComponents;
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
        if (_panel != null) await _panel.LoadLayouts();
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
