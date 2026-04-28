using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using Xunit;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeTimerTests
{
    [Fact]
    public void NoWinner_AlwaysReturnsNone()
    {
        var mockEts = new Mock<IExternalTriggerService>();
        var mockGss = new Mock<IGameStateService>();
        var timer = new GameModeTimer(mockEts.Object, mockGss.Object);
        Assert.Equal(TeamColor.NONE, timer.GetWinner);
    }

    [Fact]
    public void GetDisplayPoints_AlwaysReturnsMaxTickets()
    {
        var mockEts = new Mock<IExternalTriggerService>();
        var mockGss = new Mock<IGameStateService>();
        var timer = new GameModeTimer(mockEts.Object, mockGss.Object) { MaxTickets = 1000 };
        Assert.Equal(1000, timer.GetDisplayPoints(TeamColor.BLUE));
        Assert.Equal(1000, timer.GetDisplayPoints(TeamColor.RED));
    }

    [Fact]
    public void ShowRespawnButton_IsFalse()
    {
        var mockEts = new Mock<IExternalTriggerService>();
        var mockGss = new Mock<IGameStateService>();
        var timer = new GameModeTimer(mockEts.Object, mockGss.Object);
        Assert.False(timer.ShowRespawnButton);
    }
}
