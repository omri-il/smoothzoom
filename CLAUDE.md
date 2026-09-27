# SmoothZoom + SmoothAnnotate

Two companion WPF desktop tools for video tutorial recording: screen zoom + screen annotation overlay.
Why things are the way they are (dated incidents and decisions): `CHANGELOG.md`, not auto-loaded.

## Repo layout
```
src/SmoothZoom/        the ring + screen zoom app (below)
src/SmoothAnnotate/    the drawing overlay app (below)
src/Shared/ControlPipe.cs   the control pipe, compiled into both ("Control from other programs")
deploy/                publish.ps1 · install.ps1 · start.vbs · make_icons.py ("Install / update")
assets/                smoothzoom.ico · smoothannotate.ico (drawn by deploy/make_icons.py)
tools/touch-test.ps1   touch/pen checks without hands ("Testing touch without hands")
```

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
│   ├── ClickTranslationService.cs # Clicks while zoomed land where they appear (windowed magnifier)
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
- Cursor tracking: 0.25 (the dialog calls it "Loose"; `AppSettings.CursorTrackingSpeed`)
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
| F8 | Toggle draw mode (never F9 — that is OBS zoom-to-mouse) |
| F10 | Clear all |
| F11 | Laser pointer on/off — pressed again it goes back to the mouse, never to the pen (the screen would stay covered) |
| Esc | Stop drawing (back to the mouse). While typing text, the first Esc finishes the text. Swallowed only when it stopped something, so it still reaches the app underneath otherwise |
| F12 | Timer start/pause (double-tap = reset) |
| Ctrl+0 | Mouse mode (click-through; the toolbar hides — see Mouse/Pointer below) |
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
- **Mouse/Pointer** — exits draw mode. With `HideToolbarWhenIdle` (default **on**) the toolbar **hides completely** — it sits on the recorded screen, so any visible toolbar or dot ends up in every OBS video; F8 / Ctrl+1-8 bring it back. With it off: collapses to a small floating dot; click the dot to re-expand
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
- **Close** — ✕ in the toolbar header **stops drawing**, exactly like the mouse button
  (`OnToolbarToolSelected(None)`). It must never quit the app: quitting left the OBS
  remote's drawing buttons grey until a restart (CHANGELOG). Quitting is the tray icon's
  "Quit" only. If "SmoothAnnotate isn't running" anyway, check that it was quit from the
  tray, then check Smart App Control (Install / update).
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
│   ├── KeyboardHookService.cs # F8, F10-F12, Esc, Ctrl+0-8, Ctrl+V, Ctrl+Alt combos
│   ├── LaserService.cs        # Laser fade-out timer (single-stroke approach)
│   ├── StopwatchService.cs    # Timer with double-tap reset
│   ├── ConfettiService.cs     # Particle physics confetti
│   ├── OverlayService.cs      # Win32 click-through toggling, z-order
│   └── SettingsService.cs     # JSON persistence
└── Views/
    ├── OverlayWindow.xaml(.cs)  # Fullscreen transparent overlay (InkCanvas + ShapeCanvas + ConfettiCanvas)
    ├── PenInkCanvas.cs          # InkCanvas that ignores touches while the pen is near (IgnoreTouchNearPen)
    ├── ToolbarWindow.xaml(.cs)  # Horizontal dark toolbar (draggable, collapsible to dot)
    └── ToastWindow.xaml(.cs)    # Mode indicator popup
```

### Key Technical Patterns
- **Click-through overlay:** `WS_EX_TRANSPARENT` toggled via Win32 `SetWindowLong`. The window's background is always `Transparent`. While drawing, input is caught by `_hitLayer`, an alpha-1 (`#01000000`) fill at the bottom of the overlay's Grid. A layered window takes input only where its pixels aren't fully transparent.
- **Toolbar clickable in draw mode — two mechanisms, both needed:**
  - **Holes in the hit layer** under the toolbar and the OBS remote (`UpdateHitLayer`). A tap there falls through the overlay to the window below. **This is what makes finger taps work:** a finger doesn't hover, so the timer below never sees it coming — without the holes a tap on the toolbar lands on the canvas.
  - **50ms `DispatcherTimer`** checks the cursor position via `GetCursorPos` and temporarily sets the overlay click-through while hovering over the toolbar or remote. DPI-aware using `PresentationSource.TransformToDevice`. It also re-cuts the remote's hole when the remote moves; the toolbar's hole follows its `LocationChanged` / `SizeChanged` / `IsVisibleChanged`.
