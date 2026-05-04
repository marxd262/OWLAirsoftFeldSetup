using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services.Interfaces
{
    public interface IGameHistoryService
    {
        int? CurrentGameId { get; }
        string EndReason { get; set; }
        void RecordGameStart(GameMode gameMode, Dictionary<string, Tower> towers,
            Dictionary<TeamColor, TeamBase> teams, TeamColor teamInWald);
        void RecordGameEnd(IGameModeBase? currentGame, Dictionary<string, Tower> towers);

        List<GameHistory> GetAllGames();
        GameHistory? GetGame(int id);
        List<GameHistoryTeam> GetGameTeams(int gameHistoryId);
        List<GameHistoryTower> GetGameTowers(int gameHistoryId);
        GameHistorySnapshot? GetGameSnapshot(int gameHistoryId);

        List<GameHistoryEvent> GetGameEvents(int gameHistoryId);
        Dictionary<int, int> GetDeathsPerMinute(int gameHistoryId);
        Dictionary<string, int> GetTowerContestRanking(int gameHistoryId);
        List<(DateTimeOffset Time, int BlueScore, int RedScore)> GetScoreTimeline(int gameHistoryId);

        List<GameHistory> GetGamesByDateRange(DateTime from, DateTime to);
        Dictionary<string, int> GetWinRateByMode(DateTime? from, DateTime? to);
        Dictionary<string, int> GetWinRateBySide(DateTime? from, DateTime? to);
        List<(DateTime Day, double AvgDuration)> GetAvgDurationByDay(DateTime from, DateTime to);
        List<(DateTime Day, int BlueDeaths, int RedDeaths)> GetDeathsByDay(DateTime from, DateTime to);
        Dictionary<string, int> GetGlobalTowerHotspots(DateTime? from, DateTime? to);
        List<GameHistory> GetSameDayGames(int gameHistoryId);
    }
}
