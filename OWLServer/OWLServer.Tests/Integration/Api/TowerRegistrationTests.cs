using Microsoft.Extensions.DependencyInjection;
using Xunit;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Integration.Api;

public class TowerRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TowerRegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterTower_AddsTowerToDictionary()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var countBefore = gss.TowerManagerService.Towers.Count;

        await _client.PostAsync("/api/RegisterTower?id=AA:BB:CC:DD&ip=http://192.168.1.50", null);
        await Task.Delay(500);

        Assert.True(gss.TowerManagerService.Towers.ContainsKey("AA:BB:CC:DD"));
        Assert.Equal(countBefore + 1, gss.TowerManagerService.Towers.Count);
    }

    [Fact]
    public async Task RegisterTower_DuplicateIgnored()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var countBefore = gss.TowerManagerService.Towers.Count;

        await _client.PostAsync("/api/RegisterTower?id=XX:YY&ip=http://192.168.1.50", null);
        await Task.Delay(500);
        await _client.PostAsync("/api/RegisterTower?id=XX:YY&ip=http://192.168.1.50", null);
        await Task.Delay(500);

        Assert.True(gss.TowerManagerService.Towers.ContainsKey("XX:YY"));
        Assert.Equal(countBefore + 1, gss.TowerManagerService.Towers.Count);
    }
}
