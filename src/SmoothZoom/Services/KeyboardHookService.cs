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

    private bool _ctrlPressed;
    private bool _altPressed;

    public event Action? ToggleZoomPressed;
    public event Action? PanicResetPressed;
    public event Action? ViewLockPressed;
    public event Action<int>? ScrollWheel;
    public event Action? HighlightTogglePressed;
    public event Action? HelpTogglePressed;
    public event Action<int, int>? DragPan; // deltaX, deltaY

    private bool _middleDragging;
    private int _lastDragX;
    private int _lastDragY;

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
        // Unhook and re-hook to recover from Windows silently removing hooks
        if (_kbHookId != IntPtr.Zero)
            User32.UnhookWindowsHookEx(_kbHookId);
        if (_mouseHookId != IntPtr.Zero)
            User32.UnhookWindowsHookEx(_mouseHookId);

        _kbHookId = IntPtr.Zero;
        _mouseHookId = IntPtr.Zero;
        InstallHooks();
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN;

            switch (kbd.vkCode)
            {
                case 0xA2 or 0xA3:
                    _ctrlPressed = isKeyDown;
                    break;
                case 0xA4 or 0xA5:
                    _altPressed = isKeyDown;
                    break;
            }

            if (isKeyDown && _ctrlPressed && _altPressed)
            {
                bool handled = true;
                switch (kbd.vkCode)
                {
                    case 0x5A: ToggleZoomPressed?.Invoke(); break;
                    case 0x1B: PanicResetPressed?.Invoke(); break;
                    case 0x4C: ViewLockPressed?.Invoke(); break;
                    case 0x48: HighlightTogglePressed?.Invoke(); break;
                    case 0xBF: HelpTogglePressed?.Invoke(); break;
                    default: handled = false; break;
                }
                // Swallow the key to prevent Windows "ding" sound
                if (handled) return (IntPtr)1;
            }
        }

        return User32.CallNextHookEx(_kbHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;

            if (msg == User32.WM_MOUSEWHEEL)
            {
                var mouse = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
                int delta = (short)(mouse.mouseData >> 16);

                bool ctrlHeld = (User32.GetAsyncKeyState(User32.VK_CONTROL) & 0x8000) != 0;
                bool altHeld = (User32.GetAsyncKeyState(User32.VK_MENU) & 0x8000) != 0;

                if (ctrlHeld && altHeld)
                {
                    ScrollWheel?.Invoke(delta > 0 ? 1 : -1);
                    return (IntPtr)1;
                }
            }
            else if (msg == User32.WM_MBUTTONDOWN)
            {
                var mouse = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
                _middleDragging = true;
                _lastDragX = mouse.pt.X;
                _lastDragY = mouse.pt.Y;
            }
            else if (msg == User32.WM_MBUTTONUP)
            {
                _middleDragging = false;
            }
            else if (msg == User32.WM_MOUSEMOVE && _middleDragging)
            {
                var mouse = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
                int dx = mouse.pt.X - _lastDragX;
                int dy = mouse.pt.Y - _lastDragY;
                _lastDragX = mouse.pt.X;
                _lastDragY = mouse.pt.Y;

                if (dx != 0 || dy != 0)
                    DragPan?.Invoke(dx, dy);
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
