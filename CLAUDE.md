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
    ├── PenInkCanvas.cs          # InkCanvas that ignores finger/palm touches (IgnoreTouch)
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
Stored at `%APPDATA%\SmoothAnnotate\settings.json`. `HideToolbarWhenIdle` (default true) — see Mouse/Pointer above. `IgnoreTouch` (default true)
and `PassThroughWindowTitle` — see "Pen and touch" below.

### Debug Log
Written to `%LOCALAPPDATA%\SmoothAnnotate\debug.log`

### Planned (Tier 1 — not yet built)
1. **Undo/Redo** (Ctrl+Z / Ctrl+Y) — UndoService with combined ink+shape stack
2. **Pen size toggle** — Thin/Medium/Thick buttons in toolbar
3. **Filled shapes** — Outline / Tinted / Solid fill mode for Rect + Circle
4. **Export PNG** (Ctrl+E) — renders annotations to clipboard as transparent PNG
5. **10 colors** — adds Orange, Pink, Purple, Teal, Gray to palette

---

## Control from other programs (OBS dashboard, Start menu, pen button)
Both apps are controlled from outside through a **named pipe** (`src/Shared/ControlPipe.cs`,
compiled into both via a linked `<Compile>` in each csproj). One text command goes in and
one JSON line comes back, and every reply carries the app's current state. Only the same
Windows user can connect (`PipeOptions.CurrentUserOnly`). There are 4 instances, so a few
callers at once never see "pipe busy".

| Pipe | Commands | Reply |
|---|---|---|
| `\\.\pipe\SmoothZoom.control` | `status` · `ring toggle` · `ring on` · `ring off` | `{ok, ring, auto, obs, recording}` (`auto` = the recording watcher turned it on) |
| `\\.\pipe\SmoothAnnotate.control` | `status` · `draw toggle` · `draw off` · `laser toggle` · `clear` | `{ok, drawing, tool}` |

- **`ring …` from any caller counts as "by hand"**, exactly like Ctrl+Alt+H. It takes over
  from auto-on, so a recording's end leaves the ring alone (`SetRingByHand`).
- **`draw toggle` is on/off, not F8's cycle.** It switches between pen and mouse. From the
  laser it goes to the pen, because the remote has a separate laser button
  (`ToggleDrawOnOff`).
- Commands run on the UI thread through `ControlPipeServer.OnUi`, which gives up after
  1.5 s and replies `busy`. The reply is always written, with its own timer.
  ⚠️ **Until that fix, one 2 s timer covered read + handle + write.** A slow UI moment then
  cancelled the write, and the caller got an EMPTY reply.
- ⚠️ **`ControlPipeServer.Send` runs on the thread pool** (`Task.Run`). The first version
  waited on the async pipe calls straight from the second launch's `OnStartup`, which is
  the UI thread. Their continuations queued for that same waiting thread, so every
  `--toggle` copy hung forever after delivering its command (2026-09-27: two copies of
  SmoothZoom stuck).
- ⚠️ **The server never calls `WaitForPipeDrain()`.** It blocks a thread with no timeout,
  and those hung callers held an instance each. After replying, it waits at most 2 s for
  the caller to hang up (`WaitForHangUpAsync`).
- A `--toggle` launched from an SSH session (session 0) exits without reaching the
  running copy in the desktop session. A real tap, or a test through an Interactive
  scheduled task, works. Python's plain `open()` on the pipe does work from SSH.
- **Measured 2026-09-27:** warm replies take 1–6 ms. The first calls after an app starts
  took up to ~1 s, so callers wait 1.5 s (`smooth.TIMEOUT` in OBS-dashboard).
- **Command-line flags:**
  - `--toggle`: if the app is already running, send it `ring toggle` / `draw toggle` over
    the pipe and exit quietly. Otherwise start and switch on. This is what the Start-menu
    entries and the pen's top button run.
  - `--autostart`: already running = exit quietly (`start.vbs`).
  - A plain second launch still shows the "already running" box.
- **The OBS dashboard's remote** (OBS-dashboard repo, `smooth.py` + `POST /api/smooth`)
  has four buttons on this pipe: ring, draw, laser, clear. They are lit from `status`.

## Pen and touch (the laptop: HP OmniBook Ultra Flip 14, touch screen + pen)
- **Only the pen draws** (`IgnoreTouch`, default true, `Views/PenInkCanvas.cs`). A finger or
  palm on the drawing layer does nothing. The mouse and pen are unchanged, and a machine
  without touch sees no difference. Two places must agree:
  - The routed stylus events: a touch is marked handled, so no stroke is collected.
  - The `DynamicRenderer`: it draws live ink on WPF's pen thread BEFORE those events.
    Without its filter a finger would leave a trail that vanishes on lift-off. The pen
    thread can't query tablets, so the touch digitizers' ids are collected on the UI
    thread at load.
  - Shapes, text and select ignore mouse events that came from touch (`IsFromTouch`).
