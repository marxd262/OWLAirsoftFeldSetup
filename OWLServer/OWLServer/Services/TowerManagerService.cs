using System.Drawing;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using Radzen;

namespace OWLServer.Services;

public class TowerManagerService
{
    private ExternalTriggerService ExternalTriggerService { get; }
    private GameStateService GameStateService { get; }
    private CancellationTokenSource abort = new();
    public bool IsRunning { get; private set; }

    public Dictionary<string, Tower> Towers { get; } = new();

    public TowerManagerService(ExternalTriggerService externalTriggerService, GameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;

        ExternalTriggerService.TowerPressedAction += HandleTowerClicked;
    }

    public void RunTowerManager()
    {
        IsRunning = true;
        Task.Run(Runner, abort.Token);
    }

    private void Runner()
    {
        while (true)
        {
            Thread.Sleep(200);

            if (abort.IsCancellationRequested)
            {
                break;
            }

            if (GameStateService.CurrentGame is GameModeConquest gameMode && gameMode.IsRunning)
            {
                
                foreach (var tower in Towers.Values.Where(t => 
                             t.IsForControlling 
                             && t.CurrentColor != TeamColor.NONE 
                             && t.CapturedAt != null 
                             && t.CapturedAt?.AddSeconds(t.ResetsAfterInSeconds) < DateTime.Now))
                {
                    tower.CurrentColor = TeamColor.NONE;
                    
                    foreach (string towerid in tower.ControllsTowerID)
                    {
                        if(Towers[towerid].CurrentColor == TeamColor.NONE)
                            Towers[towerid].CurrentColor = TeamColor.LOCKED;
                    }

                    tower.CapturedAt = null;
                }
                
                foreach (var tower in Towers.Values.Where(t => t.IsPressed))
                {
                    if (tower.IsControlled && Towers[tower.IsControlledByID].CurrentColor != tower.PressedByColor)
                    {
                        tower.IsPressed = false;
                        break;
                    }
                    
                    if (tower.LastPressed?.AddSeconds(tower.TimeToCaptureInSeconds) < DateTime.Now)
                    {
                        tower.SetTowerColor(tower.PressedByColor); 
                        tower.CapturedAt = DateTime.Now;
                        tower.IsPressed = false;
                        tower.LastPressed = null;
                        tower.PressedByColor = TeamColor.NONE;
                        tower.CaptureProgress = 1;

                        if (tower.IsForControlling)
                        {
                            foreach (string towerid in tower.ControllsTowerID)
                            {
                                Towers[towerid].CurrentColor = TeamColor.NONE;
                            }
                        }
                    }
                    else
                    {
                        var timeSincePressed = DateTime.Now - tower.LastPressed;
                        var s = timeSincePressed?.Seconds;
                        double? progress = s / tower.TimeToCaptureInSeconds;
                        if (progress != null)
                            tower.CaptureProgress = (double)progress;
                    }
                }
                

                ExternalTriggerService.StateHasChangedAction?.Invoke();
            }
            else
            {
                Thread.Sleep(5000);
            }
        }
    }

    public void RegisterTower(string id, string ip)
    {
        if (Towers.ContainsKey(id)) return;
        Towers.Add(id, new Tower(id, ip) { CurrentColor = TeamColor.NONE });
        ExternalTriggerService.StateHasChangedAction.Invoke();
    }

    public void TowerChangeColor(string TowerID, TeamColor newColor)
    {
        if (Towers.ContainsKey(TowerID))
        {
            Towers[TowerID].SetTowerColor(newColor);
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

    public async void PingAll()
    {
        foreach (var tower in Towers)
        {
            tower.Value.PingTower();
            ExternalTriggerService.StateHasChangedAction?.Invoke();
        }
    }
    public async void OffTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.SetTowerColor(TeamColor.OFF);
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }
    public async void ResetTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.Reset();
        }
        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }
}