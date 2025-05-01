using Microsoft.AspNetCore.Components;
using OWLServer.Services;

namespace OWLServer.Components.ConfigComponents;

public partial class TowerConfig : ComponentBase
{
    [Inject]
    public GameStateService GameStateService { get; set; }
}