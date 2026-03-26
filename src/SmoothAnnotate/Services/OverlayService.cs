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
}
