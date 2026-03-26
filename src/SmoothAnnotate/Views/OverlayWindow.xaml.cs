using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SmoothAnnotate.Models;
using SmoothAnnotate.Services;

namespace SmoothAnnotate.Views;

public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;
    private bool _isDrawMode;
    private bool _isOverToolbar; // tracks if overlay is currently click-through for toolbar
    private AnnotationTool _currentTool = AnnotationTool.None;
    private readonly AnnotationSettings _settings;
    private LaserService? _laserService;
    private StopwatchService? _stopwatchService;
    private ToastWindow? _toast;
    private ToolbarWindow? _toolbar;
    private readonly System.Windows.Threading.DispatcherTimer _toolbarHitTimer;

    // Current drawing color
    private Color _currentColor;

    // Shape drawing state
    private Point _shapeStartPoint;
    private Shape? _shapePreview;
    private bool _isDrawingShape;

    // Text tool state
    private TextBox? _activeTextBox;
    private double _textSize = 32; // Medium default
    private static readonly double[] TextSizes = { 24, 32, 48 }; // Small, Medium, Large
    private int _textSizeIndex = 1; // Start at Medium

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

        _toast = new ToastWindow();
        _toast.Show();

        _toolbar = new ToolbarWindow();
        _toolbar.ToolSelected += OnToolbarToolSelected;
        _toolbar.ColorSelected += SetColor;
        _toolbar.ClearRequested += ClearAllStrokes;
        _toolbar.TextSizeCycled += () =>
        {
            CycleTextSize();
            string[] labels = { "Small", "Medium", "Large" };
            _toolbar?.UpdateTextSizeLabel(labels[_textSizeIndex]);
        };
        _toolbar.Show();

        // Timer to detect when cursor is over toolbar and pass clicks through
        _toolbarHitTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _toolbarHitTimer.Tick += OnToolbarHitCheck;
    }

    private void OnToolbarToolSelected(AnnotationTool tool)
    {
        if (tool == AnnotationTool.None)
        {
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(tool);
            ShowModeIndicator(tool.ToString().ToUpper());
        }
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
        OverlayService.SetNoActivate(_hwnd);
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
        _toolbar?.SetActiveColor(index);

        // Update current tool's drawing attributes and cursor
        if (_currentTool is AnnotationTool.Pen or AnnotationTool.Laser)
        {
            var attrs = DrawCanvas.DefaultDrawingAttributes.Clone();
            attrs.Color = _currentColor;
            DrawCanvas.DefaultDrawingAttributes = attrs;
            DrawCanvas.Cursor = CreateCircleCursor(_currentColor, attrs.Width);
        }
        // Keep laser fade color in sync
        _laserService?.SetBaseColor(_currentColor);

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
            _isOverToolbar = false;
            RepositionToCurrentMonitor();
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            OverlayService.RemoveClickThrough(_hwnd);
            _toolbar?.RaiseAboveOverlay();
            _toolbarHitTimer.Start();
        }
    }

    private void ExitDrawMode()
    {
        _isDrawMode = false;
        _isOverToolbar = false;
        _toolbarHitTimer.Stop();
        SetTool(AnnotationTool.None);
        Background = Brushes.Transparent;
        OverlayService.SetClickThrough(_hwnd);
        // Expand back to full virtual screen for click-through mode
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void RepositionToCurrentMonitor()
    {
        Native.User32.GetCursorPos(out var pt);
        var hMonitor = Native.User32.MonitorFromPoint(pt, Native.User32.MONITOR_DEFAULTTONEAREST);

        var mi = new Native.User32.MONITORINFO();
        mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(mi);

        if (Native.User32.GetMonitorInfo(hMonitor, ref mi))
        {
            // Convert screen pixels to WPF device-independent units
            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            Left = mi.rcMonitor.Left * dpiX;
            Top = mi.rcMonitor.Top * dpiY;
            Width = (mi.rcMonitor.Right - mi.rcMonitor.Left) * dpiX;
            Height = (mi.rcMonitor.Bottom - mi.rcMonitor.Top) * dpiY;
        }
    }

    private void OnToolbarHitCheck(object? sender, EventArgs e)
    {
        if (!_isDrawMode || _toolbar == null) return;

        Native.User32.GetCursorPos(out var pt);
        var toolbarBounds = _toolbar.GetScreenBounds();
        bool cursorOverToolbar = toolbarBounds.Contains(pt.X, pt.Y);

        if (cursorOverToolbar && !_isOverToolbar)
        {
            // Cursor entered toolbar area - let clicks pass through to toolbar
            _isOverToolbar = true;
            OverlayService.SetClickThrough(_hwnd);
            _toolbar.RaiseAboveOverlay();
        }
        else if (!cursorOverToolbar && _isOverToolbar)
        {
            // Cursor left toolbar area - resume capturing for drawing
            _isOverToolbar = false;
            OverlayService.RemoveClickThrough(_hwnd);
        }
    }

    private bool IsShapeTool(AnnotationTool tool) =>
        tool is AnnotationTool.Arrow or AnnotationTool.Rectangle or AnnotationTool.Circle or AnnotationTool.Text;

    private void SetTool(AnnotationTool tool)
    {
        // Clean up previous tool state
        CommitActiveTextBox();

        _currentTool = tool;
        _toolbar?.SetActiveTool(tool);

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
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(_currentColor, _settings.PenSize);
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
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(Color.FromRgb(255, 255, 0), _settings.HighlighterSize);
                    _laserService?.Stop();
                    break;

                case AnnotationTool.Eraser:
                    DrawCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(Color.FromRgb(200, 200, 200), _settings.EraserSize);
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
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(_currentColor, _settings.LaserSize);
                    _laserService?.SetBaseColor(_currentColor);
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
        else if (_currentTool == AnnotationTool.Pen)
        {
            // Add subtle shadow under pen strokes for a polished look
            var shadowAttrs = new DrawingAttributes
            {
                Color = Color.FromArgb(40, 0, 0, 0),
                Width = e.Stroke.DrawingAttributes.Width + 4,
                Height = e.Stroke.DrawingAttributes.Height + 4,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse,
                IgnorePressure = true
            };
            var shadow = new System.Windows.Ink.Stroke(e.Stroke.StylusPoints.Clone(), shadowAttrs);

            int idx = DrawCanvas.Strokes.IndexOf(e.Stroke);
            if (idx >= 0)
                DrawCanvas.Strokes.Insert(idx, shadow);
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
            FontSize = _textSize,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_currentColor),
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4),
            MinWidth = 80,
            AcceptsReturn = false,
            CaretBrush = new SolidColorBrush(_currentColor),
            FlowDirection = System.Windows.FlowDirection.RightToLeft,
            TextAlignment = TextAlignment.Right
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
            // Detect Hebrew characters for RTL support
            bool hasHebrew = text.Any(c => c >= '\u0590' && c <= '\u05FF');

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = _textSize,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false,
                FlowDirection = hasHebrew ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight,
                TextAlignment = hasHebrew ? TextAlignment.Right : TextAlignment.Left
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

    // --- Text Size ---

    public void CycleTextSize()
    {
        _textSizeIndex = (_textSizeIndex + 1) % TextSizes.Length;
        _textSize = TextSizes[_textSizeIndex];
        string[] labels = { "SMALL", "MEDIUM", "LARGE" };
        ShowModeIndicator($"TEXT {labels[_textSizeIndex]}");
    }

    // --- Custom Cursor ---

    private static Cursor CreateCircleCursor(Color color, double size)
    {
        int dim = Math.Max((int)(size + 8), 24);
        int hotspot = dim / 2;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Outer circle (white outline for visibility on any background)
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.White), 2),
                new Point(hotspot, hotspot), size / 2 + 2, size / 2 + 2);
            // Inner circle (tool color)
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(color), 1.5),
                new Point(hotspot, hotspot), size / 2, size / 2);
            // Center dot
            dc.DrawEllipse(new SolidColorBrush(color), null,
                new Point(hotspot, hotspot), 1.5, 1.5);
        }

        var rtb = new RenderTargetBitmap(dim, dim, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;

        // Build .cur format: 22-byte header + PNG data via icon hack
        // Simpler approach: use System.Windows.Input.Cursor from stream
        return CursorFromBitmap(rtb, hotspot, hotspot);
    }

    private static Cursor CursorFromBitmap(RenderTargetBitmap bitmap, int hotX, int hotY)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // .cur file header
        bw.Write((short)0);      // Reserved
        bw.Write((short)2);      // Type: cursor
        bw.Write((short)1);      // Image count

        // Directory entry
        bw.Write((byte)width);
        bw.Write((byte)height);
        bw.Write((byte)0);       // Color count
        bw.Write((byte)0);       // Reserved
        bw.Write((short)hotX);   // Hotspot X
        bw.Write((short)hotY);   // Hotspot Y

        // We'll write size after we know it
        long sizePos = ms.Position;
        bw.Write(0);              // Placeholder for size
        bw.Write(22);             // Offset to data (header=6 + entry=16 = 22)

        long dataStart = ms.Position;

        // DIB header (BITMAPINFOHEADER)
        bw.Write(40);                 // Header size
        bw.Write(width);              // Width
        bw.Write(height * 2);         // Height (doubled for XOR + AND masks)
        bw.Write((short)1);           // Planes
        bw.Write((short)32);          // Bits per pixel
        bw.Write(0);                  // Compression (none)
        bw.Write(0);                  // Image size (can be 0 for uncompressed)
        bw.Write(0);                  // X pixels per meter
        bw.Write(0);                  // Y pixels per meter
        bw.Write(0);                  // Colors used
        bw.Write(0);                  // Important colors

        // XOR mask (BGRA, bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * stride) + (x * 4);
                bw.Write(pixels[idx + 0]); // B
                bw.Write(pixels[idx + 1]); // G
                bw.Write(pixels[idx + 2]); // R
                bw.Write(pixels[idx + 3]); // A
            }
        }

        // AND mask (1bpp, bottom-up) - all zeros = fully visible
        int andStride = ((width + 31) / 32) * 4;
        byte[] andMask = new byte[andStride * height];
        bw.Write(andMask);

        // Write actual size
        long dataSize = ms.Position - dataStart;
        ms.Position = sizePos;
        bw.Write((int)dataSize);

        ms.Position = 0;
        return new Cursor(ms);
    }

    // --- Mode Indicator ---

    private void ShowModeIndicator(string text)
    {
        App.Log($"ShowModeIndicator: {text}");
        _toast?.ShowToast(text);
    }
}
