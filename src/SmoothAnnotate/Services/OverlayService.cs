using System.Runtime.InteropServices;
using SmoothAnnotate.Native;

namespace SmoothAnnotate.Services;

public static class OverlayService
{
    public static void SetClickThrough(IntPtr hwnd)
    {
        int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_TRANSPARENT);
    }

    public static void RemoveClickThrough(IntPtr hwnd)
    {
        int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle & ~User32.WS_EX_TRANSPARENT);
    }

    public static void HideFromAltTab(IntPtr hwnd)
    {
        int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_TOOLWINDOW);
    }

    public static void SetNoActivate(IntPtr hwnd)
    {
        int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_NOACTIVATE);
    }

    /// <summary>You see the window; OBS, screenshots and screen shares don't. For the
    /// controls only (toolbar, dot, mode labels) — never the overlay, whose ink is the point.</summary>
    public static void HideFromCapture(IntPtr hwnd, string name)
    {
        if (User32.SetWindowDisplayAffinity(hwnd, User32.WDA_EXCLUDEFROMCAPTURE))
            App.Log($"{name}: hidden from screen capture");
        else
            App.Log($"{name}: HIDE FROM CAPTURE FAILED, error {Marshal.GetLastWin32Error()} (it will be recorded)");
    }

    public static void RaiseToTop(IntPtr hwnd)
    {
        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
    }
}
