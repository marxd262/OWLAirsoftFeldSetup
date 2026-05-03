using OWLServer.Models;

namespace OWLServer.Services.Interfaces
{
    public interface IGameHistoryService
    {
        int? CurrentGameId { get; }
        string EndReason { get; set; }
        void RecordGameStart();
        void RecordGameEnd();
        List<GameHistory> GetAllGames();
        GameHistory? GetGame(int id);
        List<GameHistoryTeam> GetGameTeams(int gameHistoryId);
        List<GameHistoryTower> GetGameTowers(int gameHistoryId);
        GameHistorySnapshot? GetGameSnapshot(int gameHistoryId);
    }
}
