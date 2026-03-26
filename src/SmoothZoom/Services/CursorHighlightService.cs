using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class CursorHighlightService : IDisposable
{
    private Window? _overlayWindow;
    private Ellipse? _ring;
    private Ellipse? _fill;
    private readonly DispatcherTimer _timer;
    private bool _isActive;

    // Configurable
    public double RingSize { get; set; } = 40;
    public double RingThickness { get; set; } = 3;
    public System.Windows.Media.Color RingColor { get; set; } = System.Windows.Media.Color.FromArgb(200, 255, 220, 50); // Yellow
    public System.Windows.Media.Color FillColor { get; set; } = System.Windows.Media.Color.FromArgb(40, 255, 220, 50);  // Subtle fill
    public bool ShowFill { get; set; } = true;

    // Win32 constants for click-through window
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    public CursorHighlightService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
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

        _overlayWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Width = RingSize + RingThickness * 2,
            Height = RingSize + RingThickness * 2,
            ResizeMode = ResizeMode.NoResize,
        };

        var canvas = new System.Windows.Controls.Canvas();

        // Fill circle (optional subtle highlight)
        if (ShowFill)
        {
            _fill = new Ellipse
            {
                Width = RingSize,
                Height = RingSize,
                Fill = new SolidColorBrush(FillColor),
            };
            System.Windows.Controls.Canvas.SetLeft(_fill, RingThickness);
            System.Windows.Controls.Canvas.SetTop(_fill, RingThickness);
            canvas.Children.Add(_fill);
        }

        // Ring outline
        _ring = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            Stroke = new SolidColorBrush(RingColor),
            StrokeThickness = RingThickness,
            Fill = System.Windows.Media.Brushes.Transparent,
        };
        System.Windows.Controls.Canvas.SetLeft(_ring, RingThickness);
        System.Windows.Controls.Canvas.SetTop(_ring, RingThickness);
        canvas.Children.Add(_ring);

        _overlayWindow.Content = canvas;
        _overlayWindow.Show();

        // Make click-through and hide from alt-tab
        MakeClickThrough();

        _isActive = true;
        _timer.Start();
    }

    private void MakeClickThrough()
    {
        if (_overlayWindow == null) return;

        var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    private void Deactivate()
    {
        _timer.Stop();
        _overlayWindow?.Close();
        _overlayWindow = null;
        _ring = null;
        _fill = null;
        _isActive = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_overlayWindow == null) return;

        User32.GetCursorPos(out var cursor);

        // Convert physical pixels to WPF device-independent units
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
