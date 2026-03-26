using System.Diagnostics;
using System.Runtime.InteropServices;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class KeyboardHookService : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;

    // CRITICAL: Store delegate as class field to prevent GC collection
    private readonly User32.LowLevelKeyboardProc _hookProc;

    private bool _ctrlPressed;
    private bool _altPressed;

    public event Action? ToggleZoomPressed;
    public event Action? PanicResetPressed;
    public event Action? ViewLockPressed;

    public KeyboardHookService()
    {
        _hookProc = HookCallback;
        Install();
    }

    private void Install()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = User32.SetWindowsHookEx(
            User32.WH_KEYBOARD_LL,
            _hookProc,
            Kernel32.GetModuleHandle(module.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN;
            bool isKeyUp = wParam == User32.WM_KEYUP || wParam == User32.WM_SYSKEYUP;

            // Track modifier state
            switch (kbd.vkCode)
            {
                case 0xA2: // VK_LCONTROL
                case 0xA3: // VK_RCONTROL
                    _ctrlPressed = isKeyDown;
                    break;
                case 0xA4: // VK_LMENU (Left Alt)
                case 0xA5: // VK_RMENU (Right Alt)
                    _altPressed = isKeyDown;
                    break;
            }

            // Check hotkeys on key down
            if (isKeyDown)
            {
                // Ctrl+Alt+Z - Toggle zoom
                if (_ctrlPressed && _altPressed && kbd.vkCode == 0x5A) // VK_Z
                {
                    ToggleZoomPressed?.Invoke();
                }
                // Ctrl+Alt+Escape - Panic reset
                else if (_ctrlPressed && _altPressed && kbd.vkCode == 0x1B) // VK_ESCAPE
                {
                    PanicResetPressed?.Invoke();
                }
                // Ctrl+Alt+L - View lock toggle
                else if (_ctrlPressed && _altPressed && kbd.vkCode == 0x4C) // VK_L
                {
                    ViewLockPressed?.Invoke();
                }
            }
        }

        // Always pass to next hook
        return User32.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
