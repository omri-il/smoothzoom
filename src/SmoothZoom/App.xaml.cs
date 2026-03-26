using System.Threading;
using System.Windows;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmoothZoom;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private WinForms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance enforcement
        _mutex = new Mutex(true, "Global\\SmoothZoomMutex", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("SmoothZoom is already running.", "SmoothZoom",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Settings", null, OnSettingsClicked);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add("Quit", null, OnQuitClicked);

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "SmoothZoom",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
    }

    private static Drawing.Icon CreateDefaultIcon()
    {
        // Create a simple magnifying glass icon programmatically
        var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            // Draw magnifying glass circle
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(70, 130, 220), 2.5f);
            g.DrawEllipse(pen, 4, 4, 18, 18);

            // Draw handle
            using var handlePen = new Drawing.Pen(Drawing.Color.FromArgb(70, 130, 220), 3f);
            g.DrawLine(handlePen, 19, 19, 27, 27);

            // Draw "Z" in center
            using var font = new Drawing.Font("Segoe UI", 7f, Drawing.FontStyle.Bold);
            using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(70, 130, 220));
            g.DrawString("Z", font, brush, 7, 6);
        }
        var handle = bitmap.GetHicon();
        return Drawing.Icon.FromHandle(handle);
    }

    public void SetTrayIconZoomed(bool isZoomed)
    {
        if (_trayIcon == null) return;

        var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            var color = isZoomed
                ? Drawing.Color.FromArgb(255, 100, 80)   // Red when zoomed
                : Drawing.Color.FromArgb(70, 130, 220);  // Blue when normal

            using var pen = new Drawing.Pen(color, 2.5f);
            g.DrawEllipse(pen, 4, 4, 18, 18);

            using var handlePen = new Drawing.Pen(color, 3f);
            g.DrawLine(handlePen, 19, 19, 27, 27);

            using var font = new Drawing.Font("Segoe UI", 7f, Drawing.FontStyle.Bold);
            using var brush = new Drawing.SolidBrush(color);
            g.DrawString("Z", font, brush, 7, 6);
        }
        var handle = bitmap.GetHicon();
        _trayIcon.Icon = Drawing.Icon.FromHandle(handle);
        _trayIcon.Text = isZoomed ? "SmoothZoom (Zoomed)" : "SmoothZoom";
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        // Will be implemented in Phase 5
        System.Windows.MessageBox.Show("Settings coming soon!", "SmoothZoom",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        // TODO: Reset magnification before quitting (Phase 3)
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
