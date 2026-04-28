using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OWLServer.Components.Pages;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using Xunit;

namespace OWLServer.Tests.UI.Pages;

public class GameEndOverlayTests : TestContext
{
    private readonly Mock<IGameStateService> _mockGss;
    private readonly Mock<IExternalTriggerService> _mockEts;

    public GameEndOverlayTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });

        Services.AddSingleton(_mockGss.Object);
        Services.AddSingleton(_mockEts.Object);
    }

    [Fact]
    public void ShowsWhenGameIsFinished()
    {
        var mockGame = new Mock<IGameModeBase>();
        mockGame.Setup(g => g.IsFinished).Returns(true);
        mockGame.Setup(g => g.GetWinner).Returns(TeamColor.BLUE);
        mockGame.Setup(g => g.GetDisplayPoints(It.IsAny<TeamColor>())).Returns(5);
        _mockGss.Setup(g => g.CurrentGame).Returns(mockGame.Object);

        var cut = RenderComponent<GameEndOverlay>();
        Assert.Contains("SPIEL ENDE", cut.Markup);
    }

    [Fact]
    public void HiddenWhenGameNotFinished()
    {
        var mockGame = new Mock<IGameModeBase>();
        mockGame.Setup(g => g.IsFinished).Returns(false);
        _mockGss.Setup(g => g.CurrentGame).Returns(mockGame.Object);

        var cut = RenderComponent<GameEndOverlay>();
        Assert.DoesNotContain("SPIEL ENDE", cut.Markup);
    }
}
