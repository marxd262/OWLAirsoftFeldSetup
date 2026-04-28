using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using Xunit;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeTeamDeathmatchTests
{
    private readonly Mock<IExternalTriggerService> _mockEts;
    private readonly Mock<IGameStateService> _mockGss;
    private readonly GameModeTeamDeathmatch _tdm;

    public GameModeTeamDeathmatchTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.ExternalTriggerService).Returns(_mockEts.Object);
        _mockGss.Setup(g => g.TowerManagerService).Returns(new Mock<ITowerManagerService>().Object);
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });

        _tdm = new GameModeTeamDeathmatch(_mockEts.Object, _mockGss.Object);
        _tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
    }

    [Fact]
    public void ShowRespawnButton_IsTrue()
    {
        Assert.True(_tdm.ShowRespawnButton);
    }

    [Fact]
    public void FillTeams_InitializesDeathsToZero()
    {
        Assert.Equal(0, _tdm.TeamDeaths[TeamColor.BLUE]);
        Assert.Equal(0, _tdm.TeamDeaths[TeamColor.RED]);
    }

    [Fact]
    public void GetWinner_TieReturnsNone()
    {
        Assert.Equal(TeamColor.NONE, _tdm.GetWinner);
    }
}
