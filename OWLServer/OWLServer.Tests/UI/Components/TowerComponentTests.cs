using Bunit;
using OWLServer.Components.Components;
using OWLServer.Models;
using OWLServer.Tests.Helpers;
using Xunit;

namespace OWLServer.Tests.UI.Components;

public class TowerComponentTests : TestContext
{
    [Fact]
    public void RendersTowerDisplayLetter()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            DisplayLetter = "A",
            CurrentColor = TeamColor.NONE
        };

        var cut = RenderComponent<TowerComponent>(p => p
            .Add(c => c.Tower, tower)
            .Add(c => c.Smaller, false));

        Assert.Contains("A", cut.Markup);
    }
}
