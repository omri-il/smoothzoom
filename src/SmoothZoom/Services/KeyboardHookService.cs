using System.Runtime.InteropServices;
using System.Windows.Threading;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class KeyboardHookService : IDisposable
{
    private IntPtr _kbHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private readonly DispatcherTimer _watchdog;

    // CRITICAL: Store delegates as class fields to prevent GC collection
    private readonly User32.LowLevelKeyboardProc _kbHookProc;
    private readonly User32.LowLevelMouseProc _mouseHookProc;

    public event Action? ToggleZoomPressed;
    public event Action? PanicResetPressed;
    public event Action? ViewLockPressed;
    public event Action? ZoomInStepPressed;
    public event Action? ZoomOutStepPressed;
    public event Action? HighlightTogglePressed;
    public event Action? HelpTogglePressed;
    public event Action<bool>? MiddleButtonChanged;

    private ClickTranslationService? _clickTranslation;

    public void SetClickTranslation(ClickTranslationService service)
    {
        _clickTranslation = service;
    }

    public KeyboardHookService()
    {
        _kbHookProc = KeyboardHookCallback;
        _mouseHookProc = MouseHookCallback;
        InstallHooks();

        // Watchdog: reinstall hooks every 30 seconds to recover from Windows unhooking
        _watchdog = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _watchdog.Tick += (_, _) => ReinstallHooks();
        _watchdog.Start();
    }

    private void InstallHooks()
    {
        var moduleHandle = Kernel32.GetModuleHandle(null);

        if (_kbHookId == IntPtr.Zero)
        {
            _kbHookId = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _kbHookProc, moduleHandle, 0);
        }

        if (_mouseHookId == IntPtr.Zero)
        {
            _mouseHookId = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseHookProc, moduleHandle, 0);
        }
    }

    private void ReinstallHooks()
    {
        if (_kbHookId != IntPtr.Zero)
            User32.UnhookWindowsHookEx(_kbHookId);
        if (_mouseHookId != IntPtr.Zero)
            User32.UnhookWindowsHookEx(_mouseHookId);

        _kbHookId = IntPtr.Zero;
        _mouseHookId = IntPtr.Zero;
        InstallHooks();
    }

    /// <summary>
    /// Check modifier state in real-time — never stale, never blocks keyboard.
    /// </summary>
    private static bool IsCtrlHeld() => (User32.GetAsyncKeyState(User32.VK_CONTROL) & 0x8000) != 0;
    private static bool IsAltHeld() => (User32.GetAsyncKeyState(User32.VK_MENU) & 0x8000) != 0;

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            bool isKeyDown = wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN;

            if (isKeyDown && IsCtrlHeld() && IsAltHeld())
            {
                var kbd = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
                bool handled = true;
                switch (kbd.vkCode)
                {
                    case 0x5A: ToggleZoomPressed?.Invoke(); break;       // Z
                    case 0x1B: PanicResetPressed?.Invoke(); break;       // Esc
                    case 0x4C: ViewLockPressed?.Invoke(); break;         // L
                    case 0x48: HighlightTogglePressed?.Invoke(); break;  // H
                    case 0xBF: HelpTogglePressed?.Invoke(); break;       // /
                    case 0xBB: ZoomInStepPressed?.Invoke(); break;       // =/+
                    case 0xBD: ZoomOutStepPressed?.Invoke(); break;      // -/_
                    default: handled = false; break;
                }
                // Only swallow the action key (Z, H, L, etc.) — NEVER swallow Ctrl or Alt
                if (handled) return (IntPtr)1;
            }
        }

        // Always pass through — never block the keyboard
        return User32.CallNextHookEx(_kbHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;

            // Middle button: pan/drag control — never translated
            if (msg == User32.WM_MBUTTONDOWN)
            {
                MiddleButtonChanged?.Invoke(true);
            }
            else if (msg == User32.WM_MBUTTONUP)
            {
                MiddleButtonChanged?.Invoke(false);
            }

            // Click translation for windowed magnifier
            if (_clickTranslation != null)
            {
                var hookData = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
                if (_clickTranslation.TryTranslateClick(msg, hookData))
                    return (IntPtr)1; // Swallow the original click
            }
        }

        return User32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _watchdog.Stop();
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
