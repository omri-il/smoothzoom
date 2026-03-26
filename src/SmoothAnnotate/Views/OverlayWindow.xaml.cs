using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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

    // Current drawing color
    private Color _currentColor;

    // Shape drawing state
    private Point _shapeStartPoint;
    private Shape? _shapePreview;
    private bool _isDrawingShape;

    // Text tool state
    private TextBox? _activeTextBox;

    // Color palette
    private static readonly Color[] Palette = new[]
    {
        Color.FromRgb(255, 0, 0),     // 1: Red
        Color.FromRgb(50, 120, 255),   // 2: Blue
        Color.FromRgb(0, 200, 80),     // 3: Green
        Color.FromRgb(255, 255, 255),  // 4: White
        Color.FromRgb(255, 230, 0)     // 5: Yellow
    };

    public OverlayWindow() : this(new AnnotationSettings()) { }

    public OverlayWindow(AnnotationSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _currentColor = (Color)ColorConverter.ConvertFromString(_settings.PenColor);

        DrawCanvas.StrokeCollected += OnStrokeCollected;

        // Shape canvas mouse events
        ShapeCanvas.MouseLeftButtonDown += OnShapeMouseDown;
        ShapeCanvas.MouseMove += OnShapeMouseMove;
        ShapeCanvas.MouseLeftButtonUp += OnShapeMouseUp;

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

    // --- Draw Mode Toggle (Cycle: Pen -> Highlighter -> Eraser -> Off) ---

    public void ToggleDrawMode()
    {
        switch (_currentTool)
        {
            case AnnotationTool.None:
                EnterDrawMode();
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
            default: // Eraser, Laser, Arrow, Rectangle, Circle, Text -> Off
                ExitDrawMode();
                ShowModeIndicator("CLICK-THROUGH");
                break;
        }
    }

    // --- Shape Tool Toggles ---

    public void ToggleArrow()
    {
        if (_currentTool == AnnotationTool.Arrow)
        {
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Arrow);
            ShowModeIndicator("ARROW");
        }
    }

    public void ToggleRectangle()
    {
        if (_currentTool == AnnotationTool.Rectangle)
        {
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Rectangle);
            ShowModeIndicator("RECTANGLE");
        }
    }

    public void ToggleCircle()
    {
        if (_currentTool == AnnotationTool.Circle)
        {
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Circle);
            ShowModeIndicator("CIRCLE");
        }
    }

    public void ToggleText()
    {
        if (_currentTool == AnnotationTool.Text)
        {
            CommitActiveTextBox();
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Text);
            ShowModeIndicator("TEXT");
        }
    }

    public void ToggleLaser()
    {
        if (_currentTool == AnnotationTool.Laser)
        {
            SetTool(AnnotationTool.Pen);
            ShowModeIndicator("PEN");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Laser);
            ShowModeIndicator("LASER");
        }
    }

    // --- Color Switching ---

    public void SetColor(int index)
    {
        if (index < 1 || index > Palette.Length) return;
        _currentColor = Palette[index - 1];

        // Update current tool's drawing attributes if in ink mode
        if (_currentTool is AnnotationTool.Pen or AnnotationTool.Laser)
        {
            var attrs = DrawCanvas.DefaultDrawingAttributes.Clone();
            attrs.Color = _currentColor;
            DrawCanvas.DefaultDrawingAttributes = attrs;
        }

        string[] names = { "RED", "BLUE", "GREEN", "WHITE", "YELLOW" };
        ShowModeIndicator(names[index - 1]);
    }

    // --- Timer Controls ---

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

    // --- Clear ---

    public void ClearAllStrokes()
    {
        DrawCanvas.Strokes.Clear();
        ShapeCanvas.Children.Clear();
        _laserService?.ClearTracking();
        _shapePreview = null;
        _isDrawingShape = false;
        ShowModeIndicator("CLEARED");
    }

    // --- Internal Helpers ---

    private void EnterDrawMode()
    {
        if (!_isDrawMode)
        {
            _isDrawMode = true;
            OverlayService.RemoveClickThrough(_hwnd);
        }
    }

    private void ExitDrawMode()
    {
        _isDrawMode = false;
        SetTool(AnnotationTool.None);
        OverlayService.SetClickThrough(_hwnd);
    }

    private bool IsShapeTool(AnnotationTool tool) =>
        tool is AnnotationTool.Arrow or AnnotationTool.Rectangle or AnnotationTool.Circle or AnnotationTool.Text;

    private void SetTool(AnnotationTool tool)
    {
        // Clean up previous tool state
        CommitActiveTextBox();

        _currentTool = tool;

        if (IsShapeTool(tool))
        {
            // Shape tools: disable InkCanvas, enable ShapeCanvas
            DrawCanvas.EditingMode = InkCanvasEditingMode.None;
            ShapeCanvas.IsHitTestVisible = true;
            _laserService?.Stop();

            DrawCanvas.Cursor = tool == AnnotationTool.Text ? Cursors.IBeam : Cursors.Cross;
            ShapeCanvas.Cursor = tool == AnnotationTool.Text ? Cursors.IBeam : Cursors.Cross;
        }
        else
        {
            // Ink tools: enable InkCanvas, disable ShapeCanvas
            ShapeCanvas.IsHitTestVisible = false;

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
                        Color = _currentColor,
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
                        Color = _currentColor,
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
    }

    // --- InkCanvas Stroke Collected ---

    private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_currentTool == AnnotationTool.Laser)
        {
            _laserService?.RegisterStroke(e.Stroke);
        }
    }

    // --- Shape Drawing (Arrow, Rectangle, Circle) ---

    private void OnShapeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentTool == AnnotationTool.Text)
        {
            PlaceTextBox(e.GetPosition(ShapeCanvas));
            return;
        }

        if (!IsShapeTool(_currentTool) || _currentTool == AnnotationTool.Text) return;

        _shapeStartPoint = e.GetPosition(ShapeCanvas);
        _isDrawingShape = true;

        var brush = new SolidColorBrush(_currentColor);

        _shapePreview = _currentTool switch
        {
            AnnotationTool.Arrow => new Line
            {
                Stroke = brush,
                StrokeThickness = _settings.PenSize,
                X1 = _shapeStartPoint.X,
                Y1 = _shapeStartPoint.Y,
                X2 = _shapeStartPoint.X,
                Y2 = _shapeStartPoint.Y,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Triangle
            },
            AnnotationTool.Rectangle => new System.Windows.Shapes.Rectangle
            {
                Stroke = brush,
                StrokeThickness = _settings.PenSize,
                Fill = Brushes.Transparent,
                RadiusX = 2,
                RadiusY = 2
            },
            AnnotationTool.Circle => new Ellipse
            {
                Stroke = brush,
                StrokeThickness = _settings.PenSize,
                Fill = Brushes.Transparent
            },
            _ => null
        };

        if (_shapePreview != null)
        {
            if (_shapePreview is not Line)
            {
                Canvas.SetLeft(_shapePreview, _shapeStartPoint.X);
                Canvas.SetTop(_shapePreview, _shapeStartPoint.Y);
            }
            ShapeCanvas.Children.Add(_shapePreview);
            ShapeCanvas.CaptureMouse();
        }
    }

    private void OnShapeMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawingShape || _shapePreview == null) return;

        var currentPoint = e.GetPosition(ShapeCanvas);

        switch (_shapePreview)
        {
            case Line line:
                line.X2 = currentPoint.X;
                line.Y2 = currentPoint.Y;
                break;
            case System.Windows.Shapes.Rectangle rect:
                UpdateRectShape(rect, _shapeStartPoint, currentPoint);
                break;
            case Ellipse ellipse:
                UpdateRectShape(ellipse, _shapeStartPoint, currentPoint);
                break;
        }
    }

    private void OnShapeMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawingShape) return;

        _isDrawingShape = false;
        ShapeCanvas.ReleaseMouseCapture();

        // Finalize arrow: add arrowhead polygon
        if (_shapePreview is Line line)
        {
            AddArrowHead(line);
        }

        _shapePreview = null;
    }

    private static void UpdateRectShape(Shape shape, Point start, Point current)
    {
        double x = Math.Min(start.X, current.X);
        double y = Math.Min(start.Y, current.Y);
        double w = Math.Abs(current.X - start.X);
        double h = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
        shape.Width = Math.Max(w, 1);
        shape.Height = Math.Max(h, 1);
    }

    private void AddArrowHead(Line line)
    {
        double dx = line.X2 - line.X1;
        double dy = line.Y2 - line.Y1;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 5) return;

        double headSize = Math.Max(12, _settings.PenSize * 4);
        double angle = Math.Atan2(dy, dx);

        var p1 = new Point(
            line.X2 - headSize * Math.Cos(angle - 0.4),
            line.Y2 - headSize * Math.Sin(angle - 0.4));
        var p2 = new Point(
            line.X2 - headSize * Math.Cos(angle + 0.4),
            line.Y2 - headSize * Math.Sin(angle + 0.4));

        var arrowHead = new Polygon
        {
            Points = new PointCollection { new(line.X2, line.Y2), p1, p2 },
            Fill = new SolidColorBrush(_currentColor),
            Stroke = new SolidColorBrush(_currentColor),
            StrokeThickness = 1
        };
        ShapeCanvas.Children.Add(arrowHead);
    }

    // --- Text Tool ---

    private void PlaceTextBox(Point position)
    {
        CommitActiveTextBox();

        _activeTextBox = new TextBox
        {
            FontSize = 20,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_currentColor),
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            Padding = new Thickness(4, 2, 4, 2),
            MinWidth = 60,
            AcceptsReturn = false,
            CaretBrush = new SolidColorBrush(_currentColor)
        };

        Canvas.SetLeft(_activeTextBox, position.X);
        Canvas.SetTop(_activeTextBox, position.Y);
        ShapeCanvas.Children.Add(_activeTextBox);

        _activeTextBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter || args.Key == Key.Escape)
            {
                CommitActiveTextBox();
                args.Handled = true;
            }
        };

        _activeTextBox.Focus();
    }

    private void CommitActiveTextBox()
    {
        if (_activeTextBox == null) return;

        string text = _activeTextBox.Text;
        double left = Canvas.GetLeft(_activeTextBox);
        double top = Canvas.GetTop(_activeTextBox);
        var color = (_activeTextBox.Foreground as SolidColorBrush)?.Color ?? _currentColor;

        ShapeCanvas.Children.Remove(_activeTextBox);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 20,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false
            };
            textBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.7
            };
            Canvas.SetLeft(textBlock, left);
            Canvas.SetTop(textBlock, top);
            ShapeCanvas.Children.Add(textBlock);
        }

        _activeTextBox = null;
    }

    // --- Mode Indicator ---

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
