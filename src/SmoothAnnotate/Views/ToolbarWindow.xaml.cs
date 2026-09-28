using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SmoothAnnotate.Models;
using SmoothAnnotate.Services;

namespace SmoothAnnotate.Views;

public partial class ToolbarWindow : Window
{
    private IntPtr _hwnd;
    private readonly Border[] _colorSwatches;
    private readonly Dictionary<AnnotationTool, Button> _toolButtons;
    private int _activeColorIndex = 1;
    private bool _isCollapsed;

    public event Action<AnnotationTool>? ToolSelected;
    public event Action<int>? ColorSelected;
    public event Action? ClearRequested;
    public event Action? TextSizeCycled;
    public event Action? CloseRequested;
    public event Action? ConfettiRequested;

    public ToolbarWindow()
    {
        InitializeComponent();

        _colorSwatches = new[] { Color1, Color2, Color3, Color4, Color5 };
        _toolButtons = new Dictionary<AnnotationTool, Button>
        {
            { AnnotationTool.Select, BtnSelect },
            { AnnotationTool.Pen, BtnPen },
            { AnnotationTool.Highlighter, BtnHighlighter },
            { AnnotationTool.Eraser, BtnEraser },
            { AnnotationTool.Laser, BtnLaser },
            { AnnotationTool.Arrow, BtnArrow },
            { AnnotationTool.Rectangle, BtnRect },
            { AnnotationTool.Circle, BtnCircle },
            { AnnotationTool.Text, BtnText },
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            OverlayService.HideFromAltTab(_hwnd);
            // It floats over the recorded screen: you see the toolbar and the dot, videos don't
            OverlayService.HideFromCapture(_hwnd, "Toolbar");
        };

        Loaded += (_, _) => MoveToTopCenter();

        SetActiveColor(1);
        SetActiveTool(AnnotationTool.None);
    }

    /// <summary>Force this window above the overlay using Win32 z-order.</summary>
    public void RaiseAboveOverlay()
    {
        if (_hwnd != IntPtr.Zero)
            OverlayService.RaiseToTop(_hwnd);
    }

    /// <summary>Returns screen-pixel bounds of the toolbar with padding for hover detection.</summary>
    public Rect GetScreenBounds()
    {
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        const double pad = 12;
        return new Rect(
            Left * dpiX - pad, Top * dpiY - pad,
            ActualWidth * dpiX + pad * 2, ActualHeight * dpiY + pad * 2);
    }

    // --- Collapse / Expand ---

    public void CollapseToMinimal()
    {
        MainContent.Visibility = Visibility.Collapsed;
        MinimalButton.Visibility = Visibility.Visible;
        _isCollapsed = true;
        // The dot sits wherever you last dragged it; the first time, top-center
        var screen = SystemParameters.WorkArea;
        var dot = LoadDotPosition();
        Left = dot?.X ?? screen.Left + (screen.Width - DotSize) / 2;
        Top = dot?.Y ?? screen.Top + 10;
    }

    public void ExpandFromMinimal()
    {
        if (!_isCollapsed) return;
        MinimalButton.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;
        _isCollapsed = false;
        // The full toolbar opens top-center, wherever the dot is
        Dispatcher.BeginInvoke(new Action(MoveToTopCenter),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void MoveToTopCenter()
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Left + (screen.Width - ActualWidth) / 2;
        Top = screen.Top + 10;
    }

    // --- The dot's place, kept across restarts (in DIPs, like Left/Top) ---

    private const double DotSize = 42;

    private static readonly string DotFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmoothAnnotate", "dot-position.json");

    private static Point? LoadDotPosition()
    {
        try
        {
            if (!File.Exists(DotFile)) return null;
            var p = JsonSerializer.Deserialize<Point>(File.ReadAllText(DotFile));
            // A monitor that has since gone would leave the dot off screen
            bool onScreen =
                p.X >= SystemParameters.VirtualScreenLeft &&
                p.Y >= SystemParameters.VirtualScreenTop &&
                p.X + DotSize <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                p.Y + DotSize <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
            return onScreen ? p : null;
        }
        catch
        {
            return null; // unreadable: back to top-center
        }
    }

    private static void SaveDotPosition(Point p)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DotFile)!);
            File.WriteAllText(DotFile, JsonSerializer.Serialize(p));
        }
        catch (Exception ex)
        {
            App.Log($"Could not save the dot's position: {ex.Message}");
        }
    }

    public bool IsCollapsed => _isCollapsed;

    // --- Event handlers ---

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr)
        {
            if (Enum.TryParse<AnnotationTool>(tagStr, out var tool))
                ToolSelected?.Invoke(tool);
        }
    }

    private void Color_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            ColorSelected?.Invoke(idx);
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        CloseRequested?.Invoke();
        e.Handled = true;
    }

    private void ConfettiButton_Click(object sender, RoutedEventArgs e)
    {
        ConfettiRequested?.Invoke();
    }

    private void TextSizeButton_Click(object sender, RoutedEventArgs e)
    {
        TextSizeCycled?.Invoke();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearRequested?.Invoke();
    }

    private void ClickThroughButton_Click(object sender, RoutedEventArgs e)
    {
        ToolSelected?.Invoke(AnnotationTool.None);
    }

    /// <summary>The dot: drag it to move it, click it to draw.</summary>
    private void MinimalButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        double left = Left, top = Top;
        try
        {
            DragMove(); // returns when the button is released
        }
        catch (InvalidOperationException)
        {
            // the button was already up (a quick tap) — a click
        }
        if (Math.Abs(Left - left) + Math.Abs(Top - top) > 4)
        {
            SaveDotPosition(new Point(Left, Top));
            return;
        }
        ExpandFromMinimal();
        ToolSelected?.Invoke(AnnotationTool.Pen);
    }

    // --- Visual state ---

    public void UpdateTextSizeLabel(string label)
    {
        BtnTextSize.ToolTip = $"Text Size: {label}";
        TextSizeLabel.Text = label.Length <= 2 ? label : label[..1].ToUpper();
    }

    public void SetActiveTool(AnnotationTool tool)
    {
        // Highlight active tool button
        foreach (var (t, btn) in _toolButtons)
        {
            if (t == tool)
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(50, 80, 150, 255));
                btn.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = new SolidColorBrush(Color.FromArgb(187, 255, 255, 255));
            }
        }

        // Highlight Pointer button when in mouse/click-through mode
        if (tool == AnnotationTool.None)
        {
            BtnClickThrough.Background = new SolidColorBrush(Color.FromArgb(50, 80, 150, 255));
        }
        else
        {
            BtnClickThrough.Background = Brushes.Transparent;
        }
    }

    public void SetActiveColor(int index)
    {
        _activeColorIndex = index;
        for (int i = 0; i < _colorSwatches.Length; i++)
        {
            _colorSwatches[i].BorderBrush = (i + 1) == index
                ? new SolidColorBrush(Colors.White)
                : Brushes.Transparent;
        }
    }
}
