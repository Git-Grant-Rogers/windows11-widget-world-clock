namespace WorldClockWidget.Services;

/// <summary>
/// Fires a callback just after each wall-clock minute boundary while started. Aligning to the
/// boundary means the widget flips from 9:41 to 9:42 at the same moment the taskbar does,
/// instead of drifting by up to a minute.
/// </summary>
internal sealed class MinuteRefreshTimer : IDisposable
{
    private static readonly TimeSpan s_slack = TimeSpan.FromMilliseconds(250);

    private readonly Action _onTick;
    private readonly Timer _timer;
    private readonly Lock _gate = new();
    private bool _running;
    private bool _disposed;

    public MinuteRefreshTimer(Action onTick)
    {
        _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
        _timer = new Timer(_ => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _running)
            {
                return;
            }

            _running = true;
            Schedule();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _running = false;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _running = false;
            _timer.Dispose();
        }
    }

    internal static TimeSpan DelayUntilNextMinute(DateTimeOffset now)
    {
        DateTimeOffset next = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset).AddMinutes(1);
        TimeSpan delay = next - now + s_slack;
        return delay < s_slack ? s_slack : delay;
    }

    private void Schedule()
    {
        _timer.Change(DelayUntilNextMinute(DateTimeOffset.Now), Timeout.InfiniteTimeSpan);
    }

    private void Tick()
    {
        lock (_gate)
        {
            if (!_running || _disposed)
            {
                return;
            }
        }

        try
        {
            _onTick();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Minute refresh failed.", ex);
        }

        lock (_gate)
        {
            if (_running && !_disposed)
            {
                Schedule();
            }
        }
    }
}
