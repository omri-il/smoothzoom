using System.Runtime.InteropServices;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class MagnificationService : IDisposable
{
    private bool _initialized;
    private readonly List<IntPtr> _excludeWindows = new();

    // Windowed magnifier state
    private System.Windows.Forms.Form? _hostForm;
    private IntPtr _magWindow = IntPtr.Zero;
    private float _lastScale;
    private bool _cursorHidden;

    // Public state for click translation
    public User32.RECT CurrentSourceRect { get; private set; }
    public User32.RECT ActiveMonitorBounds { get; private set; }

    // IsActive: magnifier form and child window exist (regardless of visibility)
    public bool IsActive => _hostForm != null && _magWindow != IntPtr.Zero;

    public event Action? MagnifierWindowCreated;

    public void ExcludeWindow(IntPtr hwnd)
    {
        if (!_excludeWindows.Contains(hwnd))
            _excludeWindows.Add(hwnd);
        UpdateFilterList();
    }

    public void RemoveExcludedWindow(IntPtr hwnd)
    {
        _excludeWindows.Remove(hwnd);
        UpdateFilterList();
    }

    public bool Initialize()
    {
        _initialized = MagnificationApi.MagInitialize();
        if (_initialized)
        {
            // Safety: reset any leftover fullscreen zoom
            MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
        }
        return _initialized;
    }

    public void SetZoom(float scale, int cursorX, int cursorY, User32.RECT monitorBounds)
    {
        if (!_initialized) return;

        if (scale <= 1.0f)
        {
            // Hide instead of destroy — form stays alive for instant re-show next zoom.
            // Destroy/create on every zoom cycle caused a black flash.
            if (_hostForm != null) _hostForm.Visible = false;
            RestoreCursor();
            return;
        }

        // Cursor must be hidden for the entire zoomed duration (flag is idempotent)
        HideCursor();

        // If form exists but was created for a different monitor, recreate it
        if (IsActive &&
            (ActiveMonitorBounds.Left  != monitorBounds.Left  ||
             ActiveMonitorBounds.Top   != monitorBounds.Top   ||
             ActiveMonitorBounds.Right != monitorBounds.Right))
        {
            DestroyMagnifier();
        }

        // Create magnifier if it doesn't exist yet
        if (!IsActive)
        {
            CreateMagnifier(monitorBounds);
            if (!IsActive) return; // Creation failed
        }

        // Update transform only when scale changes
        if (MathF.Abs(scale - _lastScale) > 0.001f)
        {
            var transform = MagnificationApi.MAGTRANSFORM.CreateScale(scale);
            MagnificationApi.MagSetWindowTransform(_magWindow, ref transform);
            _lastScale = scale;
        }

        // Calculate source rect (what part of the desktop to show)
        int monW = monitorBounds.Right - monitorBounds.Left;
        int monH = monitorBounds.Bottom - monitorBounds.Top;
        float viewW = monW / scale;
        float viewH = monH / scale;

        float relX = (float)(cursorX - monitorBounds.Left) / monW;
        float relY = (float)(cursorY - monitorBounds.Top) / monH;

        float srcLeft = cursorX - relX * viewW;
        float srcTop  = cursorY - relY * viewH;

        // Edge clamping
        srcLeft = Math.Clamp(srcLeft, monitorBounds.Left, monitorBounds.Right  - viewW);
        srcTop  = Math.Clamp(srcTop,  monitorBounds.Top,  monitorBounds.Bottom - viewH);

        var sourceRect = new User32.RECT
        {
            Left   = (int)MathF.Round(srcLeft),
            Top    = (int)MathF.Round(srcTop),
            Right  = (int)MathF.Round(srcLeft + viewW),
            Bottom = (int)MathF.Round(srcTop  + viewH)
        };

        CurrentSourceRect  = sourceRect;
        ActiveMonitorBounds = monitorBounds;

        // Set new source content
        MagnificationApi.MagSetWindowSource(_magWindow, sourceRect);

        // Show form first (if hidden), then force an immediate synchronous repaint.
        // Order matters: show → invalidate → UpdateWindow (bypasses queue, calls WndProc
        // directly so WM_PAINT fires before the next timer tick — the form must be visible
        // for UpdateWindow to have effect on the child).
        if (!_hostForm!.Visible) _hostForm.Visible = true;
        User32.InvalidateRect(_magWindow, IntPtr.Zero, false);
        User32.UpdateWindow(_magWindow);
    }

    public void Reset()
    {
        DestroyMagnifier();
        RestoreCursor();
        // Safety fallback: also clear any fullscreen transform
        MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
    }

    private void CreateMagnifier(User32.RECT monitorBounds)
    {
        int monW = monitorBounds.Right - monitorBounds.Left;
        int monH = monitorBounds.Bottom - monitorBounds.Top;

        // Create host form hidden — it will be shown by SetZoom after source is set,
        // so the first visible frame already has magnifier content (no black flash).
        _hostForm = new MagnifierHostForm
        {
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
            StartPosition   = System.Windows.Forms.FormStartPosition.Manual,
            Location        = new System.Drawing.Point(monitorBounds.Left, monitorBounds.Top),
            Size            = new System.Drawing.Size(monW, monH),
            TopMost         = true,
            ShowInTaskbar   = false,
            BackColor       = System.Drawing.Color.Black
        };

        // Accessing Handle creates the HWND without showing the form
        IntPtr hostHwnd = _hostForm.Handle;

        // Set extended styles: click-through + no activate + tool window
        int exStyle = User32.GetWindowLong(hostHwnd, User32.GWL_EXSTYLE);
        exStyle |= (int)(User32.WS_EX_TRANSPARENT | User32.WS_EX_LAYERED |
                         User32.WS_EX_TOOLWINDOW  | User32.WS_EX_NOACTIVATE);
        User32.SetWindowLong(hostHwnd, User32.GWL_EXSTYLE, exStyle);

        // WS_EX_LAYERED requires layered window attributes (fully opaque)
        SetLayeredWindowAttributes(hostHwnd, 0, 255, 0x02 /* LWA_ALPHA */);

        // Create magnifier child window
        _magWindow = User32.CreateWindowEx(
            0,
            User32.WC_MAGNIFIER,
            "MagnifierChild",
            User32.WS_CHILD | User32.WS_VISIBLE,
            0, 0, monW, monH,
            hostHwnd,
            IntPtr.Zero,
            Kernel32.GetModuleHandle(null),
            IntPtr.Zero);

        if (_magWindow == IntPtr.Zero)
        {
            _hostForm.Close();
            _hostForm.Dispose();
            _hostForm = null;
            return;
        }

        // Set initial transform
        var transform = MagnificationApi.MAGTRANSFORM.CreateScale(1.0f);
        MagnificationApi.MagSetWindowTransform(_magWindow, ref transform);
        _lastScale = 1.0f;

        // Set filter list (exclude host form + any registered windows)
        UpdateFilterList();

        ActiveMonitorBounds = monitorBounds;
        MagnifierWindowCreated?.Invoke();
        // NOTE: form is intentionally NOT shown here — SetZoom shows it after source is set
    }

    private void DestroyMagnifier()
    {
        if (_magWindow != IntPtr.Zero)
        {
            User32.DestroyWindow(_magWindow);
            _magWindow = IntPtr.Zero;
        }

        if (_hostForm != null)
        {
            _hostForm.Close();
            _hostForm.Dispose();
            _hostForm = null;
        }

        _lastScale = 0;
    }

    private void UpdateFilterList()
    {
        if (_magWindow == IntPtr.Zero) return;

        var handles = new List<IntPtr>();

        // Always exclude the host form
        if (_hostForm != null)
            handles.Add(_hostForm.Handle);

        // Add user-registered exclusions (cursor highlight, help overlay, etc.)
        foreach (var hwnd in _excludeWindows)
        {
            if (User32.IsWindow(hwnd))
                handles.Add(hwnd);
        }

        if (handles.Count == 0) return;

        int size = Marshal.SizeOf<IntPtr>();
        IntPtr buffer = Marshal.AllocHGlobal(size * handles.Count);
        try
        {
            for (int i = 0; i < handles.Count; i++)
                Marshal.WriteIntPtr(buffer, i * size, handles[i]);

            MagnificationApi.MagSetWindowFilterList(
                _magWindow,
                MagnificationApi.MW_FILTERMODE_EXCLUDE,
                handles.Count,
                buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static User32.RECT GetMonitorBounds(int cursorX, int cursorY)
    {
        var point   = new User32.POINT { X = cursorX, Y = cursorY };
        var monitor = User32.MonitorFromPoint(point, User32.MONITOR_DEFAULTTONEAREST);
        var info    = new User32.MONITORINFO { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
        User32.GetMonitorInfo(monitor, ref info);
        return info.rcMonitor;
    }

    private void HideCursor()
    {
        if (_cursorHidden) return;
        User32.ShowCursor(false);
        _cursorHidden = true;
    }

    private void RestoreCursor()
    {
        if (!_cursorHidden) return;
        User32.ShowCursor(true);
        _cursorHidden = false;
    }

    public void Dispose()
    {
        DestroyMagnifier();
        RestoreCursor();
        if (_initialized)
        {
            MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
            MagnificationApi.MagUninitialize();
            _initialized = false;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    /// <summary>
    /// Custom Form that prevents background erase flicker and uses WS_CLIPCHILDREN.
    /// </summary>
    private class MagnifierHostForm : System.Windows.Forms.Form
    {
        protected override System.Windows.Forms.CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= (int)User32.WS_CLIPCHILDREN;
                return cp;
            }
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            const int WM_ERASEBKGND = 0x0014;
            const int WM_NCHITTEST  = 0x0084;
            const int HTTRANSPARENT = -1;

            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1; // Skip background erase
                return;
            }

            // Make the form transparent to mouse input at WndProc level too
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
