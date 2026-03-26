using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmoothAnnotate.Services;

public class LaserService
{
    private readonly InkCanvas _inkCanvas;
    private readonly DispatcherTimer _fadeTimer;
    private readonly List<(Stroke Glow, Stroke Core, DateTime Created)> _laserStrokes = new();
    private readonly int _fadeMs;
    private readonly double _coreSize;
    private Color _baseColor;

    public LaserService(InkCanvas inkCanvas, int fadeMs, Color baseColor, double coreSize)
    {
        _inkCanvas = inkCanvas;
        _fadeMs = fadeMs;
        _baseColor = baseColor;
        _coreSize = coreSize;

        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _fadeTimer.Tick += OnFadeTick;
    }

    public void Start() => _fadeTimer.Start();

    public void Stop()
    {
        _fadeTimer.Stop();
        RemoveAllLaserStrokes();
    }

    public void SetBaseColor(Color color) => _baseColor = color;

    public void RegisterStroke(Stroke glowStroke)
    {
        // The incoming stroke IS the glow (wide, semi-transparent, drawn live).
        // Create a thin bright CORE stroke on top for a crisp center.
        var coreAttrs = new DrawingAttributes
        {
            Color = _baseColor,
            Width = _coreSize,
            Height = _coreSize,
            FitToCurve = true,
            StylusTip = StylusTip.Ellipse,
            IgnorePressure = true
        };

        var coreStroke = new Stroke(glowStroke.StylusPoints.Clone(), coreAttrs);
        _inkCanvas.Strokes.Add(coreStroke);

        _laserStrokes.Add((glowStroke, coreStroke, DateTime.UtcNow));
    }

    public void ClearTracking() => _laserStrokes.Clear();

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        for (int i = _laserStrokes.Count - 1; i >= 0; i--)
        {
            var (glow, core, created) = _laserStrokes[i];
            double ageMs = (now - created).TotalMilliseconds;

            if (ageMs >= _fadeMs)
            {
                _inkCanvas.Strokes.Remove(glow);
                _inkCanvas.Strokes.Remove(core);
                _laserStrokes.RemoveAt(i);
            }
            else
            {
                double progress = ageMs / _fadeMs;
                byte coreAlpha = (byte)(255 * (1.0 - progress));
                byte glowAlpha = (byte)(80 * (1.0 - progress));

                core.DrawingAttributes.Color = Color.FromArgb(coreAlpha, _baseColor.R, _baseColor.G, _baseColor.B);
                glow.DrawingAttributes.Color = Color.FromArgb(glowAlpha, _baseColor.R, _baseColor.G, _baseColor.B);
            }
        }
    }

    private void RemoveAllLaserStrokes()
    {
        foreach (var (glow, core, _) in _laserStrokes)
        {
            _inkCanvas.Strokes.Remove(glow);
            _inkCanvas.Strokes.Remove(core);
        }
        _laserStrokes.Clear();
    }
}
