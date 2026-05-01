using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace OWLServer.Components.MapComponents;

public partial class MapCanvas : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnMapClick { get; set; }
}
