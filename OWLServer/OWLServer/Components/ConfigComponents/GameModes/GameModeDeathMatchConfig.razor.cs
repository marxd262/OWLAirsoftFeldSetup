using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Components.ConfigComponents.GameModes;

public partial class GameModeDeathMatchConfig : ComponentBase
{
    [Parameter]
    public GameModeTeamDeathmatch CurrentGame { get; set; }

    public void test()
    {
    }
}