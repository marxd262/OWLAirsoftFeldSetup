using OWLServer.Models;

namespace OWLServer.Services;

public class ExternalTriggerService
{
    public Action StateHasChangedAction = null!;

    public event EventHandler<KlickerEventArgs> KlickerPressedAction = null!;
    public event EventHandler<TowerEventArgs> TowerPressedAction = null!;

    public void InvokeKlickerPressed(TeamColor color)
    {
        KlickerPressedAction?.Invoke(this, new KlickerEventArgs(color));
    }

    public void InvokeTowerPressed(string towerId, TeamColor color)
    {
        TowerPressedAction?.Invoke(this, new TowerEventArgs(towerId, color));
    }
}

public class KlickerEventArgs : EventArgs
{
    public TeamColor TeamColor;

    public KlickerEventArgs(TeamColor teamColor)
    {
        TeamColor = teamColor;
    }
}

public class TowerEventArgs :EventArgs
{
    public string TowerId { get; set; }
    public TeamColor TeamColor { get; set; }

    public TowerEventArgs(string towerId, TeamColor teamColor)
    {
        TowerId = towerId;
        TeamColor = teamColor;
    }
}