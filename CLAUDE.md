# SmoothZoom

Lightweight Windows background utility for smooth, GPU-accelerated screen zooming. Designed for OBS tutorial recordings.

## Tech Stack
- **Language:** C# / .NET 8 (WPF)
- **Core API:** Windows Magnification.dll (`MagSetFullscreenTransform`) — native GPU-accelerated zoom
- **Input:** Global keyboard hooks (`WH_KEYBOARD_LL`) + mouse hooks (`WH_MOUSE_LL`)
- **UI:** WPF for Settings window + System tray via `System.Windows.Forms.NotifyIcon`
- **No NuGet dependencies** — everything is built-in .NET 8

## Build & Run
```bash
# Requires .NET 8 SDK (installed at ~/.dotnet)
export PATH="$HOME/.dotnet:$PATH"
dotnet build
dotnet run --project src/SmoothZoom
```

## Hotkeys
| Shortcut | Action |
|----------|--------|
| Ctrl+Alt+Z | Toggle zoom in/out |
| Ctrl+Alt+Scroll | Adjust zoom level (0.25x steps) |
| Ctrl+Alt+L | Lock/unlock viewport |
| Ctrl+Alt+H | Toggle cursor highlight ring |
| Ctrl+Alt+/ | Show/hide help overlay |
| Ctrl+Alt+Esc | Panic reset (instant zoom out) |

## Project Structure
```
src/SmoothZoom/
├── App.xaml(.cs)              # App lifecycle, tray icon, crash recovery
├── app.manifest               # DPI awareness
├── Native/
│   ├── MagnificationApi.cs    # P/Invoke: Magnification.dll
│   ├── User32.cs              # P/Invoke: hooks, cursor, monitors
│   └── Kernel32.cs            # P/Invoke: GetModuleHandle
├── Services/
│   ├── ZoomController.cs      # Core: easing, state machine, cursor tracking
│   ├── MagnificationService.cs # Zoom transform + offset math + edge clamping
│   ├── KeyboardHookService.cs # Global keyboard + mouse hooks
│   ├── CursorHighlightService.cs # Cursor ring overlay
│   └── SettingsService.cs     # JSON persistence
├── Models/
│   └── AppSettings.cs         # Settings POCO
└── Views/
    ├── SettingsWindow.xaml(.cs)  # Settings UI
    └── HelpOverlay.xaml(.cs)    # Hotkey reference card
```

## Settings
Stored at `%APPDATA%\SmoothZoom\settings.json`. Defaults:
- Zoom level: 2.0x (range: 1.5–4.0x)
- Zoom speed: 300ms (range: 100–800ms)
- Cursor tracking: 0.15 (medium)
- Start with Windows: enabled

## Known Limitations
- **Multi-monitor:** `MagSetFullscreenTransform` zooms ALL monitors (Windows API limitation). OBS still captures correctly since it records a specific source. Per-monitor zoom would require switching to windowed magnifier API (major rewrite).
- **Text blur during panning:** Minimized by rounding offsets to nearest pixel and adaptive cursor tracking, but some blur is inherent to the magnification API's bilinear interpolation.

## Key Technical Details
- **Thread affinity:** All Magnification API calls must stay on UI thread (DispatcherTimer at 16ms)
- **Delegate pinning:** Keyboard/mouse hook delegates stored as class fields to prevent GC
- **Crash recovery:** Resets zoom on startup + on unhandled exceptions
- **DPI awareness:** PerMonitorV2 via ApplicationHighDpiMode project property
- **Easing:** Cubic ease-in-out for zoom animation
- **Cursor tracking:** Lerp with adaptive snapping (eliminates sub-pixel jitter when still)
