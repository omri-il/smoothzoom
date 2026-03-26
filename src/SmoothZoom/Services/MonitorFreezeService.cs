using System.Runtime.InteropServices;
using SmoothZoom.Native;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmoothZoom.Services;

/// <summary>
/// Captures screenshots of non-active monitors and displays them as
/// frozen overlays so they appear unaffected by fullscreen magnification.
/// </summary>
public class MonitorFreezeService : IDisposable
{
    private readonly List<WinForms.Form> _overlays = new();

    public void FreezeOtherMonitors(User32.RECT activeMonitorBounds)
    {
        UnfreezeAll();

        foreach (var screen in WinForms.Screen.AllScreens)
        {
            var bounds = screen.Bounds;

            // Skip the active monitor
            if (bounds.Left == activeMonitorBounds.Left &&
                bounds.Top == activeMonitorBounds.Top &&
                bounds.Right == activeMonitorBounds.Right &&
                bounds.Bottom == activeMonitorBounds.Bottom)
                continue;

            // Capture screenshot of this monitor (shows what was there before zoom)
            var screenshot = CaptureScreen(bounds);
            if (screenshot == null) continue;

            var overlay = new FreezeOverlayForm
            {
                FormBorderStyle = WinForms.FormBorderStyle.None,
                ShowInTaskbar = false,
                TopMost = true,
                StartPosition = WinForms.FormStartPosition.Manual,
                Location = new Drawing.Point(bounds.Left, bounds.Top),
                Size = new Drawing.Size(bounds.Width, bounds.Height),
                BackgroundImage = screenshot,
                BackgroundImageLayout = WinForms.ImageLayout.Stretch,
            };
            overlay.Show();

            // Make it click-through so user can still interact if needed
            IntPtr hwnd = overlay.Handle;
            int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
            User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE,
                exStyle | (int)(User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT
                              | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE));

            _overlays.Add(overlay);
        }
    }

    public void UnfreezeAll()
    {
        foreach (var overlay in _overlays)
        {
            overlay.BackgroundImage?.Dispose();
            overlay.Hide();
            overlay.Close();
            overlay.Dispose();
        }
        _overlays.Clear();
    }

    private static Drawing.Bitmap? CaptureScreen(Drawing.Rectangle bounds)
    {
        try
        {
            var bitmap = new Drawing.Bitmap(bounds.Width, bounds.Height);
            using var g = Drawing.Graphics.FromImage(bitmap);
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0,
                new Drawing.Size(bounds.Width, bounds.Height));
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        UnfreezeAll();
    }
}

internal class FreezeOverlayForm : WinForms.Form
{
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;

    protected override void WndProc(ref WinForms.Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }
        base.WndProc(ref m);
    }

    protected override WinForms.CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= (int)(User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT
                              | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE);
            return cp;
        }
    }
}
