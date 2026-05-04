using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services;
using OWLServer.Services.Interfaces;
using Radzen;
using Radzen.Blazor;

namespace OWLServer.Components.Pages.AdminPages;

public partial class GameHistoryPage : IDisposable
{
    [Inject] private IExternalTriggerService ExternalTriggerService { get; set; } = null!;
    [Inject] private IGameHistoryService GameHistoryService { get; set; } = null!;
    [Inject] private DialogService DialogService { get; set; } = null!;

    private List<GameHistory> _allGames = new();
    private List<GameHistory> _filteredGames = new();
    private Dictionary<int, List<GameHistoryTeam>> _gameTeamsByGame = new();

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private int? _filterGameMode;
    private int? _filterWinner;
    private string _sortOrder = "newest";

    private Action _stateChangedHandler = null!;

    private readonly List<(string Text, int Value)> _gameModeOptions = new()
    {
        ("Conquest", 2),
        ("TeamDeathmatch", 1),
        ("Timer", 3),
        ("ChainBreak", 4)
    };

    private readonly List<(string Text, int? Value)> _winnerOptions = new()
    {
        ("Blau", (int)TeamColor.BLUE),
        ("Rot", (int)TeamColor.RED),
        ("Unentschieden", (int)TeamColor.NONE)
    };

    private readonly List<(string Text, string Value)> _sortOptions = new()
    {
        ("Neueste zuerst", "newest"),
        ("Alteste zuerst", "oldest"),
        ("Langste Dauer", "longest")
    };

    protected override void OnInitialized()
    {
        _stateChangedHandler = () => InvokeAsync(StateHasChanged);
        ExternalTriggerService.StateHasChangedAction += _stateChangedHandler;
        LoadGames();
    }

    private void LoadGames()
    {
        _allGames = GameHistoryService.GetAllGames();
        foreach (var game in _allGames)
        {
            _gameTeamsByGame[game.Id] = GameHistoryService.GetGameTeams(game.Id);
        }
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = _allGames.AsEnumerable();

        if (_dateFrom.HasValue)
            query = query.Where(g => g.StartTime.Date >= _dateFrom.Value.Date);
        if (_dateTo.HasValue)
            query = query.Where(g => g.StartTime.Date <= _dateTo.Value.Date);
        if (_filterGameMode.HasValue)
            query = query.Where(g => g.GameMode == _filterGameMode.Value);
        if (_filterWinner.HasValue)
            query = query.Where(g => g.Winner == _filterWinner.Value);

        query = _sortOrder switch
        {
            "oldest" => query.OrderBy(g => g.StartTime),
            "longest" => query.OrderByDescending(g => g.Duration),
            _ => query.OrderByDescending(g => g.StartTime)
        };

        _filteredGames = query.ToList();
    }

    private void OnFilterChanged() => ApplyFilters();

    private double GetScorePercent(GameHistoryTeam team)
    {
        var teams = _gameTeamsByGame.GetValueOrDefault(team.GameHistoryId, new());
        int max = teams.Max(t => t.FinalScore);
        if (max == 0) return 0;
        return (double)team.FinalScore / max * 100;
    }

    private string GetGameModeDisplayName(int gameMode) => ((GameMode)gameMode) switch
    {
        GameMode.Conquest => "Eroberung",
        GameMode.TeamDeathMatch => "Team Deathmatch",
        GameMode.Timer => "Timer",
        GameMode.ChainBreak => "Kettenbruch",
        _ => ((GameMode)gameMode).ToString()
    };

    private async Task OpenDetail(int gameId)
    {
        var game = GameHistoryService.GetGame(gameId);
        var teams = GameHistoryService.GetGameTeams(gameId);
        var towers = GameHistoryService.GetGameTowers(gameId);
        var snapshot = GameHistoryService.GetGameSnapshot(gameId);

        var parameters = new Dictionary<string, object>
        {
            { "Game", game! },
            { "Teams", teams },
            { "Towers", towers },
            { "Snapshot", snapshot }
        };

        await DialogService.OpenAsync<GameHistoryDetail>("Spieldetails", parameters,
            new DialogOptions { Width = "800px", Height = "90vh", ShowTitle = true, Draggable = true });
    }

    public void Dispose()
    {
        ExternalTriggerService.StateHasChangedAction -= _stateChangedHandler;
    }
}
