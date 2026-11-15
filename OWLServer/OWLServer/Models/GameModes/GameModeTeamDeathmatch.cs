using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeTeamDeathmatch : GameModeBase
{
    public override string Name { get; set; } = "Teamdeathmatch";
    public override GameMode GameMode => GameMode.TeamDeathMatch;
    public override bool ShowRespawnButton => true;
    protected override TimeSpan TickInterval => TimeSpan.FromMilliseconds(500);

    public bool IsTicket = true;

    public Dictionary<TeamColor, int> TeamDeaths = new();

    public GameModeTeamDeathmatch(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
        : base(externalTriggerService, gameStateService)
    {
        ExternalTriggerService.KlickerPressedAction += ClickerPressed;
    }

    private void ClickerPressed(object? sender, KlickerEventArgs args)
    {
        if (StartTime != null && IsRunning)
        {
            TeamDeaths[args.TeamColor] += 1;
            try { ExternalTriggerService.StateHasChangedAction?.Invoke(); }
            catch { }
        }
    }

    public override void FillTeams(List<TeamBase> teams)
    {
        foreach (var team in teams)
            TeamDeaths[team.TeamColor] = 0;
    }

    public override int GetDisplayPoints(TeamColor color)
    {
        int points = IsTicket ? MaxTickets - TeamDeaths[color] : TeamDeaths[color];
        return points < 0 ? 0 : points;
    }

    protected override bool Tick() => TeamDeaths.Any(e => e.Value >= MaxTickets);

    public override void ResetGame()
    {
        base.ResetGame();
        foreach (var key in TeamDeaths.Keys.ToList())
            TeamDeaths[key] = 0;
    }

    public override TeamColor GetWinner =>
        TeamDeaths.Values.Distinct().Count() == 1
            ? TeamColor.NONE
            : TeamDeaths.First(e => e.Value == TeamDeaths.Values.Min()).Key;

    public override void Dispose()
    {
        ExternalTriggerService.KlickerPressedAction -= ClickerPressed;
        base.Dispose();
    }
}
