using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OWLServer.Components.TeamPanel;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using Xunit;

namespace OWLServer.Tests.UI.Components;

public class MatchScoreBarTests : TestContext
{
    private readonly Mock<IGameStateService> _mockGss;
    private readonly Mock<IExternalTriggerService> _mockEts;

    public MatchScoreBarTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(new Dictionary<string, Tower>());

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);
        _mockGss.Setup(g => g.ExternalTriggerService).Returns(_mockEts.Object);

        Services.AddSingleton(_mockGss.Object);
        Services.AddSingleton(_mockEts.Object);
    }

    [Fact]
    public void RendersPauseWhenNoGame()
    {
        _mockGss.Setup(g => g.CurrentGame).Returns((IGameModeBase?)null);

        var cut = RenderComponent<MatchScoreBar>(p => p.Add(c => c.TeamOnSide, TeamColor.NONE));

        Assert.Contains("PAUSE", cut.Markup);
    }
}
