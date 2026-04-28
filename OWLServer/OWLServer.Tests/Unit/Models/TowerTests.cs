using System.Drawing;
using OWLServer.Models;
using OWLServer.Tests.Helpers;
using Xunit;

namespace OWLServer.Tests.Unit.Models;

public class TowerTests
{
    [Fact]
    public void SetTowerColor_UpdatesCurrentColor()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient);
        tower.SetTowerColor(TeamColor.RED);
        Assert.Equal(TeamColor.RED, tower.CurrentColor);
    }

    [Fact]
    public async Task SetTowerColor_CallsHttpClient()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient);
        await tower.SendColorToTower(Color.Red);
        Assert.Contains(httpClient.PostedUrls, u => u.Contains("/api/setcolor/"));
    }

    [Fact]
    public void SetToStartColor_SetsNone()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient);
        tower.CurrentColor = TeamColor.RED;
        tower.SetToStartColor();
        Assert.Equal(TeamColor.NONE, tower.CurrentColor);
    }

    [Fact]
    public void IsLocked_WhenColorIsLocked()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient) { CurrentColor = TeamColor.LOCKED };
        Assert.True(tower.IsLocked);
    }

    [Fact]
    public void IsLocked_WhenColorNotLocked()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient) { CurrentColor = TeamColor.RED };
        Assert.False(tower.IsLocked);
    }

    [Fact]
    public void Reset_ClearsAllRuntimeState()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            CapturedAt = DateTime.Now,
            IsPressed = true,
            PressedByColor = TeamColor.RED,
            CaptureProgress = 0.5,
            CurrentColor = TeamColor.BLUE
        };

        tower.Reset();

        Assert.Null(tower.CapturedAt);
        Assert.False(tower.IsPressed);
        Assert.Equal(TeamColor.NONE, tower.PressedByColor);
        Assert.Equal(0, tower.CaptureProgress);
        Assert.Equal(TeamColor.NONE, tower.CurrentColor);
    }

    [Fact]
    public void GetDisplayProgress_WhenPressed_ReturnsCaptureProgress()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            IsPressed = true,
            CaptureProgress = 0.75
        };
        Assert.Equal(75, tower.GetDisplayProgress());
    }

    [Fact]
    public void GetDisplayProgress_WhenNotPressed_Returns100()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            IsPressed = false,
            CaptureProgress = 0.5
        };
        Assert.Equal(100, tower.GetDisplayProgress());
    }
}
