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
    private CursorHighlightService? _cursorHighlight;
    private ClickTranslationService? _clickTranslation;
    private ObsRecordingWatcher? _obsWatcher;
    private bool _ringOnForRecording; // the watcher (not Ctrl+Alt+H) turned the ring on
    private HelpOverlay? _helpOverlay;
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
        _cursorHighlight = new CursorHighlightService();
        _cursorHighlight.SetMagnificationService(_magnification);
        _clickTranslation = new ClickTranslationService(_magnification);
        ApplySettings();

        SetupTrayIcon();
        SetupKeyboardHook();

        if (_settings.AutoRingWhileRecording)
        {
            _obsWatcher = new ObsRecordingWatcher();
            _obsWatcher.RecordingChanged += r => Dispatcher.BeginInvoke(() => OnRecordingChanged(r));
            _obsWatcher.Start();
        }
    }

    private void OnRecordingChanged(bool recording)
    {
        if (_cursorHighlight == null) return;
        if (recording)
        {
            if (_cursorHighlight.IsActive) return; // already on by hand — leave it to the user
            _cursorHighlight.SetActive(true);
            _ringOnForRecording = true;
        }
        else if (_ringOnForRecording)
        {
            _cursorHighlight.SetActive(false);
            _ringOnForRecording = false;
        }
    }

    private void ApplySettings()
    {
        if (_zoomController != null)
        {
            _zoomController.TargetZoomLevel = _settings.TargetZoomLevel;
            _zoomController.ZoomDurationMs = _settings.ZoomDurationMs;
            _zoomController.CursorTrackingSpeed = _settings.CursorTrackingSpeed;
        }
        if (_cursorHighlight != null)
        {
            _cursorHighlight.RingSize = _settings.HighlightRingSize;
            _cursorHighlight.RingThickness = _settings.HighlightThickness;
            _cursorHighlight.ClickRipple = _settings.ClickRipple;
            try
            {
                _cursorHighlight.RingColor =
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                        _settings.HighlightColor);
            }
            catch { /* keep default if parse fails */ }
            try
            {
                _cursorHighlight.RingFill =
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                        _settings.HighlightFill);
            }
            catch { /* keep default if parse fails */ }
            _cursorHighlight.Refresh();
        }
    }

    private void SetupCrashRecovery()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            _magnification?.Reset();
            MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
        };
        DispatcherUnhandledException += (_, _) =>
        {
            _magnification?.Reset();
            MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
        };
    }

    private void OnZoomStateChanged(bool isZoomed, User32.RECT activeMonitorBounds)
    {
        UpdateTrayIcon(isZoomed);
    }

    private void SetupTrayIcon()
    {
        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Help (Ctrl+Alt+/)", null, (_, _) => OnHelpToggle());
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
        if (_clickTranslation != null)
            _keyboardHook.SetClickTranslation(_clickTranslation);
        // Dispatch all hook events to the UI thread — hook callbacks fire on the
        // low-level hook thread, not the WPF dispatcher thread, which causes race
        // conditions with the DispatcherTimer reading/writing ZoomController state.
        _keyboardHook.ToggleZoomPressed      += () => Dispatcher.BeginInvoke(() => _zoomController?.Toggle());
        _keyboardHook.PanicResetPressed      += () => Dispatcher.BeginInvoke(() => _zoomController?.PanicReset());
        _keyboardHook.ViewLockPressed        += () => Dispatcher.BeginInvoke(() => _zoomController?.ToggleViewLock());
        _keyboardHook.ZoomInStepPressed      += () => Dispatcher.BeginInvoke(() => _zoomController?.ZoomInStep());
        _keyboardHook.ZoomOutStepPressed     += () => Dispatcher.BeginInvoke(() => _zoomController?.ZoomOutStep());
        _keyboardHook.MiddleButtonChanged    += (p)  => Dispatcher.BeginInvoke(() => _zoomController?.SetMiddleDragging(p));
        _keyboardHook.HighlightTogglePressed += () => Dispatcher.BeginInvoke(() =>
        {
            _cursorHighlight?.Toggle();
            _ringOnForRecording = false; // a manual toggle takes over from auto-on
        });
        _keyboardHook.MouseClicked           += (r) => Dispatcher.BeginInvoke(() => _cursorHighlight?.ShowClick(r));
        _keyboardHook.HelpTogglePressed      += () => Dispatcher.BeginInvoke(OnHelpToggle);
    }

    private void OnHelpToggle()
    {
        if (_helpOverlay != null)
        {
            _helpOverlay.Close();
            _helpOverlay = null;
            return;
        }

        _helpOverlay = new HelpOverlay();
        _helpOverlay.Closed += (_, _) => _helpOverlay = null;
        _helpOverlay.Show();
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
        _obsWatcher?.Dispose();
        _cursorHighlight?.Dispose();
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
