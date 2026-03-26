using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SmoothAnnotate.Models;
using SmoothAnnotate.Services;

namespace SmoothAnnotate.Views;

public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;
    private bool _isDrawMode;
    private AnnotationTool _currentTool = AnnotationTool.None;
    private readonly AnnotationSettings _settings;
    private LaserService? _laserService;
    private StopwatchService? _stopwatchService;

    public OverlayWindow() : this(new AnnotationSettings()) { }

    public OverlayWindow(AnnotationSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        DrawCanvas.StrokeCollected += OnStrokeCollected;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    public void InitializeServices(LaserService laserService, StopwatchService stopwatchService)
    {
        _laserService = laserService;
        _stopwatchService = stopwatchService;
        _stopwatchService.TimeUpdated += time => Dispatcher.Invoke(() => TimerText.Text = time);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        OverlayService.SetClickThrough(_hwnd);
        OverlayService.HideFromAltTab(_hwnd);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public void ToggleDrawMode()
    {
        // Cycle: None -> Pen -> Highlighter -> Eraser -> None
        switch (_currentTool)
        {
            case AnnotationTool.None:
                _isDrawMode = true;
                OverlayService.RemoveClickThrough(_hwnd);
                SetTool(AnnotationTool.Pen);
                ShowModeIndicator("PEN");
                break;
            case AnnotationTool.Pen:
                SetTool(AnnotationTool.Highlighter);
                ShowModeIndicator("HIGHLIGHTER");
                break;
            case AnnotationTool.Highlighter:
                SetTool(AnnotationTool.Eraser);
                ShowModeIndicator("ERASER");
                break;
            case AnnotationTool.Eraser:
            case AnnotationTool.Laser:
                _isDrawMode = false;
                SetTool(AnnotationTool.None);
                OverlayService.SetClickThrough(_hwnd);
                ShowModeIndicator("CLICK-THROUGH");
                break;
        }
    }

    public void ClearAllStrokes()
    {
        DrawCanvas.Strokes.Clear();
        _laserService?.ClearTracking();
        ShowModeIndicator("CLEARED");
    }

    public void ToggleLaser()
    {
        if (_currentTool == AnnotationTool.Laser)
        {
            // Toggle back to pen
            SetTool(AnnotationTool.Pen);
            ShowModeIndicator("PEN");
        }
        else
        {
            // Enter laser mode (auto-enter draw mode if needed)
            if (!_isDrawMode)
            {
                _isDrawMode = true;
                OverlayService.RemoveClickThrough(_hwnd);
            }
            SetTool(AnnotationTool.Laser);
            ShowModeIndicator("LASER");
        }
    }

    public void ToggleTimer()
    {
        _stopwatchService?.ToggleStartPause();
        TimerContainer.Visibility = Visibility.Visible;
        ShowModeIndicator(_stopwatchService?.IsRunning == true ? "TIMER STARTED" : "TIMER PAUSED");
    }

    public void ToggleTimerVisibility()
    {
        TimerContainer.Visibility = TimerContainer.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void SetTool(AnnotationTool tool)
    {
        _currentTool = tool;

        switch (tool)
        {
            case AnnotationTool.None:
                DrawCanvas.EditingMode = InkCanvasEditingMode.None;
                DrawCanvas.Cursor = Cursors.Arrow;
                _laserService?.Stop();
                break;

            case AnnotationTool.Pen:
                DrawCanvas.EditingMode = InkCanvasEditingMode.Ink;
                DrawCanvas.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = (Color)ColorConverter.ConvertFromString(_settings.PenColor),
                    Width = _settings.PenSize,
                    Height = _settings.PenSize,
                    FitToCurve = true,
                    StylusTip = StylusTip.Ellipse,
                    IgnorePressure = false
                };
                DrawCanvas.Cursor = Cursors.Pen;
                _laserService?.Stop();
                break;

            case AnnotationTool.Highlighter:
                DrawCanvas.EditingMode = InkCanvasEditingMode.Ink;
                DrawCanvas.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = Color.FromArgb(100, 255, 255, 0),
                    Width = _settings.HighlighterSize,
                    Height = _settings.HighlighterSize,
                    FitToCurve = true,
                    StylusTip = StylusTip.Rectangle,
                    IgnorePressure = true,
                    IsHighlighter = true
                };
                DrawCanvas.Cursor = Cursors.Hand;
                _laserService?.Stop();
                break;

            case AnnotationTool.Eraser:
                DrawCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                DrawCanvas.Cursor = Cursors.Cross;
                _laserService?.Stop();
                break;

            case AnnotationTool.Laser:
                DrawCanvas.EditingMode = InkCanvasEditingMode.Ink;
                DrawCanvas.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = (Color)ColorConverter.ConvertFromString(_settings.LaserColor),
                    Width = _settings.LaserSize,
                    Height = _settings.LaserSize,
                    FitToCurve = true,
                    StylusTip = StylusTip.Ellipse,
                    IgnorePressure = true
                };
                DrawCanvas.Cursor = Cursors.Cross;
                _laserService?.Start();
                break;
        }
    }

    private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_currentTool == AnnotationTool.Laser)
        {
            _laserService?.RegisterStroke(e.Stroke);
        }
    }

    private void ShowModeIndicator(string text)
    {
        ModeText.Text = text;
        ModeIndicator.Opacity = 1.0;

        var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(1200))
        {
            BeginTime = TimeSpan.FromMilliseconds(400)
        };
        ModeIndicator.BeginAnimation(OpacityProperty, fade);
    }
}
