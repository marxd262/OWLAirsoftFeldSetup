using OWLServer.Models;

namespace OWLServer.Services.Interfaces;

public interface IAudioService
{
    void PlaySound(Sounds sound);
    void StopSound();
    int GetDelay(Sounds sound);
    void SetDelay(Sounds sound, int seconds);
    string GetAssignedFile(Sounds sound);
    IReadOnlyList<string> GetAvailableSoundFiles();
    void SetSoundFile(Sounds sound, string? filename);
    Task SaveUploadedFileAsync(string filename, Stream data);
    void DeleteSoundFile(string filename);
}
