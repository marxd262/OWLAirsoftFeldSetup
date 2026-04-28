using OWLServer.Models;
using OWLServer.Services.Interfaces;
using Radzen;

namespace OWLServer.Services;

public class TowerManagerService : ITowerManagerService
{
    private ExternalTriggerService ExternalTriggerService { get; }

    public Dictionary<string, Tower> Towers { get; } = new();

    public TowerManagerService(ExternalTriggerService externalTriggerService)
    {
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

        Towers.Add(id, new Tower(id, ip) { CurrentColor = TeamColor.NONE, DisplayLetter=maxChar });
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }

    public void TowerChangeColor(string TowerID, TeamColor newColor)
    {
        if (Towers.ContainsKey(TowerID))
        {
            Towers[TowerID].SetTowerColor(newColor);
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }

    public int GetPoints(TeamColor TeamColor)
    {
        double points = 0;
        foreach (var tower in Towers.Values.Where(t => t.CurrentColor == TeamColor))
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

    public void HandleTowerButtonPressed(string towerID, TeamColor color)
    {
        if (!Towers.ContainsKey(towerID)) return;
        if (Towers[towerID].IsPressed) return;
        if (Towers[towerID].IsLocked) return;

        Towers[towerID].LastPressed = DateTime.Now;
        Towers[towerID].PressedByColor = color;
        Towers[towerID].IsPressed = true;
    }

    public void HandleTowerButtonReleased(string towerID)
    {
        if (!Towers.ContainsKey(towerID)) return;

        Towers[towerID].IsPressed = false;
        Towers[towerID].LastPressed = null;
        Towers[towerID].PressedByColor = TeamColor.NONE;
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

    public async Task PingAll()
    {
        foreach (var tower in Towers)
        {
            tower.Value.PingTower();
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }
    public async Task OffTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.SetTowerColor(TeamColor.OFF);
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }
    public async Task ResetTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.Reset();
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }
}