- **The remote stays clickable while drawing** (`PassThroughWindowTitle`, default
  "מרכז השליטה של OBS"). The full-screen drawing layer would otherwise cover it, including
  its "stop drawing" button. The 50 ms hit timer that already let clicks through to the
  toolbar does the same over that window (`FindWindow`, looked up once a second), and
  entering draw mode raises it above the layer.
- **Ring size:** the laptop's `settings.json` has `HighlightRingSize: 60`. At 200%
  scaling a ring is 2× its size in pixels, and OBS shrinks that screen to 0.6× (1800 →
  1080), so 60 comes out at ~72 px in the video, matching the home PC's 70. This is a
  per-machine value, never a code default.
- HP's F-keys are media keys unless Fn is held, so **Fn+F8** draws there. That is why the
  laptop is driven from the remote, the Start menu and the pen instead.

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

## Install / update (laptop + home PC)
Both machines run the same self-contained build — no .NET needed on the target. Only the
**laptop** has the .NET 8 SDK (the home PC has none, and its C: is nearly full), so it builds
for both.

| Machine | Installed at |
|---|---|
| Laptop | `%LOCALAPPDATA%\Programs\SmoothTools` |
| Home PC (where Omri records) | `E:\apps\SmoothTools` |

1. **Build** on the laptop, from the repo root: `powershell -File deploy\publish.ps1`. It writes
   `publish\SmoothTools\` (gitignored): `SmoothZoom\`, `SmoothAnnotate\`, `start.vbs`, `install.ps1`.
   The exe icons are `assets/*.ico`, drawn by `deploy/make_icons.py` (Pillow, run once,
   committed): a yellow ring on a dark tile, and a pencil on a red tile. The tray icons
   are the same icons, read back from the exe.
2. **Laptop:** `& publish\SmoothTools\install.ps1 -Target "$env:LOCALAPPDATA\Programs\SmoothTools"`
3. **Home PC:** `scp -r publish/SmoothTools omrii@100.111.186.101:E:/apps/SmoothTools-incoming`, then
   over SSH `powershell -ExecutionPolicy Bypass -File E:\apps\SmoothTools-incoming\install.ps1 -Target E:\apps\SmoothTools`,
   then delete `SmoothTools-incoming`.

What `deploy\install.ps1` does (safe to re-run; that is how you update):
- Stops both apps, waits for them to exit, and copies the new files in. Windows can hold an
  exe's file lock for a moment after the process ends, so the copy retries.
- Registers the logon scheduled task **"SmoothTools"**, which runs `start.vbs` from the install
  folder. `start.vbs` launches both apps with `--autostart`, and one that is already running
  exits quietly. The task starts them in the logged-on desktop. That is why it also works
  over SSH, where a process launched directly would run in invisible session 0.
  ⚠️ `start.vbs` used to ask WMI which apps were running. That failed after an update: a
  process stopped a moment earlier stays listed while anything still holds a handle to it
  (install.ps1's own PowerShell did), so SmoothAnnotate was skipped.
- Writes two **Start-menu** entries, `Start Menu\Programs\SmoothTools\`: **"Cursor ring -
  SmoothZoom"** and **"Draw - SmoothAnnotate"**. Both run the exe with `--toggle`, so a tap
  switches the ring or drawing on and off. They can be pinned to the taskbar or picked for
  the pen's top button. The names are English because WScript.Shell reads Hebrew-named
  `.lnk` files as empty.
- 🚨 **The laptop has Smart App Control ON** (read 2026-09-27: `Get-MpComputerStatus` →
  `SmartAppControlState: On`; the home PC is Off). SAC blocks unsigned programs it has no
  good cloud verdict for. **Every new build is judged again, and the verdict varies.** On
  2026-09-27 one build ran SmoothZoom and blocked SmoothAnnotate (CodeIntegrity event
  3077, "did not meet the Enterprise signing level requirements"). The next build, with
  only the pipe fix, was let through for both after ~20 s. So after any update, check that
  both apps are running on the laptop. Launched by `start.vbs`, the block shows up as a
  "Windows Script Host" error box. The check: `Get-WinEvent -LogName
  'Microsoft-Windows-CodeIntegrity/Operational'`, event 3077 naming the exe. Never try to
  get around SAC. The only ways forward are Omri turning it off, or a real code-signing
  certificate.
- ⚠️ **A NEW build starts late, once.** Microsoft Defender holds an unknown unsigned exe for
  a cloud scan on its first run: SmoothAnnotate took ~1–2 min on 2026-09-27, while the
  install printed `running: SmoothZoom` only. Wait and check again before debugging. The
  file also reads as "in use" during the scan, so an install run straight after another
  can fail its copy; re-run it.
- Makes the task the **only** autostart. It seeds `%APPDATA%\SmoothZoom\settings.json` with
  `StartWithWindows: false` (only if there is no settings file yet) and removes SmoothZoom's
  HKCU `Run` value. A second instance would pop an "already running" box.
- Ends by printing `running: SmoothAnnotate, SmoothZoom`. After that,
  `%LOCALAPPDATA%\SmoothZoom\obs.log` should say `connected to OBS` if OBS is open.
