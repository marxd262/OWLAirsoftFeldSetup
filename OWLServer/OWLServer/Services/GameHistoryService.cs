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
        private readonly IGameStateService _gameStateService;

        public int? CurrentGameId { get; private set; }
        public string EndReason { get; set; } = "Completed";

        private Dictionary<string, int> _towerCaptureCounts = new();
        private Dictionary<string, TeamColor> _lastKnownTowerColors = new();

        public GameHistoryService(
            IDbContextFactory<DatabaseContext> dbFactory,
            IExternalTriggerService externalTriggerService,
            IGameStateService gameStateService)
        {
            _dbFactory = dbFactory;
            _externalTriggerService = externalTriggerService;
            _gameStateService = gameStateService;
        }

        public void RecordGameStart()
        {
            EndReason = "Completed";
            _towerCaptureCounts = new();
            _lastKnownTowerColors = new();

            using var db = _dbFactory.CreateDbContext();

            var history = new GameHistory
            {
                GameMode = (int)(_gameStateService.CurrentGame?.GameMode ?? GameMode.None),
                StartTime = DateTime.Now,
                Winner = (int)TeamColor.NONE
            };

            db.GameHistories.Add(history);
            db.SaveChanges();
            CurrentGameId = history.Id;

            foreach (var kvp in _gameStateService.TowerManagerService.Towers)
            {
                var tower = kvp.Value;
                _towerCaptureCounts[kvp.Key] = 0;
                _lastKnownTowerColors[kvp.Key] = tower.CurrentColor;

                db.GameHistoryTowers.Add(new GameHistoryTower
                {
                    GameHistoryId = history.Id,
                    MacAddress = tower.MacAddress,
                    DisplayLetter = tower.DisplayLetter,
                    FinalColor = (int)TeamColor.NONE
                });
            }

            foreach (var kvp in _gameStateService.Teams)
            {
                var team = kvp.Value;
                string side = kvp.Key == _gameStateService.TeamInWald ? "Wald" : "Stadt";
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
                SnapshotJSON = BuildSnapshotJSON()
            };
            db.GameHistorySnapshots.Add(snapshot);

            db.SaveChanges();

            _externalTriggerService.StateHasChangedAction += TrackCaptures;
        }

        private void TrackCaptures()
        {
            if (CurrentGameId == null) return;

            foreach (var kvp in _gameStateService.TowerManagerService.Towers)
            {
                var tower = kvp.Value;
                if (!_lastKnownTowerColors.TryGetValue(kvp.Key, out var lastColor))
                {
                    _lastKnownTowerColors[kvp.Key] = tower.CurrentColor;
                    continue;
                }

                if (lastColor != tower.CurrentColor)
                {
                    if (tower.CurrentColor == TeamColor.BLUE || tower.CurrentColor == TeamColor.RED)
                    {
                        if (_towerCaptureCounts.ContainsKey(kvp.Key))
                            _towerCaptureCounts[kvp.Key]++;
                        else
                            _towerCaptureCounts[kvp.Key] = 1;
                    }
                    _lastKnownTowerColors[kvp.Key] = tower.CurrentColor;
                }
            }
        }

        public void RecordGameEnd()
        {
            _externalTriggerService.StateHasChangedAction -= TrackCaptures;

            if (CurrentGameId == null) return;

            using var db = _dbFactory.CreateDbContext();

            var history = db.GameHistories.Find(CurrentGameId.Value);
            if (history == null) return;

            history.EndTime = DateTime.Now;
            history.Duration = history.EndTime.Value - history.StartTime;
            history.Winner = (int)(_gameStateService.CurrentGame?.GetWinner ?? TeamColor.NONE);
            history.EndReason = EndReason;

            var teams = db.GameHistoryTeams.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var team in teams)
            {
                var teamColor = (TeamColor)team.TeamColor;
                team.FinalScore = _gameStateService.CurrentGame?.GetDisplayPoints(teamColor) ?? 0;
                team.TowersControlled = _gameStateService.TowerManagerService.Towers.Values
                    .Count(t => t.CurrentColor == teamColor);

                var currentGame = _gameStateService.CurrentGame;
                if (currentGame is GameModeTeamDeathmatch tdm)
                {
                    team.Deaths = tdm.TeamDeaths.GetValueOrDefault(teamColor, 0);
                }
            }

            var towerRecords = db.GameHistoryTowers.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var tr in towerRecords)
            {
                if (_gameStateService.TowerManagerService.Towers.TryGetValue(tr.MacAddress, out var tower))
                {
                    tr.FinalColor = (int)tower.CurrentColor;
                }
                if (_towerCaptureCounts.TryGetValue(tr.MacAddress, out var count))
                {
                    tr.Captures = count;
                }
            }

            db.SaveChanges();

            CurrentGameId = null;
            _towerCaptureCounts = new();
            _lastKnownTowerColors = new();
        }

        private string BuildSnapshotJSON()
        {
            var snapshot = new
            {
                Timestamp = DateTime.Now,
                GameMode = _gameStateService.CurrentGame?.GameMode.ToString(),
                Towers = _gameStateService.TowerManagerService.Towers.Values.Select(t => new
                {
                    t.MacAddress,
                    t.Name,
                    t.DisplayLetter,
                    t.TimeToCaptureInSeconds,
                    t.Multiplier,
                    t.ResetsAfterInSeconds,
                    Location = t.Location != null ? new { t.Location.Top, t.Location.Left } : null
                }).ToList(),
                Teams = _gameStateService.Teams.Select(kvp => new
                {
                    TeamColor = kvp.Key.ToString(),
                    kvp.Value.Name,
                    Side = kvp.Key == _gameStateService.TeamInWald ? "Wald" : "Stadt"
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

        public void Dispose()
        {
            _externalTriggerService.StateHasChangedAction -= TrackCaptures;
        }
    }
}
