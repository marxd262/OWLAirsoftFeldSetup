using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using Xunit;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeChainBreakTests
{
    private readonly Mock<IExternalTriggerService> _mockEts;
    private readonly Mock<IGameStateService> _mockGss;

    public GameModeChainBreakTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(new Dictionary<string, Tower>());

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });
        _mockGss.Setup(g => g.ExternalTriggerService).Returns(_mockEts.Object);
    }

    [Fact]
    public void FillTeams_InitializesPointsToZero()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object);
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(0, chainBreak.TeamPoints[TeamColor.BLUE]);
        Assert.Equal(0, chainBreak.TeamPoints[TeamColor.RED]);
    }

    [Fact]
    public void GetWinner_TieReturnsNone()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(TeamColor.NONE, chainBreak.GetWinner);
    }
}
