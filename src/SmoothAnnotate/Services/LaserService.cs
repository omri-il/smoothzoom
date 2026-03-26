using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmoothAnnotate.Services;

public class LaserService
{
    private readonly InkCanvas _inkCanvas;
    private readonly DispatcherTimer _fadeTimer;
    private readonly List<(Stroke Main, Stroke Glow, DateTime Created)> _laserStrokes = new();
    private readonly int _fadeMs;
    private Color _baseColor;

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

    public void SetBaseColor(Color color)
    {
        _baseColor = color;
    }

    public void RegisterStroke(Stroke stroke)
    {
        // Create a wider glow stroke underneath the main stroke
        var glowAttrs = new DrawingAttributes
        {
            Color = Color.FromArgb(80, _baseColor.R, _baseColor.G, _baseColor.B),
            Width = stroke.DrawingAttributes.Width * 3.5,
            Height = stroke.DrawingAttributes.Height * 3.5,
            FitToCurve = true,
            StylusTip = StylusTip.Ellipse,
            IgnorePressure = true
        };

        var glowStroke = new Stroke(stroke.StylusPoints.Clone(), glowAttrs);

        // Insert glow BEFORE main stroke so it renders underneath
        int mainIndex = _inkCanvas.Strokes.IndexOf(stroke);
        if (mainIndex >= 0)
            _inkCanvas.Strokes.Insert(mainIndex, glowStroke);
        else
            _inkCanvas.Strokes.Add(glowStroke);

        _laserStrokes.Add((stroke, glowStroke, DateTime.UtcNow));
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
            var (main, glow, created) = _laserStrokes[i];
            double ageMs = (now - created).TotalMilliseconds;

            if (ageMs >= _fadeMs)
            {
                _inkCanvas.Strokes.Remove(main);
                _inkCanvas.Strokes.Remove(glow);
                _laserStrokes.RemoveAt(i);
            }
            else
            {
                double progress = ageMs / _fadeMs;
                byte mainAlpha = (byte)(255 * (1.0 - progress));
                byte glowAlpha = (byte)(80 * (1.0 - progress));

                main.DrawingAttributes.Color = Color.FromArgb(mainAlpha, _baseColor.R, _baseColor.G, _baseColor.B);
                glow.DrawingAttributes.Color = Color.FromArgb(glowAlpha, _baseColor.R, _baseColor.G, _baseColor.B);
            }
        }
    }

    private void RemoveAllLaserStrokes()
    {
        foreach (var (main, glow, _) in _laserStrokes)
        {
            _inkCanvas.Strokes.Remove(main);
            _inkCanvas.Strokes.Remove(glow);
        }
        _laserStrokes.Clear();
    }
}
