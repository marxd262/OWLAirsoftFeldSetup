using Microsoft.Extensions.DependencyInjection;
using Xunit;
using OWLServer.Models;
using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Integration.Api;

public class KlickerClickedTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public KlickerClickedTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task KlickerClicked_FiresEvent()
    {
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        bool eventFired = false;
        TeamColor receivedColor = TeamColor.NONE;

        void Handler(object? s, KlickerEventArgs a)
        {
            eventFired = true;
            receivedColor = a.TeamColor;
        }

        ets.KlickerPressedAction += Handler;

        try
        {
            await _client.PostAsync("/api/KlickerClicked?color=BLUE", null);
            await Task.Delay(800);

            Assert.True(eventFired);
            Assert.Equal(TeamColor.BLUE, receivedColor);
        }
        finally
        {
            ets.KlickerPressedAction -= Handler;
        }
    }
}
