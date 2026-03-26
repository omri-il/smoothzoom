using System.Threading;
using System.Windows;
using System.Windows.Media;
using SmoothAnnotate.Models;
using SmoothAnnotate.Services;
using SmoothAnnotate.Views;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmoothAnnotate;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private WinForms.NotifyIcon? _trayIcon;
    private KeyboardHookService? _keyboardHook;
    private OverlayWindow? _overlayWindow;
    private AnnotationSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SetupCrashRecovery();

        _mutex = new Mutex(true, "Global\\SmoothAnnotateMutex", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("SmoothAnnotate is already running.", "SmoothAnnotate",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _settings = SettingsService.Load();

        _overlayWindow = new OverlayWindow(_settings);
        _overlayWindow.Show();

        // Initialize services after window is created
        var laserColor = (Color)ColorConverter.ConvertFromString(_settings.LaserColor);
        var laserService = new LaserService(_overlayWindow.FindName("DrawCanvas") as System.Windows.Controls.InkCanvas
            ?? throw new InvalidOperationException("DrawCanvas not found"),
            _settings.LaserFadeMs, laserColor);
        var stopwatchService = new StopwatchService();
        _overlayWindow.InitializeServices(laserService, stopwatchService);

        SetupTrayIcon();
        SetupKeyboardHook();
    }

    private void SetupCrashRecovery()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, _) => { };
        DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
        };
    }

    private void SetupTrayIcon()
    {
        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Quit", null, OnQuitClicked);

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "SmoothAnnotate",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
    }

    private static Drawing.Icon CreateIcon()
    {
        var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            var color = Drawing.Color.FromArgb(70, 130, 220);

            // Draw a pen/pencil shape
            using var pen = new Drawing.Pen(color, 2.5f);
            g.DrawLine(pen, 6, 26, 26, 6);   // Diagonal line (pen body)
            g.DrawLine(pen, 4, 28, 8, 24);    // Pen tip

            // "A" label for Annotate
            using var font = new Drawing.Font("Segoe UI", 7f, Drawing.FontStyle.Bold);
            using var brush = new Drawing.SolidBrush(color);
            g.DrawString("A", font, brush, 16, 16);
        }
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void SetupKeyboardHook()
    {
        _keyboardHook = new KeyboardHookService();

        // Core tools
        _keyboardHook.DrawModeToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleDrawMode());
        _keyboardHook.ClearInk += () => Dispatcher.Invoke(() => _overlayWindow?.ClearAllStrokes());
        _keyboardHook.LaserToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleLaser());
        _keyboardHook.TimerToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleTimer());
        _keyboardHook.TimerVisibilityToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleTimerVisibility());

        // Shape tools
        _keyboardHook.ArrowToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleArrow());
        _keyboardHook.RectangleToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleRectangle());
        _keyboardHook.CircleToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleCircle());
        _keyboardHook.TextToggled += () => Dispatcher.Invoke(() => _overlayWindow?.ToggleText());

        // Color picker
        _keyboardHook.ColorChanged += (idx) => Dispatcher.Invoke(() => _overlayWindow?.SetColor(idx));
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlayWindow?.Close();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _keyboardHook?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
