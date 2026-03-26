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
        };

        Loaded += (_, _) =>
        {
            // Position top-center of primary monitor work area
            var screen = SystemParameters.WorkArea;
            Left = screen.Left + (screen.Width - ActualWidth) / 2;
            Top = screen.Top + 10;
        };

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
        // Re-center the small dot
        var screen = SystemParameters.WorkArea;
        Left = screen.Left + (screen.Width - 42) / 2;
    }

    public void ExpandFromMinimal()
    {
        if (!_isCollapsed) return;
        MinimalButton.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;
        _isCollapsed = false;
        // Re-center the full toolbar
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var screen = SystemParameters.WorkArea;
            Left = screen.Left + (screen.Width - ActualWidth) / 2;
        }), System.Windows.Threading.DispatcherPriority.Loaded);
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

    private void MinimalButton_Click(object sender, MouseButtonEventArgs e)
    {
        ExpandFromMinimal();
        ToolSelected?.Invoke(AnnotationTool.Pen);
        e.Handled = true;
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
