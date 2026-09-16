#nullable enable
using Bomb.Components.Shared;

namespace Bomb.Services;

public class DefuseService : IDisposable
{
    private const double DefuseDurationSeconds = 10;
    private Timer? _timer;
    private DateTime _defuseStart;
    private DefuseState _state = DefuseState.Idle;
    private readonly object _lock = new();

    public DefuseState State => _state;
    public double DefuseProgress { get; private set; }

    public event Action? OnChange;

    public void StartDefuse()
    {
        lock (_lock)
        {
            if (_state != DefuseState.Idle) return;

            _state = DefuseState.Defusing;
            _defuseStart = DateTime.UtcNow;
            DefuseProgress = 0;

            _timer?.Dispose();
            _timer = new Timer(_ =>
            {
                lock (_lock)
                {
                    var elapsed = (DateTime.UtcNow - _defuseStart).TotalSeconds;
                    DefuseProgress = Math.Min(elapsed / DefuseDurationSeconds, 1.0);

                    if (elapsed >= DefuseDurationSeconds)
                    {
                        _state = DefuseState.Defused;
                        DefuseProgress = 1.0;
                        _timer?.Dispose();
                        _timer = null;
                    }

                    NotifyStateChanged();
                }
            }, null, 0, 50);

            NotifyStateChanged();
        }
    }

    public void StopDefuse()
    {
        lock (_lock)
        {
            if (_state != DefuseState.Defusing) return;

            _state = DefuseState.Idle;
            DefuseProgress = 0;
            _timer?.Dispose();
            _timer = null;
            NotifyStateChanged();
        }
    }

    public void NotifyDetonation()
    {
        lock (_lock)
        {
            _state = DefuseState.Detonated;
            DefuseProgress = 0;
            _timer?.Dispose();
            _timer = null;
            NotifyStateChanged();
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _state = DefuseState.Idle;
            DefuseProgress = 0;
            _timer?.Dispose();
            _timer = null;
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
