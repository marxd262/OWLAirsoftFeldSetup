using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services
{
    public class GameHistoryService : IGameHistoryService, IDisposable
    {
        private readonly IDbContextFactory<DatabaseContext> _dbFactory;
        private readonly IExternalTriggerService _externalTriggerService;

        public int? CurrentGameId { get; private set; }
        public string EndReason { get; set; } = "Completed";

        private readonly object _eventLock = new();
        private List<GameHistoryEvent> _pendingEvents = new();
        private Timer? _flushTimer;
        private Dictionary<string, TeamColor> _lastKnownTowerColors = new();
        private Dictionary<TeamColor, int> _lastKnownScores = new();
        private Dictionary<string, TeamColor> _lastKnownPressedBy = new();
        private Dictionary<string, Tower>? _activeTowers;
        private Dictionary<TeamColor, TeamBase>? _activeTeams;
        private TeamColor _teamInWald;
        private IGameModeBase? _activeGame;

        public GameHistoryService(
            IDbContextFactory<DatabaseContext> dbFactory,
            IExternalTriggerService externalTriggerService)
        {
            _dbFactory = dbFactory;
            _externalTriggerService = externalTriggerService;
        }

        public void RecordGameStart(IGameModeBase game, Dictionary<string, Tower> towers,
            Dictionary<TeamColor, TeamBase> teams, TeamColor teamInWald)
        {
            StopListening(); // a previous game that never ended must not leave a second subscription behind
            var gameMode = game.GameMode;
            EndReason = "Completed";
            _pendingEvents = new();
            _lastKnownTowerColors = new();
            _lastKnownPressedBy = new();
            _activeTowers = towers;
            _activeTeams = teams;
            _activeGame = game;
            _teamInWald = teamInWald;
            _lastKnownScores = teams.Keys.ToDictionary(team => team, game.GetDisplayPoints);

            foreach (var kvp in towers)
                _lastKnownTowerColors[kvp.Key] = kvp.Value.CurrentColor;

            using var db = _dbFactory.CreateDbContext();

            var history = new GameHistory
            {
                GameMode = (int)gameMode,
                StartTime = DateTime.Now,
                Winner = (int)TeamColor.NONE
            };

            db.GameHistories.Add(history);
            db.SaveChanges();
            CurrentGameId = history.Id;

            foreach (var kvp in towers)
            {
                var tower = kvp.Value;
                db.GameHistoryTowers.Add(new GameHistoryTower
                {
                    GameHistoryId = history.Id,
                    MacAddress = tower.MacAddress,
                    DisplayLetter = tower.DisplayLetter,
                    FinalColor = (int)TeamColor.NONE
                });
            }

            foreach (var kvp in teams)
            {
                var team = kvp.Value;
                string side = kvp.Key == teamInWald ? "Wald" : "Stadt";
                db.GameHistoryTeams.Add(new GameHistoryTeam
                {
                    GameHistoryId = history.Id,
                    TeamColor = (int)team.TeamColor,
                    TeamName = team.Name,
                    Side = side
                });
            }

            var snapshot = new GameHistorySnapshot
            {
                GameHistoryId = history.Id,
                SnapshotJSON = BuildSnapshotJSON(gameMode, towers, teams)
            };
            db.GameHistorySnapshots.Add(snapshot);

            db.SaveChanges();

            _externalTriggerService.StateHasChangedAction += OnStateChanged;
            _externalTriggerService.KlickerPressedAction += OnKlickerPressed;

            _flushTimer = new Timer(_ => FlushEvents(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private void StopListening()
        {
            _externalTriggerService.StateHasChangedAction -= OnStateChanged;
            _externalTriggerService.KlickerPressedAction -= OnKlickerPressed;
            _flushTimer?.Dispose();
            _flushTimer = null;
        }

        public void RecordGameEnd(IGameModeBase? currentGame, Dictionary<string, Tower> towers)
        {
            OnStateChanged(); // the tick that ended the game is not followed by another refresh
            StopListening();

            FlushEvents();

            if (CurrentGameId == null) return;

            using var db = _dbFactory.CreateDbContext();

            var history = db.GameHistories.Find(CurrentGameId.Value);
            if (history == null) return;

            history.EndTime = DateTime.Now;
            history.Duration = history.EndTime.Value - history.StartTime;
            history.Winner = (int)(currentGame?.GetWinner ?? TeamColor.NONE);
            history.EndReason = EndReason;

            var teams = db.GameHistoryTeams.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var team in teams)
            {
                var teamColor = (TeamColor)team.TeamColor;
                team.FinalScore = currentGame?.GetDisplayPoints(teamColor) ?? 0;
            }

            var towerRecords = db.GameHistoryTowers.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var tr in towerRecords)
            {
                if (towers.TryGetValue(tr.MacAddress, out var tower))
                {
                    tr.FinalColor = (int)tower.CurrentColor;
                }
            }

            db.SaveChanges();

            CurrentGameId = null;
            _activeTowers = null;
            _activeTeams = null;
            _activeGame = null;
            _pendingEvents = new();
            _lastKnownTowerColors = new();
            _lastKnownScores = new();
            _lastKnownPressedBy = new();
        }

        private void OnStateChanged()
        {
            if (CurrentGameId == null || _activeTowers == null) return;

            foreach (var (mac, tower) in _activeTowers)
            {
                var lastColor = _lastKnownTowerColors.GetValueOrDefault(mac, tower.CurrentColor);
                if (lastColor != tower.CurrentColor)
                {
                    if (tower.CurrentColor is TeamColor.BLUE or TeamColor.RED)
                        AddEvent(GameEventType.TowerCaptured, tower.CurrentColor, tower.DisplayLetter);
                    else if (tower.CurrentColor == TeamColor.NONE && lastColor is TeamColor.BLUE or TeamColor.RED)
                        AddEvent(GameEventType.TowerNeutralized, lastColor, tower.DisplayLetter); // an unlock (LOCKED → NONE) is not a neutralization
                }
                _lastKnownTowerColors[mac] = tower.CurrentColor;

                // remember who pressed: on release PressedByColor is already cleared
                var pressedBy = tower.IsPressed ? tower.PressedByColor : TeamColor.NONE;
                var lastPressedBy = _lastKnownPressedBy.GetValueOrDefault(mac, TeamColor.NONE);
                if (pressedBy != lastPressedBy)
                {
                    if (lastPressedBy != TeamColor.NONE) AddEvent(GameEventType.ButtonReleased, lastPressedBy, tower.DisplayLetter);
                    if (pressedBy != TeamColor.NONE) AddEvent(GameEventType.ButtonPressed, pressedBy, tower.DisplayLetter);
                    _lastKnownPressedBy[mac] = pressedBy;
                }
            }

            if (_activeGame != null)
            {
                foreach (var team in _lastKnownScores.Keys.ToList())
                {
                    int score = _activeGame.GetDisplayPoints(team);
                    if (score != _lastKnownScores[team])
                        AddEvent(GameEventType.PointsAwarded, team, null, score - _lastKnownScores[team]);
                    _lastKnownScores[team] = score;
                }
            }
        }

        private void OnKlickerPressed(object? sender, KlickerEventArgs args)
        {
            if (CurrentGameId == null) return;
            AddEvent(GameEventType.Death, args.TeamColor, null);
        }

        private void AddEvent(GameEventType type, TeamColor teamColor, string? towerLetter, int? value = null)
        {
            string side = teamColor == _teamInWald ? "Wald" : "Stadt";
            lock (_eventLock)
            {
                // game may have ended between the caller's check and here
                if (CurrentGameId is not int gameId) return;
                _pendingEvents.Add(new GameHistoryEvent
                {
                    GameHistoryId = gameId,
                    Timestamp = DateTimeOffset.Now,
                    EventType = (int)type,
                    TeamColor = (int)teamColor,
                    Side = side,
                    TowerLetter = towerLetter,
                    Value = value
                });
            }
        }

        private void FlushEvents()
        {
            List<GameHistoryEvent> batch;
            lock (_eventLock)
            {
                if (_pendingEvents.Count == 0) return;
                batch = _pendingEvents;
                _pendingEvents = new();
            }

            try
            {
                using var db = _dbFactory.CreateDbContext();
                db.GameHistoryEvents.AddRange(batch);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // runs on a timer thread: an unhandled exception would kill the server; requeue for next flush
                Console.Error.WriteLine($"GameHistory: event flush failed, retrying later: {ex.Message}");
                lock (_eventLock) _pendingEvents.InsertRange(0, batch);
            }
        }

        private string BuildSnapshotJSON(GameMode gameMode, Dictionary<string, Tower> towers,
            Dictionary<TeamColor, TeamBase> teams)
        {
            var snapshot = new
            {
                Timestamp = DateTime.Now,
                GameMode = gameMode.ToString(),
                Towers = towers.Values.Select(t => new
                {
                    t.MacAddress,
                    t.Name,
                    t.DisplayLetter,
                    t.TimeToCaptureInSeconds,
                    t.Multiplier,
                    t.ResetsAfterInSeconds,
                    Location = t.Location != null ? new { t.Location.Top, t.Location.Left } : null
                }).ToList(),
                Teams = teams.Select(kvp => new
                {
                    TeamColor = kvp.Key.ToString(),
                    kvp.Value.Name,
                    Side = kvp.Key == _teamInWald ? "Wald" : "Stadt"
                }).ToList()
            };

            return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public List<GameHistory> GetAllGames()
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories
                .Include(gh => gh.Teams)
                .OrderByDescending(gh => gh.StartTime)
                .ToList();
        }

        public GameHistory? GetGame(int id)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories.Find(id);
        }

        public List<GameHistoryTeam> GetGameTeams(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryTeams
                .Where(t => t.GameHistoryId == gameHistoryId)
                .ToList();
        }

        public List<GameHistoryTower> GetGameTowers(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryTowers
                .Where(t => t.GameHistoryId == gameHistoryId)
                .ToList();
        }

        public GameHistorySnapshot? GetGameSnapshot(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistorySnapshots
                .FirstOrDefault(s => s.GameHistoryId == gameHistoryId);
        }

        public List<GameHistoryEvent> GetGameEvents(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId)
                .ToList()
                .OrderBy(e => e.Timestamp)
                .ToList();
        }

        public Dictionary<int, int> GetDeathsPerMinute(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            var game = db.GameHistories.Find(gameHistoryId);
            if (game == null) return new();

            return db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId && e.EventType == (int)GameEventType.Death)
                .AsEnumerable()
                .GroupBy(e => (int)((e.Timestamp - game.StartTime).TotalMinutes))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public Dictionary<string, int> GetTowerContestRanking(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId && e.EventType == (int)GameEventType.TowerCaptured)
                .AsEnumerable()
                .GroupBy(e => e.TowerLetter ?? "")
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public List<(DateTimeOffset Time, int BlueScore, int RedScore)> GetScoreTimeline(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            var events = db.GameHistoryEvents
                .Where(e => e.GameHistoryId == gameHistoryId && e.EventType == (int)GameEventType.PointsAwarded)
                .ToList()
                .OrderBy(e => e.Timestamp)
                .ToList();

            int blue = 0, red = 0;
            var timeline = new List<(DateTimeOffset, int, int)>();

            foreach (var e in events)
            {
                if ((TeamColor)e.TeamColor == TeamColor.BLUE) blue += e.Value ?? 0;
                else red += e.Value ?? 0;
                timeline.Add((e.Timestamp, blue, red));
            }

            return timeline;
        }

        public List<GameHistory> GetGamesByDateRange(DateTime from, DateTime to)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories
                .Where(g => g.StartTime >= from && g.StartTime <= to)
                .OrderBy(g => g.StartTime)
                .ToList();
        }

        public Dictionary<string, int> GetWinRateByMode(DateTime? from, DateTime? to)
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.GameHistories.Where(g => g.Winner != (int)TeamColor.NONE); // draws and stopped games are no wins
            if (from.HasValue) query = query.Where(g => g.StartTime >= from.Value);
            if (to.HasValue) query = query.Where(g => g.StartTime <= to.Value);

            return query
                .AsEnumerable()
                .GroupBy(g => ((GameMode)g.GameMode).ToString())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public Dictionary<string, int> GetWinRateBySide(DateTime? from, DateTime? to)
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.GameHistories.AsQueryable();
            if (from.HasValue) query = query.Where(g => g.StartTime >= from.Value);
            if (to.HasValue) query = query.Where(g => g.StartTime <= to.Value);

            var games = query.ToList();
            var result = new Dictionary<string, int> { { "Wald", 0 }, { "Stadt", 0 } };

            foreach (var game in games)
            {
                var winner = (TeamColor)game.Winner;
                if (winner == TeamColor.NONE) continue;

                var teams = db.GameHistoryTeams.Where(t => t.GameHistoryId == game.Id).ToList();
                var winnerTeam = teams.FirstOrDefault(t => (TeamColor)t.TeamColor == winner);
                if (winnerTeam != null && result.ContainsKey(winnerTeam.Side))
                {
                    result[winnerTeam.Side]++;
                }
            }

            return result;
        }

        public List<(DateTime Day, double AvgDuration)> GetAvgDurationByDay(DateTime from, DateTime to)
        {
            using var db = _dbFactory.CreateDbContext();
            return db.GameHistories
                .Where(g => g.StartTime >= from && g.StartTime <= to && g.EndTime != null)
                .AsEnumerable()
                .GroupBy(g => g.StartTime.Date)
                .Select(g => (g.Key, g.Average(x => x.Duration.TotalMinutes)))
                .OrderBy(x => x.Key)
                .ToList();
        }

        public List<(DateTime Day, int BlueDeaths, int RedDeaths)> GetDeathsByDay(DateTime from, DateTime to)
        {
            using var db = _dbFactory.CreateDbContext();
            var gameIds = db.GameHistories
                .Where(g => g.StartTime >= from && g.StartTime <= to)
                .Select(g => g.Id)
                .ToList();

            var events = db.GameHistoryEvents
                .Where(e => gameIds.Contains(e.GameHistoryId) && e.EventType == (int)GameEventType.Death)
                .ToList();

            var gameDates = db.GameHistories
                .Where(g => gameIds.Contains(g.Id))
                .ToDictionary(g => g.Id, g => g.StartTime.Date);

            return events
                .GroupBy(e => gameDates.GetValueOrDefault(e.GameHistoryId, DateTime.MinValue))
                .Select(g => (
                    g.Key,
                    g.Count(e => (TeamColor)e.TeamColor == TeamColor.BLUE),
                    g.Count(e => (TeamColor)e.TeamColor == TeamColor.RED)
                ))
                .OrderBy(x => x.Key)
                .ToList();
        }

        public Dictionary<string, int> GetGlobalTowerHotspots(DateTime? from, DateTime? to)
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.GameHistoryEvents
                .Where(e => e.EventType == (int)GameEventType.TowerCaptured && e.TowerLetter != null);

            if (from.HasValue)
            {
                var gameIdsFrom = db.GameHistories.Where(g => g.StartTime >= from.Value).Select(g => g.Id).ToList();
                query = query.Where(e => gameIdsFrom.Contains(e.GameHistoryId));
            }
            if (to.HasValue)
            {
                var gameIdsTo = db.GameHistories.Where(g => g.StartTime <= to.Value).Select(g => g.Id).ToList();
                query = query.Where(e => gameIdsTo.Contains(e.GameHistoryId));
            }

            return query
                .AsEnumerable()
                .GroupBy(e => e.TowerLetter!)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public List<GameHistory> GetSameDayGames(int gameHistoryId)
        {
            using var db = _dbFactory.CreateDbContext();
            var game = db.GameHistories.Find(gameHistoryId);
            if (game == null) return new();

            var day = game.StartTime.Date;
            return db.GameHistories
                .Where(g => g.StartTime.Date == day && g.Id != gameHistoryId)
                .OrderBy(g => g.StartTime)
                .ToList();
        }

        public List<GameHistory> GetTodayGames()
        {
            using var db = _dbFactory.CreateDbContext();
            var today = DateTime.Today;
            return db.GameHistories
                .Where(g => g.StartTime.Date == today)
                .OrderByDescending(g => g.StartTime)
                .ToList();
        }

        public void Dispose() => StopListening();
    }
}
