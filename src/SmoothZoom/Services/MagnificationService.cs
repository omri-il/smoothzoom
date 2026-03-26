using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class MagnificationService : IDisposable
{
    private bool _initialized;

    public bool Initialize()
    {
        _initialized = MagnificationApi.MagInitialize();
        if (_initialized)
        {
            // Always reset on startup to clear any leftover zoom from a previous crash
            Reset();
        }
        return _initialized;
    }

    public void SetZoom(float scale, int cursorX, int cursorY, User32.RECT monitorBounds)
    {
        if (!_initialized || scale <= 1.0f)
        {
            Reset();
            return;
        }

        int monitorW = monitorBounds.Right - monitorBounds.Left;
        int monitorH = monitorBounds.Bottom - monitorBounds.Top;
        float viewW = monitorW / scale;
        float viewH = monitorH / scale;

        // Position viewport so cursor stays at its proportional screen position
        float relX = (float)(cursorX - monitorBounds.Left) / monitorW;
        float relY = (float)(cursorY - monitorBounds.Top) / monitorH;

        float visibleLeft = cursorX - relX * viewW;
        float visibleTop = cursorY - relY * viewH;

        // Edge clamping: prevent black borders
        visibleLeft = Math.Clamp(visibleLeft, monitorBounds.Left, monitorBounds.Right - viewW);
        visibleTop = Math.Clamp(visibleTop, monitorBounds.Top, monitorBounds.Bottom - viewH);

        // Round to nearest pixel to reduce sub-pixel blur
        int offsetX = (int)MathF.Round(visibleLeft);
        int offsetY = (int)MathF.Round(visibleTop);

        MagnificationApi.MagSetFullscreenTransform(scale, offsetX, offsetY);
    }

    public void Reset()
    {
        MagnificationApi.MagSetFullscreenTransform(1.0f, 0, 0);
    }

    public static User32.RECT GetMonitorBounds(int cursorX, int cursorY)
    {
        var point = new User32.POINT { X = cursorX, Y = cursorY };
        var monitor = User32.MonitorFromPoint(point, User32.MONITOR_DEFAULTTONEAREST);

        var info = new User32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<User32.MONITORINFO>() };
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
