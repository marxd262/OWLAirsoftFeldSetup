using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.Pages.AdminPages;

public partial class SoundTest : ComponentBase
{
    [Inject] IAudioService AudioService { get; set; } = null!;
    [Inject] IJSRuntime JS { get; set; } = null!;

    private Dictionary<Sounds, string> _pendingAssignments = new();
    private Dictionary<Sounds, int> _pendingDelays = new();
    private string? _statusMessage;
    private bool _statusIsError;
    private bool _isUploading;

    protected override void OnInitialized()
    {
        foreach (Sounds s in Enum.GetValues<Sounds>())
        {
            _pendingAssignments[s] = AudioService.GetAssignedFile(s);
            _pendingDelays[s] = AudioService.GetDelay(s);
        }
    }

    private IReadOnlyList<string> AvailableFiles => AudioService.GetAvailableSoundFiles();

    private void OnDropdownChanged(Sounds slot, string? value)
    {
        _pendingAssignments[slot] = value ?? "";
    }

    private void SaveSlot(Sounds slot)
    {
        try
        {
            AudioService.SetSoundFile(slot, _pendingAssignments[slot]);
            AudioService.SetDelay(slot, _pendingDelays[slot]);
            SetStatus($"{slot} gespeichert.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Fehler: {ex.Message}", true);
        }
    }

    private void PlaySound(Sounds sound) => AudioService.PlaySound(sound);

    private void StopSound() => AudioService.StopSound();

    private async Task TriggerFileInput() =>
        await JS.InvokeVoidAsync("eval", "document.getElementById('soundFileInput').click()");

    private async Task OnUploadChange(InputFileChangeEventArgs e)
    {
        _isUploading = true;
        _statusMessage = null;
        StateHasChanged();

        try
        {
            foreach (var file in e.GetMultipleFiles(10))
            {
                await using var stream = file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
                await AudioService.SaveUploadedFileAsync(file.Name, stream);
            }
            SetStatus("Upload erfolgreich.", false);

            foreach (Sounds s in Enum.GetValues<Sounds>())
                _pendingAssignments[s] = AudioService.GetAssignedFile(s);
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

    private void DeleteFile(string filename)
    {
        try
        {
            AudioService.DeleteSoundFile(filename);
            foreach (Sounds s in Enum.GetValues<Sounds>())
                _pendingAssignments[s] = AudioService.GetAssignedFile(s);
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
