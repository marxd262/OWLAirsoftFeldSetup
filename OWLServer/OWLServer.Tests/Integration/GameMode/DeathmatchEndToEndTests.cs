using Microsoft.Extensions.DependencyInjection;
using Xunit;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Integration.GameMode;

public class DeathmatchEndToEndTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeathmatchEndToEndTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Deathmatch_KlickerIncrementsDeaths()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var tdm = new GameModeTeamDeathmatch(ets, gss);
        tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        gss.CurrentGame = tdm;

        gss.StartGame();
        await Task.Delay(500);

        await _client.PostAsync("/api/KlickerClicked?color=RED", null);
        await Task.Delay(800);

        Assert.Equal(1, tdm.TeamDeaths[TeamColor.RED]);
        Assert.Equal(0, tdm.TeamDeaths[TeamColor.BLUE]);

        tdm.EndGame();
    }

    [Fact]
    public async Task Deathmatch_WinnerIsTeamWithFewerDeaths()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var tdm = new GameModeTeamDeathmatch(ets, gss);
        tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        gss.CurrentGame = tdm;

        gss.StartGame();
        await Task.Delay(500);

        await _client.PostAsync("/api/KlickerClicked?color=RED", null);
        await _client.PostAsync("/api/KlickerClicked?color=RED", null);
        await _client.PostAsync("/api/KlickerClicked?color=BLUE", null);
        await Task.Delay(800);

        tdm.EndGame();
        await Task.Delay(500);

        Assert.Equal(TeamColor.BLUE, tdm.GetWinner);
    }

    [Fact]
    public async Task Deathmatch_TieReturnsNone()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var tdm = new GameModeTeamDeathmatch(ets, gss);
        tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });

        Assert.Equal(TeamColor.NONE, tdm.GetWinner);
    }
}
