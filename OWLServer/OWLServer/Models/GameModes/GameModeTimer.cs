using OWLServer.Services.Interfaces;

namespace OWLServer.Models.GameModes;

public class GameModeTimer : GameModeBase
{
    public override string Name { get; set; } = "Zeitspiel";
    public override GameMode GameMode => GameMode.Timer;

    public GameModeTimer(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
        : base(externalTriggerService, gameStateService)
    {
        MaxTickets = 1000;
    }

    public override TeamColor GetWinner => TeamColor.NONE;
    public override int GetDisplayPoints(TeamColor color) => MaxTickets;
    public override void FillTeams(List<TeamBase> teams) { }
    protected override bool Tick() => false;
}
