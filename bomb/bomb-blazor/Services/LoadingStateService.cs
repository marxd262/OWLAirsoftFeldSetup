#nullable enable
namespace Bomb.Services;

public class LoadingStateService : IDisposable
{
    private Timer? _timer;
    private decimal _progress;
    private bool _isLoading;

    public decimal Progress => _progress;
    public bool IsLoading => _isLoading;

    public event Action? OnChange;

    public void StartLoading()
    {
        if (_isLoading) return;
        _isLoading = true;
        _timer?.Dispose();
        _timer = new Timer(_ =>
        {
            if (_progress < 100)
                _progress++;
            NotifyStateChanged();
        }, null, 100, 100);
        NotifyStateChanged();
    }

    public void StopLoading()
    {
        _isLoading = false;
        _timer?.Dispose();
        _timer = new Timer(_ =>
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
        }, null, 100, 100);
        NotifyStateChanged();
    }

    public void Reset()
    {
        _isLoading = false;
        _timer?.Dispose();
        _timer = null;
        _progress = 0;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
