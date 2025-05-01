using Microsoft.AspNetCore.Components;
using OWLServer.Models.GameModes;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class GameModeConquestConfig : ComponentBase
{
    [Parameter]
    public GameModeConquest CurrentGame{get;set;}
}