- **Toolbar in mouse mode:** hidden (`HideToolbarWhenIdle`, the default). Only with that setting off does it collapse to a 42px floating dot; a click on the dot re-expands it and returns to Pen mode.
- **Single-monitor overlay:** `MonitorFromPoint` + `GetMonitorInfo` constrains overlay to cursor's monitor when entering draw mode.
- **WS_EX_NOACTIVATE** on overlay so toolbar keeps focus.
- **Arrow pairing:** `_arrowPairs` dictionary maps Line↔Polygon so Select tool moves both together.
- **Delegate pinning:** Hook delegates stored as class fields to prevent GC collection.

### Settings
Stored at `%APPDATA%\SmoothAnnotate\settings.json`. `HideToolbarWhenIdle` (default true) — see Mouse/Pointer above. `IgnoreTouchNearPen` (default true)
and `PassThroughWindowTitle` — see "Pen and touch" below.

### Debug Log
Written to `%LOCALAPPDATA%\SmoothAnnotate\debug.log`. It is cleared on start. A touch that
palm rejection threw away logs `touch ignored, pen near (in range: …, last seen … ms ago)`,
so "my finger doesn't draw" is answered there first.

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
- **`laser toggle` = F11**: laser on, or from the laser back to the mouse (`ToggleLaser`).
- Commands run on the UI thread through `ControlPipeServer.OnUi`, which gives up after
  1.5 s and replies `busy`. The reply is always written, with its own timer.
  ⚠️ **Never one timer across read + handle + write:** a slow UI moment cancels the write
  and the caller gets an EMPTY reply.
- ⚠️ **`ControlPipeServer.Send` runs on the thread pool** (`Task.Run`). Waiting on the async
  pipe calls straight from the second launch's `OnStartup` (the UI thread) deadlocks: their
  continuations queue for that same waiting thread, so the `--toggle` copy delivers its
  command and then hangs forever.
- ⚠️ **The server never calls `WaitForPipeDrain()`.** It blocks a thread with no timeout,
  so a caller that never reads holds an instance for good. After replying, it waits at
  most 2 s for the caller to hang up (`WaitForHangUpAsync`).
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
- **A finger draws, except while the pen is near** (`IgnoreTouchNearPen`, default true,
  `Views/PenInkCanvas.cs`). Omri's choice: he draws with his hand too, so don't go back to
  "only the pen draws" (CHANGELOG). `false` = a finger always draws.
  - **"Near"** = the pen is in range over the overlay, or was less than 1 s ago (a palm lifts
    a moment after the pen). The pen is watched on the whole *window*, with
    `handledEventsToo`: in-range / in-air-move / down / move = here; out-of-range /
    `StylusLeave` = gone. It isn't watched on the canvas alone, because a shape tool puts
    ShapeCanvas on top and the pen's events then never reach the canvas. `StylusLeave` counts
    as gone because over the toolbar the pen's out-of-range event goes to the toolbar.
  - **Each touch is judged once, at touch-down, and keeps that verdict until lift-off.** A
    palm stays ignored even if the pen leaves mid-contact, and a finger stroke never breaks
    off halfway.
  - **Two places make that call and must agree:**
    - The routed stylus events: a touch is marked handled, so no stroke is collected.
    - The `DynamicRenderer`: it draws live ink on WPF's pen thread BEFORE those events.
      Without its filter a palm would leave a trail that vanishes on lift-off. The pen
      thread can't query tablets, so the touch digitizers' ids are collected on the UI thread
      at load. `PenIsNear` reads only plain fields, so the pen thread can ask it too.
  - Shapes, text and select ignore a mouse-down that came from a touch while the pen is
    near (`IsFromTouch`). Only the *down* is filtered: moves and ups act only on a shape or
    drag that a down started, and filtering an up could leave the mouse captured.
  - Press-and-hold (right-click ring), flicks (a quick stroke would become "back") and tap
    feedback circles are switched off on the overlay (`Stylus.Set…Enabled`).
- **Getting out of drawing on the laptop:** Esc, the toolbar's arrow button or the remote.
  Finger taps work on all of them thanks to the hit-layer holes (Key Technical Patterns).
  F8/F11 need Fn there (see the last bullet).
