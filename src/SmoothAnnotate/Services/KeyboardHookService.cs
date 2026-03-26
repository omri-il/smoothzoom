using System.Runtime.InteropServices;
using SmoothAnnotate.Native;

namespace SmoothAnnotate.Services;

public class KeyboardHookService : IDisposable
{
    private IntPtr _kbHookId = IntPtr.Zero;

    // CRITICAL: Store delegate as class field to prevent GC collection
    private readonly User32.LowLevelKeyboardProc _kbHookProc;

    private bool _ctrlPressed;
    private bool _altPressed;

    // Core tools
    public event Action? DrawModeToggled;
    public event Action? ClearInk;
    public event Action? LaserToggled;
    public event Action? TimerToggled;
    public event Action? TimerVisibilityToggled;

    // Shape tools
    public event Action? ArrowToggled;
    public event Action? RectangleToggled;
    public event Action? CircleToggled;
    public event Action? TextToggled;

    // Color picker (1-5)
    public event Action<int>? ColorChanged;

    // Tool number shortcuts (Ctrl+1 through Ctrl+8, Ctrl+0 = mouse)
    public event Action<int>? ToolShortcut;

    // Clipboard paste
    public event Action? ClipboardPaste;

    public KeyboardHookService()
    {
        _kbHookProc = KeyboardHookCallback;
        Install();
    }

    private void Install()
    {
        var moduleHandle = Kernel32.GetModuleHandle(null);
        App.Log($"GetModuleHandle returned: {moduleHandle}");

        _kbHookId = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _kbHookProc, moduleHandle, 0);
        if (_kbHookId == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            App.Log($"HOOK FAILED! Error code: {err}");
            throw new InvalidOperationException($"Failed to install keyboard hook. Error: {err}");
        }
        App.Log($"Keyboard hook installed OK. Handle: {_kbHookId}");
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN;

            // Track modifier state
            switch (kbd.vkCode)
            {
                case 0xA2 or 0xA3: // VK_LCONTROL / VK_RCONTROL
                    _ctrlPressed = isKeyDown;
                    break;
                case 0xA4 or 0xA5: // VK_LMENU / VK_RMENU (Alt)
                    _altPressed = isKeyDown;
                    break;
            }

            if (isKeyDown)
            {
                // F-key hotkeys (no modifiers needed - simple and reliable)
                switch (kbd.vkCode)
                {
                    case 0x78: // F9 - Toggle draw mode
                        App.Log("F9 pressed -> DrawModeToggled");
                        DrawModeToggled?.Invoke();
                        break;
                    case 0x79: // F10 - Clear canvas
                        App.Log("F10 pressed -> ClearInk");
                        ClearInk?.Invoke();
                        break;
                    case 0x7A: // F11 - Laser pointer
                        App.Log("F11 pressed -> LaserToggled");
                        LaserToggled?.Invoke();
                        break;
                    case 0x7B: // F12 - Timer start/pause
                        App.Log("F12 pressed -> TimerToggled");
                        TimerToggled?.Invoke();
                        break;
                }

                // Ctrl+number shortcuts for tools (Ctrl only, no Alt)
                if (_ctrlPressed && !_altPressed)
                {
                    switch (kbd.vkCode)
                    {
                        case 0x31: ToolShortcut?.Invoke(1); break; // Ctrl+1 = Pen
                        case 0x32: ToolShortcut?.Invoke(2); break; // Ctrl+2 = Highlighter
                        case 0x33: ToolShortcut?.Invoke(3); break; // Ctrl+3 = Laser
                        case 0x34: ToolShortcut?.Invoke(4); break; // Ctrl+4 = Eraser
                        case 0x35: ToolShortcut?.Invoke(5); break; // Ctrl+5 = Arrow
                        case 0x36: ToolShortcut?.Invoke(6); break; // Ctrl+6 = Rectangle
                        case 0x37: ToolShortcut?.Invoke(7); break; // Ctrl+7 = Circle
                        case 0x38: ToolShortcut?.Invoke(8); break; // Ctrl+8 = Text
                        case 0x30: ToolShortcut?.Invoke(0); break; // Ctrl+0 = Mouse/Pointer
                        case 0x56: ClipboardPaste?.Invoke(); break; // Ctrl+V = Paste image
                    }
                }

                // Ctrl+Alt hotkeys for less frequent tools
                if (_ctrlPressed && _altPressed)
                {
                    switch (kbd.vkCode)
                    {
                        case 0x54: // Ctrl+Alt+T - Timer visibility
                            TimerVisibilityToggled?.Invoke();
                            break;
                        case 0x41: // Ctrl+Alt+A - Arrow
                            ArrowToggled?.Invoke();
                            break;
                        case 0x52: // Ctrl+Alt+R - Rectangle
                            RectangleToggled?.Invoke();
                            break;
                        case 0x4F: // Ctrl+Alt+O - Circle (Oval)
                            CircleToggled?.Invoke();
                            break;
                        case 0x58: // Ctrl+Alt+X - Text
                            TextToggled?.Invoke();
                            break;
                        case 0x31: // Ctrl+Alt+1 - Red
                            ColorChanged?.Invoke(1);
                            break;
                        case 0x32: // Ctrl+Alt+2 - Blue
                            ColorChanged?.Invoke(2);
                            break;
                        case 0x33: // Ctrl+Alt+3 - Green
                            ColorChanged?.Invoke(3);
                            break;
                        case 0x34: // Ctrl+Alt+4 - White
                            ColorChanged?.Invoke(4);
                            break;
                        case 0x35: // Ctrl+Alt+5 - Yellow
                            ColorChanged?.Invoke(5);
                            break;
                    }
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
