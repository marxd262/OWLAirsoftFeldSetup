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

    [Fact]
    public void InitializeTowerStates_EntryPointTowers_SetToNONE()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["EP"] = new Tower { MacAddress = "EP" },
            ["T2"] = new Tower { MacAddress = "T2" }
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "EP", TowerBMacAddress = "T2", EntryAtBothEnds = false } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();

        Assert.Equal(TeamColor.NONE, towers["EP"].CurrentColor);
    }

    [Fact]
    public void InitializeTowerStates_NonEntryChainTowers_SetToLOCKED()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["EP"] = new Tower { MacAddress = "EP" },
            ["T2"] = new Tower { MacAddress = "T2" }
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "EP", TowerBMacAddress = "T2", EntryAtBothEnds = false } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();

        Assert.Equal(TeamColor.LOCKED, towers["T2"].CurrentColor);
    }

    [Fact]
    public void InitializeTowerStates_OutsiderTower_SetToNONE()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["EP"] = new Tower { MacAddress = "EP" },
            ["T2"] = new Tower { MacAddress = "T2" },
            ["T3"] = new Tower { MacAddress = "T3" }
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "EP", TowerBMacAddress = "T2", EntryAtBothEnds = false } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();

        Assert.Equal(TeamColor.NONE, towers["T3"].CurrentColor);
    }

    [Fact]
    public void GetWinner_BlueHasMorePoints_ReturnsBlue()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.BLUE] = 10;
        chainBreak.TeamPoints[TeamColor.RED] = 5;

        Assert.Equal(TeamColor.BLUE, chainBreak.GetWinner);
    }

    [Fact]
    public void GetWinner_RedHasMorePoints_ReturnsRed()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.BLUE] = 3;
        chainBreak.TeamPoints[TeamColor.RED] = 8;

        Assert.Equal(TeamColor.RED, chainBreak.GetWinner);
    }

    [Fact]
    public void GetDisplayPoints_IsTicket_MirrorsOpponent()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = true, MaxTickets = 15 };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.RED] = 3;

        Assert.Equal(12, chainBreak.GetDisplayPoints(TeamColor.BLUE));
    }

    [Fact]
    public void GetDisplayPoints_NotTicket_ReturnsOwnPoints()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.TeamPoints[TeamColor.BLUE] = 7;

        Assert.Equal(7, chainBreak.GetDisplayPoints(TeamColor.BLUE));
    }

    [Fact]
    public void Engine_NullAfterReset()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["A"] = new Tower { MacAddress = "A" },
            ["B"] = new Tower { MacAddress = "B" }
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout { Links = new List<ChainLink>() }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();
        Assert.NotNull(chainBreak.Engine);

        chainBreak.ResetGame();
        Assert.Null(chainBreak.Engine);
    }

    [Fact]
    public void GetLinkVisualState_PassThrough_DelegatesToEngine()
    {
        var towers = new Dictionary<string, Tower>
        {
            ["A"] = new Tower { MacAddress = "A" },
            ["B"] = new Tower { MacAddress = "B" }
        };
        var mockTms = new Mock<ITowerManagerService>();
        mockTms.Setup(t => t.Towers).Returns(towers);
        _mockGss.Setup(g => g.TowerManagerService).Returns(mockTms.Object);

        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object)
        {
            ActiveChainLayout = new ChainLayout
            {
                Links = new List<ChainLink> { new() { TowerAMacAddress = "A", TowerBMacAddress = "B" } }
            }
        };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        chainBreak.RunGame();
        towers["A"].SetTowerColor(TeamColor.RED);
        towers["B"].SetTowerColor(TeamColor.RED);

        var link = chainBreak.ActiveChainLayout!.Links[0];
        var (color, _, _, _, _) = chainBreak.GetLinkVisualState(link);
        Assert.Equal("#fc1911", color);
    }
}
