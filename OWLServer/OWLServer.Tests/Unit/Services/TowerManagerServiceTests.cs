using Moq;
using OWLServer.Models;
using OWLServer.Services;
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Helpers;
using Xunit;

namespace OWLServer.Tests.Unit.Services;

public class TowerManagerServiceTests
{
    private readonly Mock<IExternalTriggerService> _mockEts = new();
    private readonly TowerManagerService _service;

    public TowerManagerServiceTests()
    {
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        var mockHttpClient = new MockTowerHttpClient();
        var mockFactory = new Mock<ITowerHttpClientFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<string>())).Returns(mockHttpClient);

        _service = new TowerManagerService(_mockEts.Object, mockFactory.Object);
    }

    [Fact]
    public void RegisterTower_FirstTowerGetsLetterA()
    {
        _service.RegisterTower("AA:BB", "http://192.168.1.50");
        Assert.Equal("A", _service.Towers["AA:BB"].DisplayLetter);
    }

    [Fact]
    public void RegisterTower_AssignsSequentialLetters()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.RegisterTower("T2", "http://192.168.1.51");
        Assert.Equal("A", _service.Towers["T1"].DisplayLetter);
        Assert.Equal("B", _service.Towers["T2"].DisplayLetter);
    }

    [Fact]
    public void RegisterTower_DuplicateIdIgnored()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.RegisterTower("T1", "http://192.168.1.50");
        Assert.Single(_service.Towers);
    }

    [Fact]
    public void HandleTowerButtonPressed_SetsIsPressed()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.Towers["T1"].CurrentColor = TeamColor.NONE;
        _service.HandleTowerButtonPressed("T1", TeamColor.RED);

        var tower = _service.Towers["T1"];
        Assert.True(tower.IsPressed);
        Assert.Equal(TeamColor.RED, tower.PressedByColor);
        Assert.NotNull(tower.LastPressed);
    }

    [Fact]
    public void HandleTowerButtonPressed_IgnoresIfAlreadyPressed()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.HandleTowerButtonPressed("T1", TeamColor.RED);
        _service.HandleTowerButtonPressed("T1", TeamColor.BLUE);
        Assert.Equal(TeamColor.RED, _service.Towers["T1"].PressedByColor);
    }

    [Fact]
    public void HandleTowerButtonPressed_IgnoresIfLocked()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.Towers["T1"].CurrentColor = TeamColor.LOCKED;
        _service.HandleTowerButtonPressed("T1", TeamColor.RED);
        Assert.False(_service.Towers["T1"].IsPressed);
    }

    [Fact]
    public void HandleTowerButtonReleased_ClearsPress()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.HandleTowerButtonPressed("T1", TeamColor.RED);
        _service.HandleTowerButtonReleased("T1");
        Assert.False(_service.Towers["T1"].IsPressed);
        Assert.Equal(TeamColor.NONE, _service.Towers["T1"].PressedByColor);
    }

    [Fact]
    public void GetPoints_SumsMultipliersCorrectly()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.RegisterTower("T2", "http://192.168.1.51");
        _service.Towers["T1"].Multiplier = 1.5;
        _service.Towers["T2"].Multiplier = 2.0;
        _service.Towers["T1"].CurrentColor = TeamColor.RED;
        _service.Towers["T2"].CurrentColor = TeamColor.RED;

        var points = _service.GetPoints(TeamColor.RED);
        Assert.Equal(4, points);
    }

    [Fact]
    public void TowerChangeColor_SetsColor()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.TowerChangeColor("T1", TeamColor.BLUE);
        Assert.Equal(TeamColor.BLUE, _service.Towers["T1"].CurrentColor);
    }
}
