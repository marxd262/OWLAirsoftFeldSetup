using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;
using OWLServer.Services.Interfaces;
using Xunit;

namespace OWLServer.Tests.Unit.Services;

public class GameStateServiceTests
{
    private readonly Mock<IExternalTriggerService> _mockEts = new();
    private readonly Mock<IAudioService> _mockAudio = new();
    private readonly Mock<ITowerManagerService> _mockTms = new();

    private GameStateService CreateService()
    {
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };
        return new GameStateService(_mockEts.Object, _mockAudio.Object, _mockTms.Object);
    }

    [Fact]
    public void StartGame_PlaysSounds()
    {
        var service = CreateService();
        var mockGame = new Mock<IGameModeBase>();
        service.CurrentGame = mockGame.Object;

        service.StartGame();

        _mockAudio.Verify(a => a.PlaySound(Sounds.Countdown), Times.Once);
        _mockAudio.Verify(a => a.PlaySound(Sounds.Start), Times.Once);
    }

    [Fact]
    public void StartGame_RunsGame()
    {
        var service = CreateService();
        var mockGame = new Mock<IGameModeBase>();
        service.CurrentGame = mockGame.Object;

        service.StartGame();

        mockGame.Verify(g => g.RunGame(), Times.Once);
    }

    [Fact]
    public void StopGame_EndsGame()
    {
        var service = CreateService();
        var mockGame = new Mock<IGameModeBase>();
        service.CurrentGame = mockGame.Object;

        service.StopGame();

        mockGame.Verify(g => g.EndGame(), Times.Once);
    }

    [Fact]
    public void Reset_ClearsSpawnReadiness()
    {
        var service = CreateService();
        service.WaldSpawnReady = true;
        service.StadtSpawnReady = true;

        service.Reset();

        Assert.False(service.WaldSpawnReady);
        Assert.False(service.StadtSpawnReady);
    }

    [Fact]
    public void Reset_ResetsTowers()
    {
        var service = CreateService();
        service.Reset();
        _mockTms.Verify(t => t.ResetTowers(), Times.Once);
    }

    [Fact]
    public void HandleGameEnd_PlaysStopSound()
    {
        var service = CreateService();
        service.HandleGameEnd();
        _mockAudio.Verify(a => a.PlaySound(Sounds.Stop), Times.Once);
    }
}
