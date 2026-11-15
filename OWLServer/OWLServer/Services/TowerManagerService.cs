using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services;

public class TowerManagerService : ITowerManagerService
{
    private IExternalTriggerService ExternalTriggerService { get; }
    private readonly ITowerHttpClientFactory _httpFactory;
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;

    public Dictionary<string, Tower> Towers { get; } = new();
    private readonly object _registerLock = new(); // towers boot together; two new ones must not get the same letter

    public TowerManagerService(IExternalTriggerService externalTriggerService, ITowerHttpClientFactory httpFactory,
                               IDbContextFactory<DatabaseContext> dbFactory)
    {
        _httpFactory = httpFactory;
        _dbFactory = dbFactory;
        ExternalTriggerService = externalTriggerService;
    }

    public void RegisterTower(string id, string ip)
    {
        lock (_registerLock)
        {
            if (Towers.TryGetValue(id, out var known))
            {
                // towers re-register regularly; after a reboot the router may have given it a new IP
                if (known.IP != ip) known.SetAddress(ip, _httpFactory.Create(ip));
                return;
            }

            Towers.Add(id, new Tower(id, ip, _httpFactory.Create(ip)) { CurrentColor = TeamColor.NONE, DisplayLetter = LetterFor(id, ip) });
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }

    // A tower keeps its letter across restarts and boot orders: history and stats group by letter.
    private string LetterFor(string mac, string ip)
    {
        using var db = _dbFactory.CreateDbContext();
        var saved = db.Towers.FirstOrDefault(t => t.MacAddress == mac);
        if (saved != null) return saved.DisplayLetter;

        var maxLetter = db.Towers.Max(t => (string?)t.DisplayLetter);
        var letter = maxLetter == null ? "A" : ((char)(maxLetter[0] + 1)).ToString();
        db.Towers.Add(new Tower { MacAddress = mac, IP = ip, Name = string.Empty, DisplayLetter = letter });
        db.SaveChanges();
        return letter;
    }

    public void TowerChangeColor(string towerId, TeamColor newColor)
    {
        if (Towers.ContainsKey(towerId))
        {
            Towers[towerId].SetTowerColor(newColor);
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }

    public double GetPoints(TeamColor teamColor)
    {
        return Towers.Values.Where(t => t.CurrentColor == teamColor).Sum(t => t.Multiplier);
    }

    public void HandleTowerButtonPressed(string towerId, TeamColor color)
    {
        if (!Towers.ContainsKey(towerId)) return;
        if (Towers[towerId].IsPressed) return;
        if (Towers[towerId].IsLocked) return;
        if (Towers[towerId].CurrentColor == color) return;

        Towers[towerId].LastPressed = DateTime.Now;
        Towers[towerId].PressedByColor = color;
        Towers[towerId].IsPressed = true;
    }

    public void HandleTowerButtonReleased(string towerId)
    {
        if (!Towers.ContainsKey(towerId)) return;

        Towers[towerId].IsPressed = false;
        Towers[towerId].LastPressed = null;
        Towers[towerId].PressedByColor = TeamColor.NONE;
    }

    public void SetColorForAllTowers(TeamColor teamColor)
    {
        foreach (var tower in Towers)
        {
            tower.Value.SetTowerColor(teamColor);
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }

    public Task PingAll()
    {
        foreach (var tower in Towers)
        {
            tower.Value.PingTower();
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
        return Task.CompletedTask;
    }
    public Task OffTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.SetTowerColor(TeamColor.OFF);
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
        return Task.CompletedTask;
    }
    public Task ResetTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.Reset();
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
        return Task.CompletedTask;
    }
}
