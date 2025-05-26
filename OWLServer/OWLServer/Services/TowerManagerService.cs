using System.Net;
using System.Security.Cryptography.X509Certificates;
using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services;

public class TowerManagerService
{
    private ExternalTriggerService ExternalTriggerService { get; }
    private GameStateService GameStateService { get; }
    private CancellationTokenSource abort = new();
    public bool IsRunning { get; private set; }

    public Dictionary<string, Tower> Towers { get; } = new();

    public int TimeToCaptureTowerInSeconds { get; set; } = 5;

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
                foreach (var tower in Towers.Values.Where(t => t.IsPressed))
                {
                    if (tower.LastPressed?.AddSeconds(TimeToCaptureTowerInSeconds) < DateTime.Now)
                    {
                        tower.SetTowerColer(tower.PressedByColor); 
                        var sinc = DateTime.Now - tower.LastPressed;
                        tower.IsPressed = false;
                        tower.LastPressed = null;
                        tower.PressedByColor = TeamColor.NONE;
                    }
                    else
                    {
                        var timeSincePressed = DateTime.Now - tower.LastPressed;
                        var s = timeSincePressed?.Seconds;
                        double? progress = s / TimeToCaptureTowerInSeconds;
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
            Towers[TowerID].SetTowerColer(newColor);
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

    public void ResetTowers()
    {
        foreach (var tower in Towers)
        {
            tower.Value.Reset();
        }
    }
}