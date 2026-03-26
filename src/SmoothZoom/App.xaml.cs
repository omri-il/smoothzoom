using System.Threading;
using System.Windows;
using SmoothZoom.Models;
using SmoothZoom.Native;
using SmoothZoom.Services;
using SmoothZoom.Views;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmoothZoom;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private WinForms.NotifyIcon? _trayIcon;
    private KeyboardHookService? _keyboardHook;
    private MagnificationService? _magnification;
    private ZoomController? _zoomController;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SetupCrashRecovery();

        _mutex = new Mutex(true, "Global\\SmoothZoomMutex", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("SmoothZoom is already running.", "SmoothZoom",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Load settings
        _settings = SettingsService.Load();

        // Initialize magnification API
        _magnification = new MagnificationService();
        if (!_magnification.Initialize())
        {
            System.Windows.MessageBox.Show(
                "Failed to initialize Magnification API.\nThe app may not work correctly.",
                "SmoothZoom", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _zoomController = new ZoomController(_magnification, OnZoomStateChanged);
        ApplySettings();

        SetupTrayIcon();
        SetupKeyboardHook();
    }

    private void ApplySettings()
    {
        if (_zoomController == null) return;
        _zoomController.TargetZoomLevel = _settings.TargetZoomLevel;
        _zoomController.ZoomDurationMs = _settings.ZoomDurationMs;
        _zoomController.CursorTrackingSpeed = _settings.CursorTrackingSpeed;
    }

    private void SetupCrashRecovery()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
        };
        DispatcherUnhandledException += (_, _) =>
        {
            MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
        };
    }

    private void OnZoomStateChanged(bool isZoomed)
    {
        UpdateTrayIcon(isZoomed);
    }

    private void SetupTrayIcon()
    {
        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Settings", null, OnSettingsClicked);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add("Quit", null, OnQuitClicked);

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(false),
            Text = "SmoothZoom",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
    }

    private static Drawing.Icon CreateIcon(bool isZoomed)
    {
        var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            var color = isZoomed
                ? Drawing.Color.FromArgb(255, 100, 80)
                : Drawing.Color.FromArgb(70, 130, 220);

            using var pen = new Drawing.Pen(color, 2.5f);
            g.DrawEllipse(pen, 4, 4, 18, 18);

            using var handlePen = new Drawing.Pen(color, 3f);
            g.DrawLine(handlePen, 19, 19, 27, 27);

            using var font = new Drawing.Font("Segoe UI", 7f, Drawing.FontStyle.Bold);
            using var brush = new Drawing.SolidBrush(color);
            g.DrawString("Z", font, brush, 7, 6);
        }
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void UpdateTrayIcon(bool isZoomed)
    {
        if (_trayIcon == null) return;
        _trayIcon.Icon = CreateIcon(isZoomed);
        _trayIcon.Text = isZoomed ? "SmoothZoom (Zoomed)" : "SmoothZoom";
    }

    private void SetupKeyboardHook()
    {
        _keyboardHook = new KeyboardHookService();
        _keyboardHook.ToggleZoomPressed += () => _zoomController?.Toggle();
        _keyboardHook.PanicResetPressed += () => _zoomController?.PanicReset();
        _keyboardHook.ViewLockPressed += () => _zoomController?.ToggleViewLock();
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        var window = new SettingsWindow(_settings);
        window.ShowDialog();

        if (window.Saved)
        {
            _settings = window.Settings;
            SettingsService.Save(_settings);
            ApplySettings();
        }
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        _zoomController?.PanicReset();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _zoomController?.Dispose();
        _magnification?.Dispose();
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
