using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class CursorHighlightService : IDisposable
{
    private Window? _overlayWindow;
    private readonly DispatcherTimer _timer;
    private bool _isActive;

    // Configurable
    public double RingSize { get; set; } = 40;

    public CursorHighlightService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Toggle()
    {
        if (_isActive)
            Deactivate();
        else
            Activate();
    }

    public bool IsActive => _isActive;

    private void Activate()
    {
        if (_overlayWindow != null) return;

        // The glow needs to be larger than the ring to avoid clipping
        double glowSize = RingSize * 3;
        double windowSize = glowSize + 20; // padding

        _overlayWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Width = windowSize,
            Height = windowSize,
            ResizeMode = ResizeMode.NoResize,
        };

        var canvas = new Canvas { Width = windowSize, Height = windowSize };

        // Outer soft glow — large radial gradient
        var outerGlow = new Ellipse
        {
            Width = glowSize,
            Height = glowSize,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromArgb(70, 255, 230, 80), 0.0),   // Warm center
                    new(System.Windows.Media.Color.FromArgb(40, 255, 220, 50), 0.3),    // Yellow mid
                    new(System.Windows.Media.Color.FromArgb(15, 255, 200, 30), 0.6),    // Fading
                    new(System.Windows.Media.Color.FromArgb(0, 255, 200, 0), 1.0),      // Transparent edge
                }
            },
            Stroke = null,
        };
        Canvas.SetLeft(outerGlow, (windowSize - glowSize) / 2);
        Canvas.SetTop(outerGlow, (windowSize - glowSize) / 2);
        canvas.Children.Add(outerGlow);

        // Inner bright core — smaller, more opaque
        double coreSize = RingSize * 1.2;
        var innerCore = new Ellipse
        {
            Width = coreSize,
            Height = coreSize,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromArgb(50, 255, 255, 200), 0.0),   // Bright white-yellow center
                    new(System.Windows.Media.Color.FromArgb(30, 255, 240, 100), 0.5),   // Warm yellow
                    new(System.Windows.Media.Color.FromArgb(0, 255, 220, 50), 1.0),     // Transparent
                }
            },
            Stroke = null,
            Effect = new BlurEffect { Radius = 5 },
        };
        Canvas.SetLeft(innerCore, (windowSize - coreSize) / 2);
        Canvas.SetTop(innerCore, (windowSize - coreSize) / 2);
        canvas.Children.Add(innerCore);

        // Enable anti-aliasing
        RenderOptions.SetEdgeMode(canvas, EdgeMode.Unspecified);

        _overlayWindow.Content = canvas;
        _overlayWindow.Show();
        MakeClickThrough();

        _isActive = true;
        _timer.Start();
    }

    private void MakeClickThrough()
    {
        if (_overlayWindow == null) return;
        var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
        int style = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE,
            style | (int)(User32.WS_EX_TRANSPARENT | User32.WS_EX_TOOLWINDOW));
    }

    private void Deactivate()
    {
        _timer.Stop();
        _overlayWindow?.Close();
        _overlayWindow = null;
        _isActive = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_overlayWindow == null) return;

        User32.GetCursorPos(out var cursor);

        var source = PresentationSource.FromVisual(_overlayWindow);
        double dpiScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dpiScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double halfW = _overlayWindow.Width / 2;
        double halfH = _overlayWindow.Height / 2;

        _overlayWindow.Left = cursor.X * dpiScaleX - halfW;
        _overlayWindow.Top = cursor.Y * dpiScaleY - halfH;
    }

    public void Dispose()
    {
        Deactivate();
    }
}
