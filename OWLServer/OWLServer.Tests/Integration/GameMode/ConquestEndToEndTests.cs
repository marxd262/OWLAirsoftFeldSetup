using Microsoft.Extensions.DependencyInjection;
using Xunit;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Integration.GameMode;

public class ConquestEndToEndTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConquestEndToEndTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Conquest_StartsAndTracksTimer()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var conquest = new GameModeConquest(ets, gss)
        {
            GameDurationInMinutes = 1,
            PointDistributionFrequencyInSeconds = 1,
            IsTicket = false
        };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        gss.CurrentGame = conquest;

        gss.StartGame();
        await Task.Delay(500);

        Assert.True(conquest.IsRunning);
        Assert.False(conquest.IsFinished);
        Assert.NotNull(conquest.StartTime);

        conquest.EndGame();
        await Task.Delay(500);
        Assert.True(conquest.IsFinished);
    }

    [Fact]
    public async Task Conquest_NoWinnerWhenTie()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var conquest = new GameModeConquest(ets, gss) { IsTicket = false };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });

        Assert.Equal(TeamColor.NONE, conquest.GetWinner);
    }
}
