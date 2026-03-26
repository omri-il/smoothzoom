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

    public static void RaiseToTop(IntPtr hwnd)
    {
        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
    }
}