- **The remote stays clickable while drawing** (`PassThroughWindowTitle`, default
  "מרכז השליטה של OBS"). The full-screen drawing layer would otherwise cover it, including
  its "stop drawing" button. The hit layer has a hole over that window (`PassThroughRect`,
  re-cut by the 50 ms timer when the remote moves). The same timer also lets mouse clicks
  through over it (`FindWindow`, looked up once a second), and entering draw mode raises it
  above the layer. Verified with a real mouse click on the home PC (2026-09-27): ✏️ on the
  remote switched drawing off through the layer. How: OBS-dashboard CLAUDE.md → "How the row
  was tested".
- **Ring size:** the laptop's `settings.json` has `HighlightRingSize: 60`. At 200%
  scaling a ring is 2× its size in pixels, and OBS shrinks that screen to 0.6× (1800 →
  1080), so 60 comes out at ~72 px in the video, matching the home PC's 70. This is a
  per-machine value, never a code default.
- HP laptops ship with the top row as media keys (F1–F12 need Fn), so on the laptop F8 is
  most likely **Fn+F8**. Not verified on this one: its BIOS "action keys" setting needs admin
  to read (2026-09-27). Either way the laptop is meant to be driven from the remote, the
  Start menu and the pen.

### Testing touch without hands (`tools/touch-test.ps1`)
`powershell -ExecutionPolicy Bypass -File tools\touch-test.ps1 [-Steps finger,palm,laser,esc,toolbar]`
drives the **running** SmoothAnnotate. It uses the control pipe, simulated finger strokes
(`InjectTouchInput`), a simulated pen (`CreateSyntheticPointerDevice`) and simulated keys. It
checks the screen for red ink and the log for `touch ignored`, then prints PASS/FAIL per
check. It takes over the screen for ~30 s and presses Esc, so **tell Omri before running it**.
The script works around each quirk below:
- **WPF sees neither the injected-touch device nor the synthetic pen until its device list
  refreshes**, a few seconds after the synthetic pen is created. Until then every injected
  touch is dropped silently: no ink and no log line, which looks exactly like a bug in the
  app. The warm-up keeps stroking until one draws.
- **Ink over existing ink can't be measured** by counting red pixels, so the canvas is
  cleared before every measured stroke.
- **Long, fast, diagonal injected strokes were sometimes lost** before reaching WPF. It was
  never reproducible with short horizontal ones, and the mouse drew everywhere, so this is
  the injection, not the app. All strokes in the script are short and horizontal.
- **A palm with no ink isn't proof on its own**: the injected touch can also be dropped
  upstream. The script says which happened ("the app ignored it" = a new `touch ignored`
  line).
- The OBS remote is excluded from screen capture: screenshots never show it, but it is
  there and it is a hole in the drawing layer. The script keeps strokes clear of it.
- PowerShell traps hit on the way: `$null` passed to a `string` P/Invoke parameter arrives
  as `""` (use `[NullString]::Value`). `$r` and `$R` are the same variable. An exception on
  a background .NET thread kills the whole script, results included.

---

## Build & Run

