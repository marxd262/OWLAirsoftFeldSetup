using OWLServer.Models;

namespace OWLServer.Services.Interfaces;

public interface IExternalTriggerService
{
    Action StateHasChangedAction { get; set; }
    event EventHandler<KlickerEventArgs> KlickerPressedAction;
    event EventHandler<TowerEventArgs> TowerPressedAction;
    void InvokeKlickerPressed(TeamColor color);
    void InvokeTowerPressed(string towerId, TeamColor color);
}
