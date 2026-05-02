using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeConquest : IGameModeBase, IDisposable
{
    private IExternalTriggerService ExternalTriggerService { get; set; }
    private IGameStateService GameStateService { get; set; }
    public string Name { get; set; } = "Conquest";
    public GameMode GameMode => GameMode.Conquest;
    public int GameDurationInMinutes { get; set; } = 20;
    public int MaxTickets { get; set; } = 15;
    public bool ShowRespawnButton => false;
    public bool IsPaused { get; set; }
    public TimeSpan PausedDuration { get; set; }
    public DateTime? PauseStartedAt { get; set; }

    // Zwei Mögliche Spielmode sind implementiert. Ticket basiert (zählt runter) oder Punkte basiert (Zählt hoch)
    public bool IsTicket = true;
    public int PointDistributionFrequencyInSeconds { get; set; } = 5;
    public bool NeutralAtThresholdEnabled { get; set; } = true;
    public int CaptureNeutralThresholdPercent { get; set; } = 50;
    public bool IsRunning { get; set; } = false;
    public bool IsFinished { get; set; } = false;
    public DateTime? StartTime { get; set; }

    private CancellationTokenSource _abort = new();

    public TowerControlLayout? ActiveControlLayout { get; set; }

    // Runtime control graph — built at RunGame()
    private Dictionary<string, List<string>> _controlledChildren = new();
    private Dictionary<string, string> _controllerByChild = new();

    public bool IsControllerTower(string macAddress) => _controlledChildren.ContainsKey(macAddress);

    public Dictionary<TeamColor, int> TeamPoints = new();

    public GameModeConquest(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
    {
        ExternalTriggerService = externalTriggerService;
        GameStateService = gameStateService;
    }

    [NotMapped]
    public TimeSpan? GetTimer 
    {
        get
        {
            if (StartTime == null)
                return new TimeSpan(0, GameDurationInMinutes, 0);
            else if (IsFinished)
                return new TimeSpan(0, 0, 0);
            else if (IsPaused && PauseStartedAt != null)
                return StartTime.Value.AddMinutes(GameDurationInMinutes) - PauseStartedAt.Value + PausedDuration;
            else
                return StartTime.Value.AddMinutes(GameDurationInMinutes) - DateTime.Now + PausedDuration;
        }
    }

    public void FillTeams(List<TeamBase> teams)
    {
        foreach (var teamColor in teams)    
        {
            TeamPoints[teamColor.TeamColor] = 0;
        }
    }


    /// <summary>
    /// Returns the number of Points or Tickets based on <see cref="IsTicket"/>
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public int GetDisplayPoints(TeamColor color)
    {
        int points = 0;

        if (IsTicket)
        {
            if(color == TeamColor.BLUE)
                points = MaxTickets - TeamPoints[TeamColor.RED];
            else if (color == TeamColor.RED)
                points = MaxTickets - TeamPoints[TeamColor.BLUE];
        }
        else
        {
            points = TeamPoints[color];
        }
        
        return points < 0  ? 0 : points;
    }
    
    public void RunGame()
    {
        BuildControlMaps();
        InitializeControlTowerStates();
        StartTime = DateTime.Now;
        IsRunning = true;
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
        Task.Run(Runner, _abort.Token);
    }

    private async void Runner()
    {
        DateTime lastPointDistributed = DateTime.Now;
        while (true)
        {
            await Task.Delay(200);

            if (_abort.IsCancellationRequested)
            {
                EndGame();
                break;
            }

            if (IsPaused) continue;

            if (StartTime?.AddMinutes(GameDurationInMinutes) + PausedDuration <= DateTime.Now)
            {
                EndGame();
                break;
            }

            ProcessConquestStateMachine();

            if (lastPointDistributed.AddSeconds(PointDistributionFrequencyInSeconds) <= DateTime.Now)
            {
                DistributePoints();
                lastPointDistributed = DateTime.Now;
            }

            if (TeamPoints.Any(e => e.Value >= MaxTickets))
            {
                EndGame();
                break;
            }
        }
    }

    private void DistributePoints()
    {
        foreach (var teamColor in TeamPoints.Keys)
        {
            TeamPoints[teamColor] += GameStateService.TowerManagerService.GetPoints(teamColor);
        }
    }

    private void BuildControlMaps()
    {
        _controlledChildren = new Dictionary<string, List<string>>();
        _controllerByChild = new Dictionary<string, string>();

        if (ActiveControlLayout == null) return;

        foreach (var link in ActiveControlLayout.Links)
        {
            if (!_controlledChildren.ContainsKey(link.ControllerTowerMacAddress))
                _controlledChildren[link.ControllerTowerMacAddress] = new List<string>();
            _controlledChildren[link.ControllerTowerMacAddress].Add(link.ControlledTowerMacAddress);
            _controllerByChild[link.ControlledTowerMacAddress] = link.ControllerTowerMacAddress;
        }
    }

    private void InitializeControlTowerStates()
    {
        var controlledMacs = ActiveControlLayout?.Links
            .Select(l => l.ControlledTowerMacAddress)
            .ToHashSet() ?? new HashSet<string>();

        foreach (var tower in GameStateService.TowerManagerService.Towers.Values)
        {
            if (controlledMacs.Contains(tower.MacAddress))
                tower.SetTowerColor(TeamColor.LOCKED);
            else
                tower.SetTowerColor(TeamColor.NONE);
        }
    }

    private void ProcessConquestStateMachine()
    {
        var towers = GameStateService.TowerManagerService.Towers;

        // Lock: controller reset timer expired
        foreach (var tower in towers.Values.Where(t =>
            _controlledChildren.ContainsKey(t.MacAddress)
            && t.CurrentColor != TeamColor.NONE
            && t.CapturedAt != null
            && t.CapturedAt?.AddSeconds(t.ResetsAfterInSeconds) < DateTime.Now).ToList())
        {
            tower.SetTowerColor(TeamColor.NONE);

            foreach (string childMac in _controlledChildren[tower.MacAddress])
            {
                if (towers.TryGetValue(childMac, out var child) && child.CurrentColor == TeamColor.NONE)
                    child.SetTowerColor(TeamColor.LOCKED);
            }

            tower.CapturedAt = null;
        }

        // Capture in progress
        foreach (var tower in towers.Values.Where(t => t.IsPressed).ToList())
        {
            // Guard: if controlled, check controller's ownership
            if (_controllerByChild.TryGetValue(tower.MacAddress, out var controllerMac)
                && towers.TryGetValue(controllerMac, out var controllerTower)
                && controllerTower.CurrentColor != tower.PressedByColor)
            {
                tower.IsPressed = false;
                tower.LastPressed = null;
                tower.PressedByColor = TeamColor.NONE;
                tower.CaptureProgress = 0;
                continue;
            }

            if (tower.LastPressed?.AddSeconds(tower.TimeToCaptureInSeconds) < DateTime.Now)
            {
                tower.SetTowerColor(tower.PressedByColor);
                tower.CapturedAt = DateTime.Now;
                tower.IsPressed = false;
                tower.LastPressed = null;
                tower.PressedByColor = TeamColor.NONE;
                tower.CaptureProgress = 1;

                // Unlock children
                if (_controlledChildren.TryGetValue(tower.MacAddress, out var children))
                {
                    foreach (string childMac in children)
                    {
                        if (towers.TryGetValue(childMac, out var child))
                            child.SetTowerColor(TeamColor.NONE);
                    }
                }
            }
            else
            {
                var elapsed = DateTime.Now - tower.LastPressed;
                tower.CaptureProgress = elapsed?.TotalSeconds / tower.TimeToCaptureInSeconds ?? 0;

                if (NeutralAtThresholdEnabled
                    && tower.CaptureProgress * 100 >= CaptureNeutralThresholdPercent
                    && tower.CurrentColor != TeamColor.NONE
                    && tower.CurrentColor != tower.PressedByColor)
                {
                    tower.SetTowerColor(TeamColor.NONE);
                }
            }
        }

        ExternalTriggerService.StateHasChangedAction?.Invoke();
    }

    public void EndGame()
    {
        if (IsFinished) return;
        _abort.Cancel();
        IsRunning = false;
        IsFinished = true;
        StartTime = null;
        GameStateService.HandleGameEnd();
    }

    public void ResetGame()
    {
        if (IsRunning) EndGame();
        IsFinished = false;
        StartTime = null;
        foreach (var key in TeamPoints.Keys.ToList())
            TeamPoints[key] = 0;
        _abort.Dispose();
        _abort = new CancellationTokenSource();
        IsPaused = false;
        PauseStartedAt = null;
        PausedDuration = TimeSpan.Zero;
    }

    [NotMapped]
    public TeamColor GetWinner
    {
        get
        {
            if (TeamPoints.Values.Distinct().Count() == 1)
            {
                return TeamColor.NONE;
            }

            return TeamPoints.First(e => e.Value == TeamPoints.Values.Max()).Key;
        }
    }
    
    public override string ToString()
    {
        return Name;
    }

    public void Dispose()
    {
        StartTime = null;
        _abort.Dispose();
    }
}