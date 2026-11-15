using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;
using OWLServer.Tests.Helpers;
using Xunit;

namespace OWLServer.Tests.Unit.Services;

public class GameHistoryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = TestDbHelper.CreateConnection();
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;
    private readonly ExternalTriggerService _ets = new();
    private readonly GameHistoryService _history;
    private readonly Mock<IGameModeBase> _game = new();
    private readonly Dictionary<string, Tower> _towers = new()
    {
        ["A"] = new Tower { MacAddress = "A", DisplayLetter = "A", CurrentColor = TeamColor.NONE }
    };
    private readonly Dictionary<TeamColor, TeamBase> _teams = new()
    {
        [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
        [TeamColor.RED] = new TeamBase(TeamColor.RED)
    };

    public GameHistoryServiceTests()
    {
        _dbFactory = TestDbHelper.CreateContextFactory(_connection);
        _history = new GameHistoryService(_dbFactory, _ets);
    }

    public void Dispose()
    {
        _history.Dispose();
        _connection.Dispose();
    }

    private List<GameHistoryEvent> SavedEvents()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.GameHistoryEvents.ToList();
    }

    [Fact]
    public void ScoreChange_IsSavedAsPointsEvent()
    {
        var blueScore = 15;
        _game.Setup(g => g.GetDisplayPoints(TeamColor.BLUE)).Returns(() => blueScore);
        _game.Setup(g => g.GetDisplayPoints(TeamColor.RED)).Returns(15);
        _history.RecordGameStart(_game.Object, _towers, _teams, TeamColor.BLUE);

        blueScore = 12;
        _ets.StateHasChangedAction?.Invoke();
        _history.RecordGameEnd(_game.Object, _towers);

        var points = Assert.Single(SavedEvents(), e => e.EventType == (int)GameEventType.PointsAwarded);
        Assert.Equal((int)TeamColor.BLUE, points.TeamColor);
        Assert.Equal(-3, points.Value);
    }

    [Fact]
    public void PressAndRelease_AreSavedWithTheTeamThatPressed()
    {
        _history.RecordGameStart(_game.Object, _towers, _teams, TeamColor.BLUE);
        var tower = _towers["A"];

        tower.IsPressed = true;
        tower.PressedByColor = TeamColor.RED;
        _ets.StateHasChangedAction?.Invoke();
        tower.IsPressed = false; // what a button release does
        tower.PressedByColor = TeamColor.NONE;
        _ets.StateHasChangedAction?.Invoke();
        _history.RecordGameEnd(_game.Object, _towers);

        var events = SavedEvents();
        Assert.Contains(events, e => e.EventType == (int)GameEventType.ButtonPressed && e.TeamColor == (int)TeamColor.RED && e.Side == "Stadt");
        Assert.Contains(events, e => e.EventType == (int)GameEventType.ButtonReleased && e.TeamColor == (int)TeamColor.RED);
    }

    [Fact]
    public void StartingAgainWithoutEnd_DoesNotDoubleEvents()
    {
        _history.RecordGameStart(_game.Object, _towers, _teams, TeamColor.BLUE);
        _history.RecordGameStart(_game.Object, _towers, _teams, TeamColor.BLUE); // first game never ended

        _ets.InvokeKlickerPressed(TeamColor.RED);
        _history.RecordGameEnd(_game.Object, _towers);

        Assert.Single(SavedEvents(), e => e.EventType == (int)GameEventType.Death);
    }
}
