using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services;

public class TowerManagerService : ITowerManagerService
{
    private IExternalTriggerService ExternalTriggerService { get; }
    private readonly ITowerHttpClientFactory _httpFactory;

    public Dictionary<string, Tower> Towers { get; } = new();

    public TowerManagerService(IExternalTriggerService externalTriggerService, ITowerHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
        ExternalTriggerService = externalTriggerService;

        ExternalTriggerService.TowerPressedAction += HandleTowerClicked;
    }

    public void RegisterTower(string id, string ip)
    {
        if (Towers.ContainsKey(id)) return;

        var maxChar = Towers.Max(e => e.Value.DisplayLetter);
        if (maxChar != null) {
            maxChar = ((char)(maxChar[0] + 1)).ToString();
        }
        else
        {
            maxChar = "A";
        }

        Towers.Add(id, new Tower(id, ip, _httpFactory.Create(ip)) { CurrentColor = TeamColor.NONE, DisplayLetter = maxChar });
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }

    public void TowerChangeColor(string towerId, TeamColor newColor)
    {
        if (Towers.ContainsKey(towerId))
        {
            Towers[towerId].SetTowerColor(newColor);
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }

    public int GetPoints(TeamColor teamColor)
    {
        double points = 0;
        foreach (var tower in Towers.Values.Where(t => t.CurrentColor == teamColor))
        {
            points += tower.Multiplier;
        }

        return (int)double.Round(points);
    }

    private void HandleTowerClicked(object? sender, TowerEventArgs args)
    {
        if (!Towers.ContainsKey(args.TowerId)) return;

        TowerChangeColor(args.TowerId, args.TeamColor);
    }

    public void HandleTowerButtonPressed(string towerId, TeamColor color)
    {
        if (!Towers.ContainsKey(towerId)) return;
        if (Towers[towerId].IsPressed) return;
        if (Towers[towerId].IsLocked) return;

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

    public void SetAllTowerToStartColor()
    {
        foreach (var tower in Towers)
        {
            tower.Value.SetToStartColor();
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