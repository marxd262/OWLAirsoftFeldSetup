using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services.Interfaces;
using Radzen.Blazor;

namespace OWLServer.Components.Pages.AdminPages;

public partial class StatisticsPage : IDisposable
{
    [Inject] private IExternalTriggerService ExternalTriggerService { get; set; } = null!;
    [Inject] private IGameHistoryService GameHistoryService { get; set; } = null!;

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private bool _hasData;
    private int _totalGames;
    private int _blueWins;
    private int _redWins;
    private double _avgDuration;

    private List<object> _winRateByMode = new();
    private List<object> _winRateBySide = new();
    private List<object> _deathsByDay = new();
    private List<object> _avgDurationByDay = new();
    private List<object> _towerHotspots = new();

    private Action _stateChangedHandler = null!;

    protected override void OnInitialized()
    {
        _stateChangedHandler = () => InvokeAsync(StateHasChanged);
        ExternalTriggerService.StateHasChangedAction += _stateChangedHandler;
        LoadStats();
    }

    private void LoadStats()
    {
        var from = _dateFrom ?? DateTime.MinValue;
        var to = _dateTo ?? DateTime.MaxValue;

        var games = GameHistoryService.GetGamesByDateRange(from, to);
        _hasData = games.Count > 0;
        if (!_hasData) { StateHasChanged(); return; }

        _totalGames = games.Count;
        _blueWins = games.Count(g => (TeamColor)g.Winner == TeamColor.BLUE);
        _redWins = games.Count(g => (TeamColor)g.Winner == TeamColor.RED);
        _avgDuration = games.Where(g => g.EndTime.HasValue).Select(g => g.Duration.TotalMinutes).DefaultIfEmpty(0).Average();

        _winRateByMode = GameHistoryService.GetWinRateByMode(from, to)
            .Select(kvp => (object)new { Mode = kvp.Key, Wins = kvp.Value })
            .ToList();

        _winRateBySide = GameHistoryService.GetWinRateBySide(from, to)
            .Select(kvp => (object)new { Side = kvp.Key, Wins = kvp.Value })
            .ToList();

        _deathsByDay = GameHistoryService.GetDeathsByDay(from, to)
            .Select(d => (object)new { Day = d.Day.ToString("dd.MM"), BlueDeaths = d.BlueDeaths, RedDeaths = d.RedDeaths })
            .ToList();

        _avgDurationByDay = GameHistoryService.GetAvgDurationByDay(from, to)
            .Select(d => (object)new { Day = d.Day.ToString("dd.MM"), Duration = d.AvgDuration })
            .ToList();

        _towerHotspots = GameHistoryService.GetGlobalTowerHotspots(from, to)
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => (object)new { Letter = kvp.Key, Captures = kvp.Value })
            .ToList();
    }

    public void Dispose()
    {
        ExternalTriggerService.StateHasChangedAction -= _stateChangedHandler;
    }
}
