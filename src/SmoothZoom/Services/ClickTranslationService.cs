using System.Runtime.InteropServices;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

/// <summary>
/// Translates mouse clicks from magnified screen coordinates to unmagnified desktop coordinates.
/// Used with the windowed magnifier to make clicks land at the correct position.
/// </summary>
public class ClickTranslationService
{
    private readonly MagnificationService _magnification;
    private const uint MAGIC_EXTRA_INFO = 0xDEAD5001;

    public ClickTranslationService(MagnificationService magnification)
    {
        _magnification = magnification;
    }

    /// <summary>
    /// Attempts to translate a mouse click from magnified to desktop coordinates.
    /// Returns true if the click should be swallowed (we injected a translated version).
    /// </summary>
    public bool TryTranslateClick(int msg, User32.MSLLHOOKSTRUCT hookData)
    {
        // 1. Skip our own injected clicks (prevent infinite loop)
        if ((uint)(long)hookData.dwExtraInfo == MAGIC_EXTRA_INFO)
            return false;

        // 2. Skip if magnifier not active
        if (!_magnification.IsActive)
            return false;

        // 3. Skip if click is outside the active monitor (second monitor is not zoomed)
        var monBounds = _magnification.ActiveMonitorBounds;
        if (hookData.pt.X < monBounds.Left || hookData.pt.X >= monBounds.Right ||
            hookData.pt.Y < monBounds.Top || hookData.pt.Y >= monBounds.Bottom)
            return false;

        // 4. Determine which mouse event flags to inject
        uint downUpFlags;
        switch (msg)
        {
            case User32.WM_LBUTTONDOWN:
            case User32.WM_LBUTTONDBLCLK:
                downUpFlags = User32.MOUSEEVENTF_LEFTDOWN;
                break;
            case User32.WM_LBUTTONUP:
                downUpFlags = User32.MOUSEEVENTF_LEFTUP;
                break;
            case User32.WM_RBUTTONDOWN:
            case User32.WM_RBUTTONDBLCLK:
                downUpFlags = User32.MOUSEEVENTF_RIGHTDOWN;
                break;
            case User32.WM_RBUTTONUP:
                downUpFlags = User32.MOUSEEVENTF_RIGHTUP;
                break;
            default:
                return false; // Not a click we translate (middle button, move, etc.)
        }

        // 5. Translate coordinates: magnified screen → unmagnified desktop
        var srcRect = _magnification.CurrentSourceRect;
        int monW = monBounds.Right - monBounds.Left;
        int monH = monBounds.Bottom - monBounds.Top;
        int srcW = srcRect.Right - srcRect.Left;
        int srcH = srcRect.Bottom - srcRect.Top;

        if (monW <= 0 || monH <= 0 || srcW <= 0 || srcH <= 0)
            return false;

        float normalizedX = (float)(hookData.pt.X - monBounds.Left) / monW;
        float normalizedY = (float)(hookData.pt.Y - monBounds.Top) / monH;

        int desktopX = srcRect.Left + (int)(normalizedX * srcW);
        int desktopY = srcRect.Top + (int)(normalizedY * srcH);

        // 6. Convert to absolute coordinates for SendInput (0-65535 range)
        int vScreenX = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN);
        int vScreenY = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN);
        int vScreenW = User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN);
        int vScreenH = User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN);

        int absDesktopX = (int)(((desktopX - vScreenX) * 65535L) / (vScreenW - 1));
        int absDesktopY = (int)(((desktopY - vScreenY) * 65535L) / (vScreenH - 1));

        // Also compute absolute coords for original cursor position (to move back)
        int absOriginalX = (int)(((hookData.pt.X - vScreenX) * 65535L) / (vScreenW - 1));
        int absOriginalY = (int)(((hookData.pt.Y - vScreenY) * 65535L) / (vScreenH - 1));

        var magic = unchecked((IntPtr)MAGIC_EXTRA_INFO);

        // 7. Inject: move to translated pos → click → move back to original pos
        var inputs = new User32.INPUT[]
        {
            // Move cursor to translated desktop position
            new()
            {
                type = User32.INPUT_MOUSE,
                mi = new User32.MOUSEINPUT
                {
                    dx = absDesktopX,
                    dy = absDesktopY,
                    dwFlags = User32.MOUSEEVENTF_MOVE | User32.MOUSEEVENTF_ABSOLUTE | User32.MOUSEEVENTF_VIRTUALDESK,
                    dwExtraInfo = magic
                }
            },
            // The actual click event
            new()
            {
                type = User32.INPUT_MOUSE,
                mi = new User32.MOUSEINPUT
                {
                    dx = absDesktopX,
                    dy = absDesktopY,
                    dwFlags = downUpFlags | User32.MOUSEEVENTF_ABSOLUTE | User32.MOUSEEVENTF_VIRTUALDESK,
                    dwExtraInfo = magic
                }
            },
            // Move cursor back to original position
            new()
            {
                type = User32.INPUT_MOUSE,
                mi = new User32.MOUSEINPUT
                {
                    dx = absOriginalX,
                    dy = absOriginalY,
                    dwFlags = User32.MOUSEEVENTF_MOVE | User32.MOUSEEVENTF_ABSOLUTE | User32.MOUSEEVENTF_VIRTUALDESK,
                    dwExtraInfo = magic
                }
            }
        };

        User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<User32.INPUT>());

        return true; // Swallow the original click
    }
}
