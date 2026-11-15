using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeConquest : GameModeBase
{
    public override string Name { get; set; } = "Conquest";
    public override GameMode GameMode => GameMode.Conquest;

    // Zwei Mögliche Spielmode sind implementiert. Ticket basiert (zählt runter) oder Punkte basiert (Zählt hoch)
    public bool IsTicket = true;
    public int PointDistributionFrequencyInSeconds { get; set; } = 5;
    public bool NeutralAtThresholdEnabled { get; set; } = true;
    public int CaptureNeutralThresholdPercent { get; set; } = 50;

    public TowerControlLayout? ActiveControlLayout { get; set; }

    // Runtime control graph — built at RunGame()
    private Dictionary<string, List<string>> _controlledChildren = new();
    private Dictionary<string, string> _controllerByChild = new();
    private DateTime _lastPointDistributed;

    public bool IsControllerTower(string macAddress) => _controlledChildren.ContainsKey(macAddress);

    // double, so fractional multipliers add up across ticks instead of being rounded away each tick
    public Dictionary<TeamColor, double> TeamPoints = new();

    public GameModeConquest(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
        : base(externalTriggerService, gameStateService)
    {
    }

    public override void FillTeams(List<TeamBase> teams)
    {
        foreach (var team in teams)
            TeamPoints[team.TeamColor] = 0;
    }

    /// <summary>
    /// Returns the number of Points or Tickets based on <see cref="IsTicket"/>
    /// </summary>
    public override int GetDisplayPoints(TeamColor color)
    {
        int points = 0;

        if (IsTicket)
        {
            if (color == TeamColor.BLUE)
                points = MaxTickets - (int)TeamPoints[TeamColor.RED];
            else if (color == TeamColor.RED)
                points = MaxTickets - (int)TeamPoints[TeamColor.BLUE];
        }
        else
        {
            points = (int)TeamPoints[color];
        }

        return points < 0 ? 0 : points;
    }

    public override void RunGame()
    {
        BuildControlMaps();
        InitializeControlTowerStates();
        _lastPointDistributed = DateTime.Now;
        base.RunGame();
    }

    protected override bool Tick()
    {
        ProcessConquestStateMachine();

        if (_lastPointDistributed.AddSeconds(PointDistributionFrequencyInSeconds) <= DateTime.Now)
        {
            foreach (var teamColor in TeamPoints.Keys)
                TeamPoints[teamColor] += GameStateService.TowerManagerService.GetPoints(teamColor);
            _lastPointDistributed = DateTime.Now;
        }

        return TeamPoints.Any(e => e.Value >= MaxTickets);
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

    internal void ProcessConquestStateMachine()
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
    }

    public override void ResetGame()
    {
        base.ResetGame();
        foreach (var key in TeamPoints.Keys.ToList())
            TeamPoints[key] = 0;
    }

    public override TeamColor GetWinner =>
        TeamPoints.Values.Distinct().Count() == 1
            ? TeamColor.NONE
            : TeamPoints.First(e => e.Value == TeamPoints.Values.Max()).Key;
}
