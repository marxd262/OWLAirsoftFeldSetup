using System.Text.Json;

namespace OWLServer.Services;

public class MapService
{
    private static readonly string MapsDir = "./wwwroot/Maps";
    private static readonly string ConfigPath = "./wwwroot/Maps/mapconfig.json";
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private string _currentMapFile = "";
    private readonly object _configLock = new();

    public MapService()
    {
        LoadConfig();
    }

    private void LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            SaveConfig();
            return;
        }
        try
        {
            var json = File.ReadAllText(ConfigPath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            _currentMapFile = raw?.GetValueOrDefault("current", "") ?? "";
        }
        catch
        {
            _currentMapFile = "";
        }
    }

    private void SaveConfig()
    {
        if (!Directory.Exists(MapsDir))
            Directory.CreateDirectory(MapsDir);

        var json = JsonSerializer.Serialize(
            new Dictionary<string, string> { ["current"] = _currentMapFile },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    // Returns the web-accessible URL for the active map, falling back to the built-in map.png
    public string GetCurrentMapUrl()
    {
        lock (_configLock)
        {
            return !string.IsNullOrWhiteSpace(_currentMapFile)
                ? $"/Maps/{_currentMapFile}"
                : "/map.png";
        }
    }

    public string GetCurrentMapFile()
    {
        lock (_configLock) { return _currentMapFile; }
    }

    public IReadOnlyList<string> GetAvailableMapFiles()
    {
        if (!Directory.Exists(MapsDir))
            return Array.Empty<string>();

        return Directory.GetFiles(MapsDir, "*.*")
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrEmpty(f) &&
                        !f!.Equals("mapconfig.json", StringComparison.OrdinalIgnoreCase) &&
                        AllowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList()!;
    }

    public void SetCurrentMapFile(string? filename)
    {
        var safe = string.IsNullOrWhiteSpace(filename) ? "" : Path.GetFileName(filename.Trim());

        if (!string.IsNullOrEmpty(safe))
        {
            var ext = Path.GetExtension(safe).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new ArgumentException("Ungültiges Bildformat.");
            if (!File.Exists(Path.Combine(MapsDir, safe)))
                throw new FileNotFoundException($"Datei nicht gefunden: {safe}");
        }

        lock (_configLock)
        {
            _currentMapFile = safe;
            SaveConfig();
        }
    }

    public async Task SaveUploadedMapFileAsync(string filename, Stream data)
    {
        var safe = Path.GetFileName(filename.Trim());
        var ext = Path.GetExtension(safe).ToLowerInvariant();

        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException("Nur PNG, JPG und WebP Dateien sind erlaubt.");
        if (safe.Contains('/') || safe.Contains('\\'))
            throw new ArgumentException("Ungültiger Dateiname.");

        if (!Directory.Exists(MapsDir))
            Directory.CreateDirectory(MapsDir);

        var dest = Path.Combine(MapsDir, safe);
        await using var fs = File.Create(dest);
        await data.CopyToAsync(fs);
    }

    public void DeleteMapFile(string filename)
    {
        var safe = Path.GetFileName(filename.Trim());
        var path = Path.Combine(MapsDir, safe);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Datei nicht gefunden: {safe}");

        lock (_configLock)
        {
            if (_currentMapFile.Equals(safe, StringComparison.OrdinalIgnoreCase))
            {
                _currentMapFile = "";
                SaveConfig();
            }
        }

        File.Delete(path);
    }
}
