using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services.Interfaces;
using Radzen.Blazor;

namespace OWLServer.Components.Pages.AdminPages;

public class ChartItem
{
    public string Category { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class DeathsByDayItem
{
    public string Day { get; set; } = string.Empty;
    public int BlueDeaths { get; set; }
    public int RedDeaths { get; set; }
}

public class DurationByDayItem
{
    public string Day { get; set; } = string.Empty;
    public double Duration { get; set; }
}

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

    private List<ChartItem> _winRateByMode = new();
    private List<ChartItem> _winRateBySide = new();
    private List<DeathsByDayItem> _deathsByDay = new();
    private List<DurationByDayItem> _avgDurationByDay = new();
    private List<ChartItem> _towerHotspots = new();

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
            .Select(kvp => new ChartItem { Category = kvp.Key, Value = kvp.Value })
            .ToList();

        _winRateBySide = GameHistoryService.GetWinRateBySide(from, to)
            .Select(kvp => new ChartItem { Category = kvp.Key, Value = kvp.Value })
            .ToList();

        _deathsByDay = GameHistoryService.GetDeathsByDay(from, to)
            .Select(d => new DeathsByDayItem { Day = d.Day.ToString("dd.MM"), BlueDeaths = d.BlueDeaths, RedDeaths = d.RedDeaths })
            .ToList();

        _avgDurationByDay = GameHistoryService.GetAvgDurationByDay(from, to)
            .Select(d => new DurationByDayItem { Day = d.Day.ToString("dd.MM"), Duration = d.AvgDuration })
            .ToList();

        _towerHotspots = GameHistoryService.GetGlobalTowerHotspots(from, to)
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new ChartItem { Category = kvp.Key, Value = kvp.Value })
            .ToList();
    }

    public void Dispose()
    {
        ExternalTriggerService.StateHasChangedAction -= _stateChangedHandler;
    }
}
