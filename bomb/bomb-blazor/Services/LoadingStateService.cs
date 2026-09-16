#nullable enable
namespace Bomb.Services;

public class LoadingStateService : IDisposable
{
    private Timer? _timer;
    private decimal _progress;
    private bool _isLoading;
    private readonly object _lock = new();

    public decimal Progress => _progress;
    public bool IsLoading => _isLoading;

    public event Action? OnChange;

    public void StartLoading()
    {
        lock (_lock)
        {
            if (_isLoading) return;
            _isLoading = true;
            _timer?.Dispose();
            _timer = new Timer(_ =>
            {
                lock (_lock)
                {
                    if (_progress < 100)
                        _progress++;
                    NotifyStateChanged();
                }
            }, null, 100, 100);
            NotifyStateChanged();
        }
    }

    public void StopLoading()
    {
        lock (_lock)
        {
            _isLoading = false;
            _timer?.Dispose();
            _timer = new Timer(_ =>
            {
                lock (_lock)
                {
                    if (_progress > 0)
                    {
                        _progress--;
                        NotifyStateChanged();
                    }
                    else
                    {
                        _timer?.Dispose();
                        _timer = null;
                    }
                }
            }, null, 100, 100);
            NotifyStateChanged();
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _isLoading = false;
            _timer?.Dispose();
            _timer = null;
            _progress = 0;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        lock (_lock)
        {
            _timer?.Dispose();
        }
    }
}
