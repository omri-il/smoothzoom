using System.Diagnostics;
using System.Runtime.InteropServices;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class KeyboardHookService : IDisposable
{
    private IntPtr _kbHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;

    // CRITICAL: Store delegates as class fields to prevent GC collection
    private readonly User32.LowLevelKeyboardProc _kbHookProc;
    private readonly User32.LowLevelMouseProc _mouseHookProc;

    private bool _ctrlPressed;
    private bool _altPressed;

    public event Action? ToggleZoomPressed;
    public event Action? PanicResetPressed;
    public event Action? ViewLockPressed;
    public event Action<int>? ScrollWheel; // +1 = scroll up (zoom in), -1 = scroll down (zoom out)

    public KeyboardHookService()
    {
        _kbHookProc = KeyboardHookCallback;
        _mouseHookProc = MouseHookCallback;
        Install();
    }

    private void Install()
    {
        var moduleHandle = Kernel32.GetModuleHandle(null);

        _kbHookId = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _kbHookProc, moduleHandle, 0);
        if (_kbHookId == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");

        _mouseHookId = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseHookProc, moduleHandle, 0);
        if (_mouseHookId == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to install mouse hook. Error: {Marshal.GetLastWin32Error()}");
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN;
            bool isKeyUp = wParam == User32.WM_KEYUP || wParam == User32.WM_SYSKEYUP;

            switch (kbd.vkCode)
            {
                case 0xA2 or 0xA3: // VK_LCONTROL / VK_RCONTROL
                    _ctrlPressed = isKeyDown;
                    break;
                case 0xA4 or 0xA5: // VK_LMENU / VK_RMENU
                    _altPressed = isKeyDown;
                    break;
            }

            if (isKeyDown && _ctrlPressed && _altPressed)
            {
                switch (kbd.vkCode)
                {
                    case 0x5A: // VK_Z
                        ToggleZoomPressed?.Invoke();
                        break;
                    case 0x1B: // VK_ESCAPE
                        PanicResetPressed?.Invoke();
                        break;
                    case 0x4C: // VK_L
                        ViewLockPressed?.Invoke();
                        break;
                }
            }
        }

        return User32.CallNextHookEx(_kbHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == User32.WM_MOUSEWHEEL)
        {
            var mouse = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
            // mouseData high word contains wheel delta (120 = one notch)
            int delta = (short)(mouse.mouseData >> 16);

            // Check if Ctrl+Alt is held using async key state
            bool ctrlHeld = (User32.GetAsyncKeyState(User32.VK_CONTROL) & 0x8000) != 0;
            bool altHeld = (User32.GetAsyncKeyState(User32.VK_MENU) & 0x8000) != 0;

            if (ctrlHeld && altHeld)
            {
                ScrollWheel?.Invoke(delta > 0 ? 1 : -1);
            }
        }

        return User32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_kbHookId != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_kbHookId);
            _kbHookId = IntPtr.Zero;
        }
        if (_mouseHookId != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }
}
