// OWLServer/Models/GameModes/GameModeChainBreak.cs
using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeChainBreak : GameModeBase
{
    public override string Name { get; set; } = "ChainBreak";
    public override GameMode GameMode => GameMode.ChainBreak;
    public bool IsTicket { get; set; } = true;
    public int PointDistributionFrequencyInSeconds { get; set; } = 5;
    public bool NeutralAtThresholdEnabled { get; set; } = true;
    public int CaptureNeutralThresholdPercent { get; set; } = 50;

    // ChainBreak-specific config
    public double ChainFactor { get; set; } = 1.0;
    public ChainLayout? ActiveChainLayout { get; set; }

    // double, so fractional chain points add up across ticks instead of being rounded away each tick
    public Dictionary<TeamColor, double> TeamPoints = new();
    private DateTime _lastPointDistributed;

    public ChainGraphEngine? Engine { get; private set; }

    public GameModeChainBreak(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
        : base(externalTriggerService, gameStateService)
    {
    }

    public override void FillTeams(List<TeamBase> teams)
    {
        foreach (var team in teams)
            TeamPoints[team.TeamColor] = 0;
    }

    public override int GetDisplayPoints(TeamColor color)
    {
        int points = (int)TeamPoints[color];
        if (IsTicket)
        {
            if (color == TeamColor.BLUE) points = MaxTickets - (int)TeamPoints[TeamColor.RED];
            if (color == TeamColor.RED)  points = MaxTickets - (int)TeamPoints[TeamColor.BLUE];
        }
        return points < 0 ? 0 : points;
    }

    public override TeamColor GetWinner =>
        TeamPoints.Values.Distinct().Count() == 1
            ? TeamColor.NONE
            : TeamPoints.First(e => e.Value == TeamPoints.Values.Max()).Key;

    public override void RunGame()
    {
        Engine = new ChainGraphEngine(ActiveChainLayout, GameStateService.TowerManagerService.Towers);
        Engine.NeutralAtThresholdEnabled = NeutralAtThresholdEnabled;
        Engine.CaptureNeutralThresholdPercent = CaptureNeutralThresholdPercent;
        InitializeTowerStates();
        _lastPointDistributed = DateTime.Now;
        base.RunGame();
    }

    private void InitializeTowerStates()
    {
        if (Engine == null) return;
        foreach (var tower in GameStateService.TowerManagerService.Towers.Values)
        {
            bool inChain = Engine.EntryPoints.Contains(tower.MacAddress)
                           || Engine.Predecessors.ContainsKey(tower.MacAddress)
                           || Engine.Successors.ContainsKey(tower.MacAddress);
            if (inChain && !Engine.EntryPoints.Contains(tower.MacAddress))
                tower.SetTowerColor(TeamColor.LOCKED);
            else
                tower.SetTowerColor(TeamColor.NONE);
        }
    }

    protected override bool Tick()
    {
        Engine?.ProcessTick();

        if (_lastPointDistributed.AddSeconds(PointDistributionFrequencyInSeconds) <= DateTime.Now)
        {
            if (Engine != null)
                foreach (var teamColor in TeamPoints.Keys)
                    TeamPoints[teamColor] += Engine.GetChainPoints(teamColor, ChainFactor);
            _lastPointDistributed = DateTime.Now;
        }

        return TeamPoints.Any(e => e.Value >= MaxTickets);
    }

    public override void ResetGame()
    {
        base.ResetGame();
        foreach (var key in TeamPoints.Keys.ToList())
            TeamPoints[key] = 0;
        Engine = null;
    }

    public (string color, bool arrowA, bool arrowB, bool animated, bool bothWays) GetLinkVisualState(ChainLink link)
    {
        return Engine?.GetLinkVisualState(link)
               ?? ("#BBBBBB", false, false, false, false);
    }
}
