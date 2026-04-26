using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.Wave;
using OWLServer.Models;

namespace OWLServer.Services;

public class AudioService
{
    private static readonly string SoundsDir = "./wwwroot/Sounds";
    private static readonly string ConfigPath = "./wwwroot/Sounds/soundconfig.json";

    private readonly Dictionary<Sounds, string> _assignments = new();
    private readonly object _configLock = new();
    private readonly object _playbackLock = new();
    private WaveOutEvent? _currentWaveOut;
    private Process? _currentSoxProcess;

    public AudioService()
    {
        LoadConfig();
    }

    private void LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            _assignments[Sounds.Start] = "GameStart.mp3";
            _assignments[Sounds.Stop] = "GameOver.mp3";
            _assignments[Sounds.Countdown] = "";
            _assignments[Sounds.Freeze] = "";
            SaveConfig();
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (raw != null)
            {
                foreach (var kvp in raw)
                {
                    if (Enum.TryParse<Sounds>(kvp.Key, out var sound))
                        _assignments[sound] = kvp.Value ?? "";
                }
            }
        }
        catch
        {
            // Corrupt config — fall back to defaults
            _assignments[Sounds.Start] = "GameStart.mp3";
            _assignments[Sounds.Stop] = "GameOver.mp3";
            _assignments[Sounds.Countdown] = "";
            _assignments[Sounds.Freeze] = "";
        }

        // Fill missing enum values
        foreach (Sounds s in Enum.GetValues<Sounds>())
            _assignments.TryAdd(s, "");
    }

    private void SaveConfig()
    {
        var dict = _assignments.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value ?? "");
        var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });

        if (!Directory.Exists(SoundsDir))
            Directory.CreateDirectory(SoundsDir);

        File.WriteAllText(ConfigPath, json);
    }

    private string GetFile(Sounds sound)
    {
        lock (_configLock)
        {
            if (_assignments.TryGetValue(sound, out var fn) && !string.IsNullOrWhiteSpace(fn))
                return Path.Combine(SoundsDir, fn);
            return "";
        }
    }

    public void PlaySound(Sounds sound)
    {
        string file = GetFile(sound);
        if (string.IsNullOrEmpty(file)) return;

        StopSound();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Task.Run(() => PlaySoundWindows(file));
        else
            Task.Run(() => PlaySoundLinux(file));
    }

    public void StopSound()
    {
        lock (_playbackLock)
        {
            _currentWaveOut?.Stop();
            _currentWaveOut = null;

            try { if (_currentSoxProcess?.HasExited == false) _currentSoxProcess.Kill(); } catch { }
            _currentSoxProcess = null;
        }
    }

    private void PlaySoundWindows(string file)
    {
        WaveOutEvent? waveOut = null;
        try
        {
            using var reader = new AudioFileReader(file);
            waveOut = new WaveOutEvent();
            lock (_playbackLock) { _currentWaveOut = waveOut; }
            waveOut.Init(reader);
            waveOut.Play();
            while (waveOut.PlaybackState == PlaybackState.Playing)
                Thread.Sleep(50);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioService] Windows playback error: {ex.Message}");
        }
        finally
        {
            lock (_playbackLock) { if (_currentWaveOut == waveOut) _currentWaveOut = null; }
            waveOut?.Dispose();
        }
    }

    private void PlaySoundLinux(string file)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"play -q {file}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return;

        lock (_playbackLock) { _currentSoxProcess = process; }
        process.WaitForExit();
        lock (_playbackLock) { if (_currentSoxProcess == process) _currentSoxProcess = null; }
    }

    public string GetAssignedFile(Sounds sound)
    {
        lock (_configLock)
        {
            return _assignments.TryGetValue(sound, out var f) ? f ?? "" : "";
        }
    }

    public IReadOnlyList<string> GetAvailableSoundFiles()
    {
        if (!Directory.Exists(SoundsDir))
            return Array.Empty<string>();

        return Directory.GetFiles(SoundsDir, "*.*")
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrEmpty(f) &&
                        !f!.Equals("soundconfig.json", StringComparison.OrdinalIgnoreCase) &&
                        (f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(f => f)
            .ToList()!;
    }

    public void SetSoundFile(Sounds sound, string? filename)
    {
        var safe = string.IsNullOrWhiteSpace(filename) ? "" : Path.GetFileName(filename.Trim());

        if (!string.IsNullOrEmpty(safe))
        {
            var ext = Path.GetExtension(safe).ToLowerInvariant();
            if (ext != ".mp3" && ext != ".wav" && ext != ".ogg")
                throw new ArgumentException("Ungültiges Dateiformat.");

            if (!File.Exists(Path.Combine(SoundsDir, safe)))
                throw new FileNotFoundException($"Datei nicht gefunden: {safe}");
        }

        lock (_configLock)
        {
            _assignments[sound] = safe;
            SaveConfig();
        }
    }

    public async Task SaveUploadedFileAsync(string filename, Stream data)
    {
        var safe = Path.GetFileName(filename.Trim());
        var ext = Path.GetExtension(safe).ToLowerInvariant();

        if (ext != ".mp3" && ext != ".wav" && ext != ".ogg")
            throw new ArgumentException("Nur .mp3, .wav und .ogg Dateien sind erlaubt.");

        if (safe.Contains('/') || safe.Contains('\\'))
            throw new ArgumentException("Ungültiger Dateiname.");

        if (!Directory.Exists(SoundsDir))
            Directory.CreateDirectory(SoundsDir);

        var dest = Path.Combine(SoundsDir, safe);
        await using var fs = File.Create(dest);
        await data.CopyToAsync(fs);
    }

    public void DeleteSoundFile(string filename)
    {
        var safe = Path.GetFileName(filename.Trim());
        var path = Path.Combine(SoundsDir, safe);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Datei nicht gefunden: {safe}");

        lock (_configLock)
        {
            foreach (var key in _assignments.Keys.ToList())
            {
                if (_assignments[key].Equals(safe, StringComparison.OrdinalIgnoreCase))
                    _assignments[key] = "";
            }
            SaveConfig();
        }

        File.Delete(path);
    }

    public string RunCommandWithBash(string command)
    {
        var arg = "-c \"" + command + "\"";

        Console.WriteLine("arg: " + arg);

        var psi = new ProcessStartInfo();
        psi.FileName = "/bin/bash";
        psi.Arguments = arg;
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        using var process = Process.Start(psi);

        if (process == null) return "";

        process?.WaitForExit();

        var output = process?.StandardOutput.ReadToEnd();

        return output ?? "output";
    }
}
