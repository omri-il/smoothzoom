using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Threading;

namespace SmoothAnnotate.Services;

public class LaserService
{
    private readonly InkCanvas _inkCanvas;
    private readonly DispatcherTimer _fadeTimer;
    private readonly List<(Stroke Stroke, DateTime Created)> _laserStrokes = new();
    private readonly int _fadeMs;
    private readonly Color _baseColor;

    public LaserService(InkCanvas inkCanvas, int fadeMs, Color baseColor)
    {
        _inkCanvas = inkCanvas;
        _fadeMs = fadeMs;
        _baseColor = baseColor;

        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _fadeTimer.Tick += OnFadeTick;
    }

    public void Start()
    {
        _fadeTimer.Start();
    }

    public void Stop()
    {
        _fadeTimer.Stop();
        RemoveAllLaserStrokes();
    }

    public void RegisterStroke(Stroke stroke)
    {
        _laserStrokes.Add((stroke, DateTime.UtcNow));
    }

    public void ClearTracking()
    {
        _laserStrokes.Clear();
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        for (int i = _laserStrokes.Count - 1; i >= 0; i--)
        {
            var (stroke, created) = _laserStrokes[i];
            double ageMs = (now - created).TotalMilliseconds;

            if (ageMs >= _fadeMs)
            {
                // Fully faded - remove from canvas and tracking
                _inkCanvas.Strokes.Remove(stroke);
                _laserStrokes.RemoveAt(i);
            }
            else
            {
                // Calculate remaining opacity
                byte alpha = (byte)(255 * (1.0 - ageMs / _fadeMs));
                var fadedColor = Color.FromArgb(alpha, _baseColor.R, _baseColor.G, _baseColor.B);

                stroke.DrawingAttributes.Color = fadedColor;
            }
        }
    }

    private void RemoveAllLaserStrokes()
    {
        foreach (var (stroke, _) in _laserStrokes)
        {
            _inkCanvas.Strokes.Remove(stroke);
        }
        _laserStrokes.Clear();
    }
}
