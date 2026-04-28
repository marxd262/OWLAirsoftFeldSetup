using OWLServer.Models;

namespace OWLServer.Services.Interfaces;

public interface ITowerManagerService
{
    Dictionary<string, Tower> Towers { get; }
    void RegisterTower(string id, string ip);
    void TowerChangeColor(string towerId, TeamColor newColor);
    int GetPoints(TeamColor teamColor);
    void HandleTowerButtonPressed(string towerId, TeamColor color);
    void HandleTowerButtonReleased(string towerId);
    void SetColorForAllTowers(TeamColor teamColor);
    void SetAllTowerToStartColor();
    Task PingAll();
    Task OffTowers();
    Task ResetTowers();
}
