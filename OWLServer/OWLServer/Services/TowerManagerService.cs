using System.Net;
using OWLServer.Models;

namespace OWLServer.Services;

public class TowerManagerService
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
        Towers.Add(id, new Tower(id, ip){CurrentColor = TeamColor.NONE});
        ExternalTriggerService.StateHasChangedAction.Invoke();
    }

    public void TowerChangeColor(string TowerID, TeamColor newColor)
    {
        if (Towers.ContainsKey(TowerID))
        {
            Towers[TowerID].CurrentColor = newColor;
            ExternalTriggerService.StateHasChangedAction.Invoke();
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
    
    public void ResetTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.Reset();
        }
    }
    
}
