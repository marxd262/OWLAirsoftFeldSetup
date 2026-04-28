namespace OWLServer.Services.Interfaces;

public interface IMapService
{
    string GetCurrentMapUrl();
    string GetCurrentMapFile();
    IReadOnlyList<string> GetAvailableMapFiles();
    void SetCurrentMapFile(string? filename);
    Task SaveUploadedMapFileAsync(string filename, Stream data);
    void DeleteMapFile(string filename);
}
