using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using OWLServer.Models;
using OWLServer.Services;

namespace OWLServer.Components.Pages;

public partial class MapTests : ComponentBase
{
    [Inject] GameStateService _GameStateService { get; set; } = null!;
    [Inject] MapService MapService { get; set; } = null!;
    [Inject] IJSRuntime JS { get; set; } = null!;

    private Tower? selectedTower;

    private string _pendingMapFile = "";
    private string? _statusMessage;
    private bool _statusIsError;
    private bool _isUploading;

    protected override void OnInitialized()
    {
        _pendingMapFile = MapService.GetCurrentMapFile();
    }

    private IReadOnlyList<string> AvailableMapFiles => MapService.GetAvailableMapFiles();

    private void UpdateTower(ElementLocation? location)
    {
        if (selectedTower != null)
        {
            _GameStateService.TowerManagerService.Towers.ContainsKey(selectedTower.MacAddress);
            selectedTower.Location = location;
        }
    }

    private void BtnDeleteLocationClick()
    {
        UpdateTower(null);
        StateHasChanged();
    }

    private void MapClicked(ElementLocation? location)
    {
        if (selectedTower?.Location == null && location != null)
            UpdateTower(location);
        StateHasChanged();
    }

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
