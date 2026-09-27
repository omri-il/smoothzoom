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
| Ctrl+Alt+H | Toggle cursor highlight ring (also turns on by itself while OBS records) |
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
│   ├── CursorHighlightService.cs # Cursor ring overlay + click ripple
│   ├── ObsRecordingWatcher.cs # obs-websocket v5 client: recording on/off → ring on/off
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
- Start with Windows: enabled (only written to the registry when Settings is saved)
- Cursor ring: yellow `#DCFFE632`, 70 px, 4 px thick, faint fill `HighlightFill` `#30FFE632`
  (`HighlightThickness` / `HighlightFill` / `ClickRipple` / `AutoRingWhileRecording` are
  settings.json-only — the dialog carries them over untouched)

### Cursor ring, click ripple, auto-on while recording
- The ring is an ordinary topmost click-through window, so OBS **Display Capture records it**
  and OBS's F9 zoom-to-mouse (a crop filter) enlarges it with everything else.
- **Click ripple:** left click = ring colour, right click = red; grows to 2× and fades in
  350 ms, drawn inside the ring window (which is sized for it). Only while the ring is on.
  Injected clicks (`LLMHF_INJECTED`, e.g. the magnifier's click translation) are skipped so
  one click never ripples twice.
- **Auto-on:** `ObsRecordingWatcher` connects to `ws://127.0.0.1:<port>`, reading port and
  password from OBS's own `%APPDATA%\obs-studio\plugin_config\obs-websocket\config.json` on
  every connect (no copy of the secret here). `RecordStateChanged` STARTED → ring on;
  STOPPED → ring off, **but only if the watcher turned it on** — a ring switched on with
  Ctrl+Alt+H stays on, and pressing Ctrl+Alt+H mid-recording hands control back to you.
  OBS closed = retry every 5 s, treated as "not recording". Log: `%LOCALAPPDATA%\SmoothZoom\obs.log`
  (state changes and distinct errors only).

### Known Limitations
- **Multi-monitor:** `MagSetFullscreenTransform` zooms ALL monitors (Windows API limitation). OBS still captures correctly since it records a specific source.
- **Text blur during panning:** Minimized by rounding offsets to nearest pixel and adaptive cursor tracking.

---

## SmoothAnnotate (`src/SmoothAnnotate/`)

Transparent overlay for screen drawing, shapes, laser pointer, and fun effects. Designed for video tutorials with Wacom stylus support.

### Hotkeys
| Key | Action |
|-----|--------|
| F8 | Toggle draw mode (was F9 until 2026-09-27 — F9 is OBS zoom-to-mouse) |
| F10 | Clear all |
| F11 | Laser pointer |
| F12 | Timer start/pause (double-tap = reset) |
| Ctrl+0 | Mouse mode (click-through, toolbar collapses to dot) |
| Ctrl+1 | Pen |
| Ctrl+2 | Highlighter |
| Ctrl+3 | Laser |
| Ctrl+4 | Eraser |
| Ctrl+5 | Arrow |
| Ctrl+6 | Rectangle |
| Ctrl+7 | Circle |
| Ctrl+8 | Text |
| Ctrl+V | Paste image from clipboard |
| Ctrl+Alt+A | Arrow tool |
| Ctrl+Alt+R | Rectangle tool |
| Ctrl+Alt+O | Circle/Oval tool |
| Ctrl+Alt+X | Text tool |
| Ctrl+Alt+T | Timer show/hide |
| Ctrl+Alt+1-5 | Colors: Red, Blue, Green, White, Yellow |

### Toolbar Features
- **Excalidraw-style horizontal bar** at top-center of screen (draggable)
- **Mouse/Pointer** — exits draw mode. With `HideToolbarWhenIdle` (default **on** since 2026-09-27) the toolbar **hides completely** — it sits on the recorded screen, so any visible toolbar or dot ends up in every OBS video; F8 / Ctrl+1-8 bring it back. With it off: collapses to a small floating dot; click the dot to re-expand
- **Select/Move** — drag ink strokes and shapes to reposition; arrows move as one piece (line + head)
- **Pen** — pressure-sensitive Wacom support, subtle shadow
- **Highlighter** — semi-transparent yellow, rectangle tip
- **Eraser** — stroke-level removal
- **Laser** — single-stroke fade with glow, configurable fade duration
- **Shapes** — Arrow (sharp pointy head), Rectangle, Circle — all with drop shadows
- **Text** — Hebrew RTL auto-detect, 4 sizes (Small 24 / Medium 32 / Large 48 / XL 72)
- **Color picker** — 5 colors with glow swatches
- **Confetti** — 60-particle burst with physics (gravity, spin, fade)
- **Timer** — Stopwatch HUD, double-tap to reset
- **Close** — X button in toolbar header
- **Paste image** — Ctrl+V pastes clipboard image as draggable element on overlay

### Structure
```
src/SmoothAnnotate/
├── App.xaml(.cs)              # Entry point, tray icon, service wiring
├── GlobalUsings.cs            # Resolves WPF/WinForms type ambiguities
├── Models/
│   ├── AnnotationSettings.cs  # Settings POCO
│   └── AnnotationTool.cs      # Tool enum (None/Pen/Highlighter/Eraser/Laser/Arrow/Rectangle/Circle/Text/Select)
├── Native/
│   ├── User32.cs              # P/Invoke: hooks, window styles, monitors, SetWindowPos
│   └── Kernel32.cs            # P/Invoke: GetModuleHandle
├── Services/
│   ├── KeyboardHookService.cs # F8, F10-F12, Ctrl+0-8, Ctrl+V, Ctrl+Alt combos
│   ├── LaserService.cs        # Laser fade-out timer (single-stroke approach)
│   ├── StopwatchService.cs    # Timer with double-tap reset
│   ├── ConfettiService.cs     # Particle physics confetti
│   ├── OverlayService.cs      # Win32 click-through toggling, z-order
│   └── SettingsService.cs     # JSON persistence
└── Views/
    ├── OverlayWindow.xaml(.cs)  # Fullscreen transparent overlay (InkCanvas + ShapeCanvas + ConfettiCanvas)
    ├── ToolbarWindow.xaml(.cs)  # Horizontal dark toolbar (draggable, collapsible to dot)
    └── ToastWindow.xaml(.cs)    # Mode indicator popup
```

### Key Technical Patterns
- **Click-through overlay:** `WS_EX_TRANSPARENT` toggled via Win32 `SetWindowLong`. Background: `Transparent` when click-through, `#01000000` (alpha=1) when drawing.
- **Toolbar clickable in draw mode:** 50ms `DispatcherTimer` checks cursor position via `GetCursorPos`, temporarily sets overlay click-through when hovering over toolbar. DPI-aware using `PresentationSource.TransformToDevice`.
- **Toolbar collapse:** When mouse mode is selected, toolbar collapses to a 42px floating dot. Click dot to re-expand and return to Pen mode.
- **Single-monitor overlay:** `MonitorFromPoint` + `GetMonitorInfo` constrains overlay to cursor's monitor when entering draw mode.
- **WS_EX_NOACTIVATE** on overlay so toolbar keeps focus.
- **Arrow pairing:** `_arrowPairs` dictionary maps Line↔Polygon so Select tool moves both together.
- **Delegate pinning:** Hook delegates stored as class fields to prevent GC collection.

### Settings
Stored at `%APPDATA%\SmoothAnnotate\settings.json`. `HideToolbarWhenIdle` (default true) — see Mouse/Pointer above.

### Debug Log
Written to `%LOCALAPPDATA%\SmoothAnnotate\debug.log`

### Planned (Tier 1 — not yet built)
1. **Undo/Redo** (Ctrl+Z / Ctrl+Y) — UndoService with combined ink+shape stack
2. **Pen size toggle** — Thin/Medium/Thick buttons in toolbar
3. **Filled shapes** — Outline / Tinted / Solid fill mode for Rect + Circle
4. **Export PNG** (Ctrl+E) — renders annotations to clipboard as transparent PNG
5. **10 colors** — adds Orange, Pink, Purple, Teal, Gray to palette

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
- **No hotkey conflicts:** SmoothZoom uses Ctrl+Alt, SmoothAnnotate uses F-keys + Ctrl+number (different patterns).
  ⚠️ **F9 belongs to OBS** (zoom-to-mouse, OBS-dashboard repo) — never bind it here. SmoothAnnotate's
  hook passes keys on, so a shared key fires both apps at once.

## Home PC deploy (where Omri records)
The home PC has **no .NET SDK** (and C: is nearly full), so build on the laptop as
self-contained single files and copy them over:

```bash
dotnet publish src/SmoothZoom/SmoothZoom.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/SmoothZoom
dotnet publish src/SmoothAnnotate/SmoothAnnotate.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/SmoothAnnotate
```

- Installed at **`E:pps\SmoothTools\`** (`SmoothZoom\`, `SmoothAnnotate\`) on the home PC.
- Started by the logon scheduled task **"SmoothTools"** (runs `E:pps\SmoothTools\start.vbs`,
  which launches whichever of the two is not already running — safe to re-run). `schtasks /run /tn SmoothTools` over SSH starts them in Omri's desktop
  session — a process launched straight from SSH runs in session 0 and draws nothing.
- To redeploy: stop both (`Stop-Process -Name SmoothZoom,SmoothAnnotate`), copy the new
  `publish/` output over, `schtasks /run /tn SmoothTools`.
- Its `%APPDATA%\SmoothZoom\settings.json` has `StartWithWindows: false`, so the task is the
  only autostart (a second instance would pop an "already running" box).
