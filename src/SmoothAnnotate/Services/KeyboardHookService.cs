using System.Runtime.InteropServices;
using SmoothAnnotate.Native;

namespace SmoothAnnotate.Services;

public class KeyboardHookService : IDisposable
{
    private IntPtr _kbHookId = IntPtr.Zero;

    // CRITICAL: Store delegate as class field to prevent GC collection
    private readonly User32.LowLevelKeyboardProc _kbHookProc;

    private bool _ctrlPressed;
    private bool _shiftPressed;

    public event Action? DrawModeToggled;
    public event Action? ClearInk;
    public event Action? LaserToggled;
    public event Action? TimerToggled;
    public event Action? TimerVisibilityToggled;

    public KeyboardHookService()
    {
        _kbHookProc = KeyboardHookCallback;
        Install();
    }

    private void Install()
    {
        var moduleHandle = Kernel32.GetModuleHandle(null);

        _kbHookId = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _kbHookProc, moduleHandle, 0);
        if (_kbHookId == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");
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
                case 0xA0 or 0xA1: // VK_LSHIFT / VK_RSHIFT
                    _shiftPressed = isKeyDown;
                    break;
                case 0xA2 or 0xA3: // VK_LCONTROL / VK_RCONTROL
                    _ctrlPressed = isKeyDown;
                    break;
            }

            if (isKeyDown && _ctrlPressed && _shiftPressed)
            {
                switch (kbd.vkCode)
                {
                    case 0x44: // VK_D
                        DrawModeToggled?.Invoke();
                        break;
                    case 0x43: // VK_C
                        ClearInk?.Invoke();
                        break;
                    case 0x4C: // VK_L
                        LaserToggled?.Invoke();
                        break;
                    case 0x53: // VK_S
                        TimerToggled?.Invoke();
                        break;
                    case 0x54: // VK_T
                        TimerVisibilityToggled?.Invoke();
                        break;
                }
            }
        }

        return User32.CallNextHookEx(_kbHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_kbHookId != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_kbHookId);
            _kbHookId = IntPtr.Zero;
        }
    }
}
