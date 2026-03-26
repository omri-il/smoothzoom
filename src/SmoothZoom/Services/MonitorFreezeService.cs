using SmoothZoom.Native;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmoothZoom.Services;

public class MonitorFreezeService : IDisposable
{
    private readonly List<WinForms.Form> _overlays = new();

    public void FreezeOtherMonitors(User32.RECT activeMonitorBounds)
    {
        UnfreezeAll();

        foreach (var screen in WinForms.Screen.AllScreens)
        {
            var bounds = screen.Bounds;

            if (bounds.Left == activeMonitorBounds.Left &&
                bounds.Top == activeMonitorBounds.Top &&
                bounds.Right == activeMonitorBounds.Right &&
                bounds.Bottom == activeMonitorBounds.Bottom)
                continue;

            var screenshot = CaptureScreen(bounds);
            if (screenshot == null) continue;

            // Simple topmost form with screenshot — NO click-through needed
            // since this is on the OTHER monitor the user isn't using
            var overlay = new WinForms.Form
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

            // Hide from Alt-Tab only
            IntPtr hwnd = overlay.Handle;
            int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
            User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE,
                exStyle | (int)User32.WS_EX_TOOLWINDOW);

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
