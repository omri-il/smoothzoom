using System.Windows.Threading;

namespace SmoothAnnotate.Services;

public class StopwatchService
{
    private readonly DispatcherTimer _timer;
    private DateTime _startTime;
    private TimeSpan _elapsed;
    private DateTime _lastToggleTime;

    public bool IsRunning { get; private set; }

    public event Action<string>? TimeUpdated;

    public StopwatchService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTick;
    }

    public void ToggleStartPause()
    {
        var now = DateTime.UtcNow;

        // Double-tap detection: reset if pressed within 400ms
        if ((now - _lastToggleTime).TotalMilliseconds < 400)
        {
            Reset();
            _lastToggleTime = DateTime.MinValue;
            return;
        }

        _lastToggleTime = now;

        if (IsRunning)
        {
            // Pause: accumulate elapsed time
            _elapsed += DateTime.UtcNow - _startTime;
            _timer.Stop();
            IsRunning = false;
        }
        else
        {
            // Start/Resume
            _startTime = DateTime.UtcNow;
            _timer.Start();
            IsRunning = true;
        }
    }

    public void Reset()
    {
        _timer.Stop();
        IsRunning = false;
        _elapsed = TimeSpan.Zero;
        TimeUpdated?.Invoke("00:00.0");
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var total = _elapsed + (DateTime.UtcNow - _startTime);
        TimeUpdated?.Invoke(FormatTime(total));
    }

    private static string FormatTime(TimeSpan time)
    {
        int minutes = (int)time.TotalMinutes;
        int seconds = time.Seconds;
        int tenths = time.Milliseconds / 100;
        return $"{minutes:D2}:{seconds:D2}.{tenths}";
    }
}
