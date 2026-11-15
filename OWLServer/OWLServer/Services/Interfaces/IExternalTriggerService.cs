using OWLServer.Models;

namespace OWLServer.Services.Interfaces;

public interface IExternalTriggerService
{
    Action StateHasChangedAction { get; set; }
    event EventHandler<KlickerEventArgs> KlickerPressedAction;
    void InvokeKlickerPressed(TeamColor color);
}