```bash
# Requires the .NET 8 SDK: on the laptop, 8.0.425 in C:\Program Files\dotnet (winget,
# 2026-09-27). A shell opened before that install lacks it on PATH: open a new one.
# The home PC has no SDK (see "Install / update").

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
3. **Home PC:** `scp -r publish/SmoothTools omrii@100.111.186.101:E:/apps/SmoothTools-incoming-<unique>`, then
   over SSH `powershell -ExecutionPolicy Bypass -File E:\apps\SmoothTools-incoming-<unique>\install.ps1 -Target E:\apps\SmoothTools`,
   then delete that folder.
   ⚠️ **Give the upload folder a name of your own** (a timestamp works) **and check it doesn't
   exist first** — two sessions may be told "update the home PC" at once. `scp -r` into an
   existing folder nests the copy (`…-incoming\SmoothTools\…`) instead of failing, and the
   other session's cleanup can delete it half-written. An `install.ps1` run from a partial
   copy stops both apps first, then copies broken or missing files over the good install.
   🚨 **Never put the upload folder's name in an inline SSH command as a variable** (e.g.
   `"… Remove-Item -Recurse -Force E:\apps\\$IN"` from bash). On 2026-09-27 the backslashes
   were eaten on the way, PowerShell received `E:\apps$IN`, `$IN` was an empty PowerShell
   variable, and the cleanup ran as **`Remove-Item -Recurse -Force E:\apps`**. Only the two
   running exes survived (locked); `start.vbs` and `install.ps1` were deleted. So:
   - Write the name literally, use forward slashes (`E:/apps/…`, which PowerShell accepts
     and nothing mangles), and pass it to a script **file** that refuses anything not
     matching `^SmoothTools-incoming-[A-Za-z0-9-]+$` before it installs or deletes.
   - Delete with `-LiteralPath` only.
   ⚠️ **A large `scp` to the home PC can stop partway and still exit 0.** Twice on
   2026-09-27 it stopped at ~52 MB of a 72 MB exe, leaving a truncated exe beside a complete
   `install.ps1`. What worked: 8 MB chunks, each `scp`'d with a timeout and a retry (the 21
   chunks took ~40 s). Then reassemble on the home PC and check every file's SHA-256 against
   a manifest before installing, and let the install refuse any folder not marked verified.
   Those helper scripts live outside the repo so far. If you deploy to the home PC more than
   once, add them to `deploy/`.

What `deploy\install.ps1` does (safe to re-run; that is how you update):
- Stops both apps, waits for them to exit, and copies the new files in. Windows can hold an
  exe's file lock for a moment after the process ends, so the copy retries.
- Registers the logon scheduled task **"SmoothTools"**, which runs `start.vbs` from the install
  folder. `start.vbs` launches both apps with `--autostart`, and one that is already running
  exits quietly. The task starts them in the logged-on desktop. That is why it also works
  over SSH, where a process launched directly would run in invisible session 0.
  ⚠️ Never make `start.vbs` ask WMI which apps are running: a process stopped a moment
  earlier stays listed while anything still holds a handle to it (install.ps1's own
  PowerShell does), so an app gets skipped after an update.
- Writes two **Start-menu** entries, `Start Menu\Programs\SmoothTools\`: **"Cursor ring -
  SmoothZoom"** and **"Draw - SmoothAnnotate"**. Both run the exe with `--toggle`, so a tap
  switches the ring or drawing on and off. They can be pinned to the taskbar or picked for
  the pen's top button. The names are English because WScript.Shell reads Hebrew-named
  `.lnk` files as empty.
- 🚨 **The laptop has Smart App Control ON** (last read 2026-09-27 15:49: `Get-MpComputerStatus` →
  `SmartAppControlState: On`; the home PC is Off. Omri was given the steps to turn it off
  himself, since it's a security setting; re-read it before relying on this line). SAC blocks
  unsigned programs it has no good cloud verdict for. **Every new build is judged again, and
  the verdict varies** — a build can be blocked and the next one let through (CHANGELOG). So
  after any update, check that both apps are running on the laptop. A block is CodeIntegrity
  event 3077, "did not meet the Enterprise signing level requirements". Launched by `start.vbs`, the block shows up as a
  "Windows Script Host" error box. The check: `Get-WinEvent -LogName
  'Microsoft-Windows-CodeIntegrity/Operational'`, event 3077 naming the exe. Never try to
  get around SAC. The only ways forward are Omri turning it off, or a real code-signing
  certificate.
- ⚠️ **A NEW build can start late, once — which is NOT a block.** On its first run an unknown
  unsigned exe is held for a cloud check (Defender cloud-protection events 2010 at that
  moment): it starts 20 s–2 min late, and the install prints only the other app (e.g.
  `running: SmoothZoom`). Tell the two apart before waiting: a **block** is a CodeIntegrity
  3077 naming the exe (above), and waiting never ends it. No 3077 = wait and check again.
  During the hold the file also reads as "in use", so an install run straight after another
  can fail its copy; re-run it.
- Makes the task the **only** autostart. It seeds `%APPDATA%\SmoothZoom\settings.json` with
  `StartWithWindows: false` (only if there is no settings file yet) and removes SmoothZoom's
  HKCU `Run` value. A second instance would pop an "already running" box.
- Ends by printing `running: SmoothAnnotate, SmoothZoom`. After that,
  `%LOCALAPPDATA%\SmoothZoom\obs.log` should say `connected to OBS` if OBS is open.
