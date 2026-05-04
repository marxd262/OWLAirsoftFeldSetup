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

        private Dictionary<string, int> _towerCaptureCounts = new();
        private Dictionary<string, TeamColor> _lastKnownTowerColors = new();
        private Dictionary<string, Tower>? _activeTowers;
        private Dictionary<TeamColor, TeamBase>? _activeTeams;
        private TeamColor _teamInWald;

        public GameHistoryService(
            IDbContextFactory<DatabaseContext> dbFactory,
            IExternalTriggerService externalTriggerService)
        {
            _dbFactory = dbFactory;
            _externalTriggerService = externalTriggerService;
        }

        public void RecordGameStart(GameMode gameMode, Dictionary<string, Tower> towers,
            Dictionary<TeamColor, TeamBase> teams, TeamColor teamInWald)
        {
            EndReason = "Completed";
            _towerCaptureCounts = new();
            _lastKnownTowerColors = new();
            _activeTowers = towers;
            _activeTeams = teams;
            _teamInWald = teamInWald;

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

            _externalTriggerService.StateHasChangedAction += TrackCaptures;
        }

        private void TrackCaptures()
        {
            if (CurrentGameId == null || _activeTowers == null) return;

            foreach (var kvp in _activeTowers)
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

        public void RecordGameEnd(IGameModeBase? currentGame, Dictionary<string, Tower> towers)
        {
            _externalTriggerService.StateHasChangedAction -= TrackCaptures;

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
                team.TowersControlled = towers.Values
                    .Count(t => t.CurrentColor == teamColor);

                if (currentGame is GameModeTeamDeathmatch tdm)
                {
                    team.Deaths = tdm.TeamDeaths.GetValueOrDefault(teamColor, 0);
                }
            }

            var towerRecords = db.GameHistoryTowers.Where(t => t.GameHistoryId == CurrentGameId.Value).ToList();
            foreach (var tr in towerRecords)
            {
                if (towers.TryGetValue(tr.MacAddress, out var tower))
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
            _activeTowers = null;
            _activeTeams = null;
            _towerCaptureCounts = new();
            _lastKnownTowerColors = new();
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
