using OWLServer.Models;

namespace OWLServer.Services;

public class ExternalTriggerService
{
    public Action StateHasChangedAction = null!;

    public event EventHandler<KlickerEventArgs> KlickerPressedAction = null!;

    public void InvokeKlickerPressed(TeamColor color)
    {
        KlickerPressedAction?.Invoke(this, new KlickerEventArgs(color));
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