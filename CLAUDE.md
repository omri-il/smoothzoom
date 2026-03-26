# SmoothZoom + SmoothAnnotate

Two companion WPF desktop tools for video tutorial recording: screen zoom + screen annotation overlay.

## SmoothZoom (`src/SmoothZoom/`)

Lightweight Windows background utility for smooth, GPU-accelerated screen zooming. Designed for OBS tutorial recordings.

### Tech Stack
- **Language:** C# / .NET 8 (WPF)
- **Core API:** Windows Magnification.dll (`MagSetFullscreenTransform`) — native GPU-accelerated zoom
- **Input:** Global keyboard hooks (`WH_KEYBOARD_LL`) + mouse hooks (`WH_MOUSE_LL`)
- **UI:** WPF for Settings window + System tray via `System.Windows.Forms.NotifyIcon`
- **No NuGet dependencies** — everything is built-in .NET 8

### Hotkeys
| Shortcut | Action |
|----------|--------|
| Ctrl+Alt+Z | Toggle zoom in/out |
| Ctrl+Alt+Plus | Zoom in more (+0.25x) |
| Ctrl+Alt+Minus | Zoom out (-0.25x) |
| Ctrl+Alt+L | Toggle cursor tracking (default: off) |
| Middle-click drag | Pan the zoomed view |
| Ctrl+Alt+H | Toggle cursor highlight ring |
| Ctrl+Alt+/ | Show/hide help overlay |
| Ctrl+Alt+Esc | Panic reset (instant zoom out) |

### Structure
```
src/SmoothZoom/
├── App.xaml(.cs)              # App lifecycle, tray icon, crash recovery
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

### Settings
Stored at `%APPDATA%\SmoothZoom\settings.json`. Defaults:
- Zoom level: 2.0x (range: 1.5–4.0x)
- Zoom speed: 300ms (range: 100–800ms)
- Cursor tracking: 0.15 (medium)
- Start with Windows: enabled

### Known Limitations
- **Multi-monitor:** `MagSetFullscreenTransform` zooms ALL monitors (Windows API limitation). OBS still captures correctly since it records a specific source.
- **Text blur during panning:** Minimized by rounding offsets to nearest pixel and adaptive cursor tracking.

---

## SmoothAnnotate (`src/SmoothAnnotate/`)

Transparent overlay for screen drawing, shapes, laser pointer, and fun effects. Designed for video tutorials with Wacom stylus support.

### Hotkeys
| Key | Action |
|-----|--------|
| F9 | Cycle: Pen -> Highlighter -> Eraser -> Off |
| F10 | Clear all |
| F11 | Laser pointer |
| F12 | Timer start/pause (double-tap = reset) |
| Ctrl+Alt+A | Arrow tool |
| Ctrl+Alt+R | Rectangle tool |
| Ctrl+Alt+O | Circle/Oval tool |
| Ctrl+Alt+X | Text tool |
| Ctrl+Alt+T | Timer show/hide |
| Ctrl+Alt+1-5 | Colors: Red, Blue, Green, White, Yellow |

### Toolbar Features
- **Select/Move** - Drag ink strokes and shapes to reposition
- **Pen** - Pressure-sensitive Wacom support, subtle shadow
- **Highlighter** - Semi-transparent yellow, rectangle tip
- **Eraser** - Stroke-level removal
- **Laser** - Single-stroke fade (no timing gap), configurable fade duration
- **Shapes** - Arrow (sharp pointy head), Rectangle, Circle — all with drop shadows
- **Text** - Hebrew RTL auto-detect, 4 sizes (Small 24 / Medium 32 / Large 48 / XL 72)
- **Color picker** - 5 colors with glow swatches
- **Confetti** - 60-particle burst with physics (gravity, spin, fade)
- **Timer** - Stopwatch HUD, double-tap to reset
- **Close** - X button in toolbar header

### Structure
```
src/SmoothAnnotate/
├── App.xaml(.cs)              # Entry point, tray icon, service wiring
├── GlobalUsings.cs            # Resolves WPF/WinForms type ambiguities
├── Models/
│   ├── AnnotationSettings.cs  # Settings POCO
│   └── AnnotationTool.cs      # Tool enum
├── Native/
│   ├── User32.cs              # P/Invoke: hooks, window styles, monitors
│   └── Kernel32.cs            # P/Invoke: GetModuleHandle
├── Services/
│   ├── KeyboardHookService.cs # F9-F12 + Ctrl+Alt combos
│   ├── LaserService.cs        # Laser fade-out timer
│   ├── StopwatchService.cs    # Timer with double-tap reset
│   ├── ConfettiService.cs     # Particle physics confetti
│   ├── OverlayService.cs      # Win32 click-through toggling
│   └── SettingsService.cs     # JSON persistence
└── Views/
    ├── OverlayWindow.xaml(.cs)  # Fullscreen transparent overlay
    ├── ToolbarWindow.xaml(.cs)  # Floating dark toolbar (draggable)
    └── ToastWindow.xaml(.cs)    # Mode indicator popup
```

### Key Technical Patterns
- **Click-through overlay:** `WS_EX_TRANSPARENT` toggled via Win32 `SetWindowLong`. Background: `Transparent` when click-through, `#01000000` (alpha=1) when drawing.
- **Toolbar clickable in draw mode:** 50ms `DispatcherTimer` checks cursor position via `GetCursorPos`, temporarily sets overlay click-through when hovering over toolbar. DPI-aware using `PresentationSource.TransformToDevice`.
- **Single-monitor overlay:** `MonitorFromPoint` + `GetMonitorInfo` constrains overlay to cursor's monitor when entering draw mode. Expands back to virtual screen on exit.
- **WS_EX_NOACTIVATE** on overlay so toolbar keeps focus.
- **Delegate pinning:** Hook delegates stored as class fields to prevent GC collection.

### Settings
Stored at `%APPDATA%\SmoothAnnotate\settings.json`

### Debug Log
Written to `%LOCALAPPDATA%\SmoothAnnotate\debug.log`

---

## Build & Run

```bash
# Requires .NET 8 SDK
# If not in PATH: export PATH="$LOCALAPPDATA/dotnet:$PATH"

# Build both
dotnet build src/SmoothZoom/SmoothZoom.csproj
dotnet build src/SmoothAnnotate/SmoothAnnotate.csproj

# Run (can run both simultaneously)
start src/SmoothZoom/bin/Debug/net8.0-windows/SmoothZoom.exe
start src/SmoothAnnotate/bin/Debug/net8.0-windows/SmoothAnnotate.exe
```

## Key Technical Details
- **Thread affinity:** All Magnification API calls must stay on UI thread (DispatcherTimer at 16ms)
- **Crash recovery:** SmoothZoom resets zoom on startup + on unhandled exceptions
- **DPI awareness:** PerMonitorV2 via ApplicationHighDpiMode project property
- **Easing:** Cubic ease-in-out for zoom animation
- **Cursor tracking:** Lerp with adaptive snapping (eliminates sub-pixel jitter when still)
- **No hotkey conflicts:** SmoothZoom uses Ctrl+Alt, SmoothAnnotate uses F-keys + Ctrl+Alt (different letters)
