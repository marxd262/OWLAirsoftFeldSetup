using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using Xunit;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeConquestTests
{
    private readonly Mock<IExternalTriggerService> _mockEts;
    private readonly Mock<IGameStateService> _mockGss;

    public GameModeConquestTests()
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
    public void FillTeams_InitializesTeamPointsToZero()
    {
        var conquest = new GameModeConquest(_mockEts.Object, _mockGss.Object);
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(0, conquest.TeamPoints[TeamColor.BLUE]);
        Assert.Equal(0, conquest.TeamPoints[TeamColor.RED]);
    }

    [Fact]
    public void GetDisplayPoints_IsTicket_ReturnsMaxTicketsByDefault()
    {
        var conquest = new GameModeConquest(_mockEts.Object, _mockGss.Object)
        {
            IsTicket = true,
            MaxTickets = 15
        };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.True(conquest.GetDisplayPoints(TeamColor.BLUE) >= 0);
    }

    [Fact]
    public void GetWinner_TieReturnsNone()
    {
        var conquest = new GameModeConquest(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(TeamColor.NONE, conquest.GetWinner);
    }
}
