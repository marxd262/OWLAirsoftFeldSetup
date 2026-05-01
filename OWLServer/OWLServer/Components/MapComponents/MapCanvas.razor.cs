using Microsoft.AspNetCore.Components;

namespace OWLServer.Components.MapComponents;

public partial class MapCanvas : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
