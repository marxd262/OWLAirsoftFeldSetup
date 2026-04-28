using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Services;

namespace OWLServer.Components.Pages;

public partial class MapTests : ComponentBase
{
    [Inject] GameStateService _GameStateService { get; set; } = null!;
    [Inject] MapService MapService { get; set; } = null!;
    [Inject] IJSRuntime JS { get; set; } = null!;
    [Inject] IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;

    private Tower? selectedTower;

    private List<TowerPositionLayout> _savedLayouts = new();
    private string _newLayoutName = string.Empty;

    private string _pendingMapFile = "";
    private string? _statusMessage;
    private bool _statusIsError;
    private bool _isUploading;

    protected override async Task OnInitializedAsync()
    {
        _pendingMapFile = MapService.GetCurrentMapFile();
        await LoadSavedLayouts();
    }

    private IReadOnlyList<string> AvailableMapFiles => MapService.GetAvailableMapFiles();

    // ── Tower positioning ──

    private void OnTowerSelected()
    {
        _statusMessage = null;
    }

    private void DeletePosition()
    {
        if (selectedTower == null) return;
        selectedTower.Location = null;
        _statusMessage = null;
        StateHasChanged();
    }

    private void MapClicked(ElementLocation? location)
    {
        if (selectedTower == null || location == null) return;
        selectedTower.Location = new ElementLocation
        {
            Top = location.Top,
            Left = location.Left
        };
        _statusMessage = $"Tower {selectedTower.DisplayLetter} positioniert ({location.Left:F2}, {location.Top:F2})";
        _statusIsError = false;
        StateHasChanged();
    }

    // ── Layout persistence ──

    private async Task LoadSavedLayouts()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _savedLayouts = await db.TowerPositionLayouts
            .Include(tpl => tpl.Positions)
            .ToListAsync();
    }

    private void LoadLayout(TowerPositionLayout savedLayout)
    {
        foreach (var pos in savedLayout.Positions)
        {
            if (_GameStateService.TowerManagerService.Towers.TryGetValue(pos.MacAddress, out var tower))
            {
                tower.Location = new ElementLocation
                {
                    Top = pos.Top,
                    Left = pos.Left
                };
            }
        }
        SetStatus($"Layout \"{savedLayout.Name}\" geladen ({savedLayout.Positions.Count} Positionen).", false);
        StateHasChanged();
    }

    private async Task SaveLayout()
    {
        if (string.IsNullOrWhiteSpace(_newLayoutName)) return;

        var towersWithPos = _GameStateService.TowerManagerService.Towers.Values
            .Where(t => t.Location != null)
            .ToList();

        if (!towersWithPos.Any())
        {
            SetStatus("Keine positionierten Tower zum Speichern.", true);
            return;
        }

        await using var db = await DbFactory.CreateDbContextAsync();
        var layout = new TowerPositionLayout
        {
            Name = _newLayoutName.Trim(),
            Positions = towersWithPos.Select(t => new TowerPosition
            {
                MacAddress = t.MacAddress,
                Top = t.Location!.Top,
                Left = t.Location!.Left
            }).ToList()
        };
        db.TowerPositionLayouts.Add(layout);
        await db.SaveChangesAsync();

        _newLayoutName = string.Empty;
        await LoadSavedLayouts();
        SetStatus($"Layout \"{layout.Name}\" gespeichert ({layout.Positions.Count} Positionen).", false);
    }

    private async Task DeleteLayout(TowerPositionLayout savedLayout)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var entity = await db.TowerPositionLayouts.FindAsync(savedLayout.Id);
        if (entity != null)
        {
            db.TowerPositionLayouts.Remove(entity);
            await db.SaveChangesAsync();
        }
        await LoadSavedLayouts();
        SetStatus($"Layout \"{savedLayout.Name}\" gelöscht.", false);
    }

    // ── Map file management ──

    private void SetMap()
    {
        try
        {
            MapService.SetCurrentMapFile(_pendingMapFile);
            SetStatus("Karte aktiviert.", false);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            SetStatus($"Fehler: {ex.Message}", true);
        }
    }

    private async Task TriggerFileInput() =>
        await JS.InvokeVoidAsync("eval", "document.getElementById('mapFileInput').click()");

    private async Task OnUploadChange(InputFileChangeEventArgs e)
    {
        _isUploading = true;
        _statusMessage = null;
        StateHasChanged();

        try
        {
            string lastUploaded = "";
            foreach (var file in e.GetMultipleFiles(5))
            {
                await using var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
                await MapService.SaveUploadedMapFileAsync(file.Name, stream);
                lastUploaded = file.Name;
            }
            if (!string.IsNullOrEmpty(lastUploaded))
                _pendingMapFile = lastUploaded;
            SetStatus("Upload erfolgreich.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Upload-Fehler: {ex.Message}", true);
        }
        finally
        {
            _isUploading = false;
        }
    }

    private void DeleteMapFile(string filename)
    {
        try
        {
            MapService.DeleteMapFile(filename);
            if (_pendingMapFile == filename)
                _pendingMapFile = MapService.GetCurrentMapFile();
            SetStatus($"{filename} gelöscht.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Fehler: {ex.Message}", true);
        }
    }

    private void SetStatus(string msg, bool isError)
    {
        _statusMessage = msg;
        _statusIsError = isError;
    }
}
