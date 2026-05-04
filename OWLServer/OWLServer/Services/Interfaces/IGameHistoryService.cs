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
    }
}
