using Microsoft.Extensions.DependencyInjection;
using Xunit;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Integration.Api;

public class TowerButtonFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TowerButtonFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task RegisterTowerAndWait(string id)
    {
        await _client.PostAsync($"/api/RegisterTower?id={id}&ip=http://{id}.local", null);
        await Task.Delay(500);
    }

    [Fact]
    public async Task TowerButtonPressed_SetsIsPressed()
    {
        await RegisterTowerAndWait("BTN:01");

        await _client.PostAsync("/api/TowerButtonPressed?id=BTN:01&color=RED", null);
        await Task.Delay(500);

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var tower = gss.TowerManagerService.Towers["BTN:01"];
        Assert.True(tower.IsPressed);
        Assert.Equal(TeamColor.RED, tower.PressedByColor);
    }

    [Fact]
    public async Task TowerButtonReleased_ClearsPress()
    {
        await RegisterTowerAndWait("BTN:02");

        await _client.PostAsync("/api/TowerButtonPressed?id=BTN:02&color=BLUE", null);
        await Task.Delay(500);
        await _client.PostAsync("/api/TowerButtonReleased?id=BTN:02", null);
        await Task.Delay(500);

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var tower = gss.TowerManagerService.Towers["BTN:02"];
        Assert.False(tower.IsPressed);
        Assert.Equal(TeamColor.NONE, tower.PressedByColor);
    }

    [Fact]
    public async Task CaptureTower_ChangesColor()
    {
        await RegisterTowerAndWait("CAP:01");

        await _client.PostAsync("/api/CaptureTower?id=CAP:01&color=RED", null);
        await Task.Delay(500);

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        Assert.Equal(TeamColor.RED, gss.TowerManagerService.Towers["CAP:01"].CurrentColor);
    }
}
