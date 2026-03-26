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
    private MagnificationService? _magnificationService;

    // Configurable
    public double RingSize { get; set; } = 80;
    public double RingThickness { get; set; } = 3;
    public System.Windows.Media.Color RingColor { get; set; } =
        System.Windows.Media.Color.FromArgb(220, 65, 130, 220); // Blue

    public CursorHighlightService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Set the magnification service so the highlight window can be excluded from zoom.
    /// </summary>
    public void SetMagnificationService(MagnificationService service)
    {
        _magnificationService = service;
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

        // Window needs extra space for the glow shadow
        double glowRadius = 8;
        double windowSize = RingSize + RingThickness * 2 + glowRadius * 2 + 4;

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

        // Clean ring with subtle outer glow
        var ring = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            Stroke = new SolidColorBrush(RingColor),
            StrokeThickness = RingThickness,
            Fill = System.Windows.Media.Brushes.Transparent,
            Effect = new DropShadowEffect
            {
                Color = RingColor,
                BlurRadius = glowRadius,
                ShadowDepth = 0,
                Opacity = 0.6,
            },
        };

        RenderOptions.SetEdgeMode(ring, EdgeMode.Unspecified);

        double offset = (windowSize - RingSize) / 2;
        Canvas.SetLeft(ring, offset);
        Canvas.SetTop(ring, offset);
        canvas.Children.Add(ring);

        _overlayWindow.Content = canvas;
        _overlayWindow.Show();
        MakeClickThrough();

        // Register with magnifier so it's excluded from zoom
        var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
        _magnificationService?.ExcludeWindow(hwnd);

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
        if (_overlayWindow != null)
        {
            var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
            _magnificationService?.RemoveExcludedWindow(hwnd);
            _overlayWindow.Close();
            _overlayWindow = null;
        }
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
