using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class CursorHighlightService : IDisposable
{
    private Window? _overlayWindow;
    private Canvas? _canvas;
    private readonly DispatcherTimer _timer;
    private bool _isActive;
    private MagnificationService? _magnificationService;

    // Configurable
    public double RingSize { get; set; } = 70;
    public double RingThickness { get; set; } = 4;
    public System.Windows.Media.Color RingColor { get; set; } =
        System.Windows.Media.Color.FromArgb(220, 255, 230, 50); // Yellow
    public System.Windows.Media.Color RingFill { get; set; } =
        System.Windows.Media.Color.FromArgb(48, 255, 230, 50);  // Faint yellow
    public bool ClickRipple { get; set; } = true;

    // Ripple grows from the ring to RippleScale × ring, fading out
    private const double RippleScale = 2.0;
    private const int RippleMs = 350;
    private static readonly System.Windows.Media.Color RightClickColor =
        System.Windows.Media.Color.FromArgb(230, 255, 80, 80); // Red

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
        // When magnifier creates its window, bring ring back to front
        _magnificationService.MagnifierWindowCreated += BringToFront;
    }

    private void BringToFront()
    {
        if (_overlayWindow == null) return;
        _overlayWindow.Topmost = false;
        _overlayWindow.Topmost = true;
    }

    public void Toggle()
    {
        if (_isActive)
            Deactivate();
        else
            Activate();
    }

    public void SetActive(bool active)
    {
        if (active) Activate();
        else Deactivate();
    }

    public bool IsActive => _isActive;

    /// <summary>
    /// Rebuild the ring so new size/colour settings take effect immediately.
    /// </summary>
    public void Refresh()
    {
        if (!_isActive) return;
        Deactivate();
        Activate();
    }

    private void Activate()
    {
        if (_overlayWindow != null) return;

        // Window needs extra space for the glow shadow and the click ripple
        double glowRadius = 8;
        double windowSize = RingSize * RippleScale + RingThickness * 2 + glowRadius * 2 + 4;

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

        _canvas = new Canvas { Width = windowSize, Height = windowSize };

        // Clean ring with subtle outer glow
        var ring = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            Stroke = new SolidColorBrush(RingColor),
            StrokeThickness = RingThickness,
            Fill = new SolidColorBrush(RingFill),
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
        _canvas.Children.Add(ring);

        _overlayWindow.Content = _canvas;
        _overlayWindow.Show();
        MakeClickThrough();

        // Register with magnifier so it's excluded from zoom
        var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
        _magnificationService?.ExcludeWindow(hwnd);

        _isActive = true;
        OnTimerTick(null, EventArgs.Empty); // place it before the first frame
        _timer.Start();
    }

    /// <summary>
    /// A short expanding pulse at the cursor. Must be called on the UI thread.
    /// </summary>
    public void ShowClick(bool rightButton)
    {
        if (!_isActive || !ClickRipple || _canvas == null) return;

        var color = rightButton ? RightClickColor : RingColor;
        var ripple = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = RingThickness,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            IsHitTestVisible = false,
        };
        var scale = new ScaleTransform(1, 1);
        ripple.RenderTransform = scale;

        double offset = (_canvas.Width - RingSize) / 2;
        Canvas.SetLeft(ripple, offset);
        Canvas.SetTop(ripple, offset);
        _canvas.Children.Add(ripple);

        var duration = TimeSpan.FromMilliseconds(RippleMs);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var grow = new DoubleAnimation(1, RippleScale, duration) { EasingFunction = ease };
        var fade = new DoubleAnimation(1, 0, duration);
        var canvas = _canvas;
        fade.Completed += (_, _) => canvas.Children.Remove(ripple);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        ripple.BeginAnimation(UIElement.OpacityProperty, fade);
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
            _canvas = null;
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
