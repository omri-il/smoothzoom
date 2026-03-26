using System.Runtime.InteropServices;
using SmoothZoom.Native;
using WinForms = System.Windows.Forms;

namespace SmoothZoom.Services;

public class MagnificationService : IDisposable
{
    private bool _initialized;

    // Windowed magnifier state
    private WinForms.Form? _hostForm;
    private IntPtr _magnifierHwnd = IntPtr.Zero;
    private User32.RECT _currentMonitorBounds;
    private bool _windowsCreated;
    private float _lastScale;
    private User32.RECT _lastSourceRect;
    private readonly List<IntPtr> _excludeWindows = new();

    // Event to notify overlays to re-assert Z-order
    public event Action? MagnifierWindowCreated;

    public void ExcludeWindow(IntPtr hwnd)
    {
        if (!_excludeWindows.Contains(hwnd))
            _excludeWindows.Add(hwnd);
        if (_magnifierHwnd != IntPtr.Zero)
            UpdateFilterList();
    }

    public void RemoveExcludedWindow(IntPtr hwnd)
    {
        _excludeWindows.Remove(hwnd);
        if (_magnifierHwnd != IntPtr.Zero)
            UpdateFilterList();
    }

    public bool Initialize()
    {
        _initialized = MagnificationApi.MagInitialize();
        if (_initialized)
            MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
        return _initialized;
    }

    public void SetZoom(float scale, int cursorX, int cursorY, User32.RECT monitorBounds)
    {
        if (!_initialized || scale <= 1.0f)
        {
            Reset();
            return;
        }

        if (!_windowsCreated || !BoundsEqual(_currentMonitorBounds, monitorBounds))
        {
            DestroyMagnifierWindows();
            CreateMagnifierWindows(monitorBounds);
            if (!_windowsCreated) return;
        }

        if (Math.Abs(scale - _lastScale) > 0.001f)
        {
            var transform = MagnificationApi.MAGTRANSFORM.CreateScale(scale);
            MagnificationApi.MagSetWindowTransform(_magnifierHwnd, ref transform);
            _lastScale = scale;
        }

        int monW = monitorBounds.Right - monitorBounds.Left;
        int monH = monitorBounds.Bottom - monitorBounds.Top;
        float viewW = monW / scale;
        float viewH = monH / scale;

        float relX = (float)(cursorX - monitorBounds.Left) / monW;
        float relY = (float)(cursorY - monitorBounds.Top) / monH;

        float visibleLeft = cursorX - relX * viewW;
        float visibleTop = cursorY - relY * viewH;

        visibleLeft = Math.Clamp(visibleLeft, monitorBounds.Left, monitorBounds.Right - viewW);
        visibleTop = Math.Clamp(visibleTop, monitorBounds.Top, monitorBounds.Bottom - viewH);

        var sourceRect = new User32.RECT
        {
            Left = (int)MathF.Round(visibleLeft),
            Top = (int)MathF.Round(visibleTop),
            Right = (int)MathF.Round(visibleLeft + viewW),
            Bottom = (int)MathF.Round(visibleTop + viewH)
        };

        MagnificationApi.MagSetWindowSource(_magnifierHwnd, sourceRect);

        // Only invalidate when source rect actually changed
        if (!RectEqual(sourceRect, _lastSourceRect))
        {
            User32.InvalidateRect(_magnifierHwnd, IntPtr.Zero, false);
            _lastSourceRect = sourceRect;
        }
    }

    public void Reset()
    {
        DestroyMagnifierWindows();
        MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
    }

    private void CreateMagnifierWindows(User32.RECT monitorBounds)
    {
        _currentMonitorBounds = monitorBounds;
        int monW = monitorBounds.Right - monitorBounds.Left;
        int monH = monitorBounds.Bottom - monitorBounds.Top;

        _hostForm = new MagnifierHostForm
        {
            FormBorderStyle = WinForms.FormBorderStyle.None,
            ShowInTaskbar = false,
            TopMost = true,
            StartPosition = WinForms.FormStartPosition.Manual,
            Location = new System.Drawing.Point(monitorBounds.Left, monitorBounds.Top),
            Size = new System.Drawing.Size(monW, monH),
        };
        _hostForm.Show();

        IntPtr hostHwnd = _hostForm.Handle;
        int exStyle = User32.GetWindowLong(hostHwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLong(hostHwnd, User32.GWL_EXSTYLE,
            exStyle | (int)(User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT
                          | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE));

        _magnifierHwnd = User32.CreateWindowEx(0,
            User32.WC_MAGNIFIER, "MagnifierChild",
            User32.WS_CHILD | User32.WS_VISIBLE,
            0, 0, monW, monH,
            hostHwnd, IntPtr.Zero, Kernel32.GetModuleHandle(null), IntPtr.Zero);

        if (_magnifierHwnd == IntPtr.Zero)
        {
            DestroyMagnifierWindows();
            return;
        }

        UpdateFilterList();

        _lastScale = 0;
        _lastSourceRect = default;
        _windowsCreated = true;

        // Notify overlays (ring, help) to re-assert Z-order above us
        MagnifierWindowCreated?.Invoke();
    }

    private void DestroyMagnifierWindows()
    {
        if (_magnifierHwnd != IntPtr.Zero)
        {
            User32.DestroyWindow(_magnifierHwnd);
            _magnifierHwnd = IntPtr.Zero;
        }
        if (_hostForm != null)
        {
            _hostForm.Hide();
            _hostForm.Close();
            _hostForm.Dispose();
            _hostForm = null;
        }
        _windowsCreated = false;
        _lastScale = 0;
        _lastSourceRect = default;
    }

    private void UpdateFilterList()
    {
        if (_magnifierHwnd == IntPtr.Zero || _hostForm == null) return;

        var handles = new List<IntPtr> { _hostForm.Handle };
        foreach (var hwnd in _excludeWindows)
        {
            if (User32.IsWindow(hwnd))
                handles.Add(hwnd);
        }
        _excludeWindows.RemoveAll(h => !User32.IsWindow(h));

        IntPtr filterList = Marshal.AllocHGlobal(IntPtr.Size * handles.Count);
        for (int i = 0; i < handles.Count; i++)
            Marshal.WriteIntPtr(filterList, i * IntPtr.Size, handles[i]);

        MagnificationApi.MagSetWindowFilterList(_magnifierHwnd,
            MagnificationApi.MW_FILTERMODE_EXCLUDE, handles.Count, filterList);
        Marshal.FreeHGlobal(filterList);
    }

    private static bool BoundsEqual(User32.RECT a, User32.RECT b) =>
        a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    private static bool RectEqual(User32.RECT a, User32.RECT b) =>
        a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    public static User32.RECT GetMonitorBounds(int cursorX, int cursorY)
    {
        var point = new User32.POINT { X = cursorX, Y = cursorY };
        var monitor = User32.MonitorFromPoint(point, User32.MONITOR_DEFAULTTONEAREST);
        var info = new User32.MONITORINFO { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
        User32.GetMonitorInfo(monitor, ref info);
        return info.rcMonitor;
    }

    public void Dispose()
    {
        Reset();
        if (_initialized)
        {
            MagnificationApi.MagUninitialize();
            _initialized = false;
        }
    }
}

internal class MagnifierHostForm : WinForms.Form
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_ERASEBKGND = 0x0014;
    private const int HTTRANSPARENT = -1;

    protected override void WndProc(ref WinForms.Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }
        // Prevent background erasing (reduces flicker)
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }

    protected override WinForms.CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_CLIPCHILDREN prevents parent from painting over magnifier child
            cp.Style |= (int)User32.WS_CLIPCHILDREN;
            cp.ExStyle |= (int)(User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT
                              | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE);
            return cp;
        }
    }
}
