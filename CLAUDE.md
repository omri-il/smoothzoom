# SmoothZoom

WPF desktop tool for video tutorial recording: smooth screen zoom and a cursor ring.
Why things are the way they are (dated incidents and decisions): `CHANGELOG.md`, not auto-loaded.

➡️ **The drawing app is no longer here.** SmoothAnnotate became **SmoothDraw** on
2026-09-28: its own repo (`github.com/omri-il/SmoothDraw`, `C:\Users\omrii\Projects\SmoothDraw`),
with its history, and its own install, logon task and Start-menu entry. Anything about
drawing, the toolbar, the dot, undo, touch and pen, or `touch-test.ps1` → that repo's
CLAUDE.md. What is left of it here is only the cleanup in `deploy/install.ps1` (below).

## Repo layout
```
src/SmoothZoom/        the ring + screen zoom app (below)
src/Shared/ControlPipe.cs   the control pipe ("Control from other programs") — the SAME file
                       is in the SmoothDraw repo, compiled into SmoothDraw: change both
deploy/                publish.ps1 · install.ps1 · start.vbs · make_icons.py ("Install / update")
assets/                smoothzoom.ico (drawn by deploy/make_icons.py)
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
| Ctrl+Alt+H | Toggle cursor highlight ring (the only way it comes on — no auto-on since 2026-09-28) |
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
- `AutoRingWhileRecording`: **false** (settings v3, below)
- **Ring size on the laptop** (HP OmniBook Ultra Flip 14): its `settings.json` has
  `HighlightRingSize: 60`. At 200% scaling a ring is 2× its size in pixels, and OBS shrinks
  that screen to 0.6× (1800 → 1080), so 60 comes out at ~72 px in the video, matching the
  home PC's 70. This is a per-machine value, never a code default. It really took effect
  only on 2026-09-29 18:48 (read back from the real file then): before, the 60 lived only
  in Claude's hidden copy (next bullet) and the laptop ran on defaults, ring 70 and auto-on.
- 🚨 **On the laptop, don't write or read the app's AppData files straight from a Claude
  desktop session.** Programs the session starts run inside Claude's MSIX package, and a
  file they CREATE under `%APPDATA%` / `%LOCALAPPDATA%` lands in
  `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\…` instead; the session then
  reads that copy in place of the real file. The installed app (started by Task Scheduler,
  outside the package) never sees it. Seen 2026-09-29: `LocalCache\Roaming\SmoothZoom\settings.json`
  (v2, ring 60, written 2026-09-27) while the real `%APPDATA%\SmoothZoom` had no file at all.
  Do both through a one-off scheduled task (Install / update → "From a Claude desktop session").

### Cursor ring, click ripple, auto-on while recording
- The ring is an ordinary topmost click-through window, so OBS **Display Capture records it**
  and OBS's F9 zoom-to-mouse (a crop filter) enlarges it with everything else.
- **Click ripple:** left click = ring colour, right click = red; grows to 2× and fades in
  350 ms, drawn inside the ring window (which is sized for it). Only while the ring is on.
  Injected clicks (`LLMHF_INJECTED`, e.g. the magnifier's click translation) are skipped so
  one click never ripples twice.
- ⚠️ **Auto-on is OFF** (`AutoRingWhileRecording: false`, since 2026-09-28). Omri: the ring
  "keeps opening by itself". He had switched it off at 09:14 and the next recording at 09:15
  switched it back on. He chose "only when I turn it on": Ctrl+Alt+H, the tray icon or the
  Start-menu entry. Settings **v3** turns it off once, on the first start of this build (the
  file is written back as v3), so setting `true` again by hand sticks. With it off the
  watcher is never created: **SmoothZoom does not talk to OBS at all**, and `obs.log` gets no
  new lines. What follows is how it works when switched back on.
- **Auto-on (when enabled):** `ObsRecordingWatcher` connects to `ws://127.0.0.1:<port>`, reading port and
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

## Control from other programs (Start menu, pen button)
⚠️ **The OBS dashboard no longer controls this app** (2026-09-28, Omri: "I want to have a
separation between the tools"). Its remote's ring / draw / laser / clear row and its
`smooth.py` were removed; the hotkeys, the tray and the Start menu replace them.

SmoothZoom is controlled from outside through a **named pipe** (`src/Shared/ControlPipe.cs`,
linked into the csproj; the same file is in the SmoothDraw repo for its pipe). One text
command goes in and one JSON line comes back, and every reply carries the app's current
state. Only the same Windows user can connect (`PipeOptions.CurrentUserOnly`). There are 4
instances, so a few callers at once never see "pipe busy".

| Pipe | Commands | Reply |
|---|---|---|
| `\\.\pipe\SmoothZoom.control` | `status` · `ring toggle` · `ring on` · `ring off` | `{ok, ring, auto, obs, recording}` (`auto` = the recording watcher turned it on) |

(SmoothDraw's pipe, `SmoothDraw.control` — until 2026-09-28 `SmoothAnnotate.control` — is
documented in its repo.)
- **`ring …` from any caller counts as "by hand"**, exactly like Ctrl+Alt+H. It takes over
  from auto-on, so a recording's end leaves the ring alone (`SetRingByHand`).
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
  took up to ~1 s, so a caller should wait 1.5 s (the removed `smooth.TIMEOUT` did).
- **Command-line flags:**
  - `--toggle`: if the app is already running, send it `ring toggle` over the pipe and
    exit quietly. Otherwise start and switch on. This is what the Start-menu entry and
    the pen's top button run.
  - `--autostart`: already running = exit quietly (`start.vbs`).
  - A plain second launch still shows the "already running" box.

---

## Build & Run

```bash
# Requires the .NET 8 SDK, 8.0.425 on both machines:
# - laptop: C:\Program Files\dotnet (winget, 2026-09-27). A shell opened before that
#   install lacks it on PATH: open a new one.
# - home PC: portable, E:\tools\dotnet (dotnet-install.ps1 -Version 8.0.425 -NoPath,
#   2026-09-28; no admin, nothing on the full C:). Not on PATH — see "On the home PC" below.

dotnet build src/SmoothZoom/SmoothZoom.csproj
start src/SmoothZoom/bin/Debug/net8.0-windows/SmoothZoom.exe
```

### On the home PC (E:\tools\dotnet)
⚠️ **A plain `dotnet restore` fails there**: `Unable to find fallback package folder
'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'`. A leftover
machine-wide `C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.FallbackLocation.config`
names that missing folder. The env var `RestoreFallbackFolders=clear` did NOT help. What
works: a nuget.config OUTSIDE the repo — **`E:\tools\nuget-clean.config`** (since
2026-09-28: packageSources = nuget.org only, `<fallbackPackageFolders><clear /></fallbackPackageFolders>`,
and `globalPackagesFolder` = `E:\tools\nuget-packages`, so packages stay off the full C:) —
passed as `restore --configfile`, then `build` / `publish --no-restore`. Keep the CLI's own
state on E: as well: `DOTNET_CLI_HOME=E:\tools\dotnet-home`.
- **Release, one command** (since 2026-09-28 `publish.ps1` takes the SDK and the config,
  and does the restore itself):
  `$env:DOTNET_CLI_HOME='E:\tools\dotnet-home'; powershell -File deploy\publish.ps1 -Dotnet E:\tools\dotnet\dotnet.exe -NuGetConfig E:\tools\nuget-clean.config`
  (~16 s). Before that, its steps were done by hand (restore + `publish --no-restore`,
  then `start.vbs` + `install.ps1` copied beside them).

## Key Technical Details
- **Thread affinity:** All Magnification API calls must stay on UI thread (DispatcherTimer at 16ms)
- **Crash recovery:** SmoothZoom resets zoom on startup + on unhandled exceptions
- ⚠️ **DPI awareness: System-aware, NOT PerMonitorV2** (read 2026-09-28 on the home PC: its
  windows report awareness 1 at 96 DPI). The csproj's `ApplicationHighDpiMode PerMonitorV2`,
  which this line used to credit, only feeds WinForms' startup code; a WPF app takes its
  DPI mode from the manifest, and `app.manifest` declares none. On the home PC the main
  screen is 100 %, so Win32 coordinates there are its real pixels; the 150 % left screen
  is scaled by Windows.
- **Easing:** Cubic ease-in-out for zoom animation
- **Cursor tracking:** Lerp with adaptive snapping (eliminates sub-pixel jitter when still)
- **No hotkey conflicts:** SmoothZoom uses Ctrl+Alt, SmoothDraw uses F-keys + Ctrl+number (different patterns).
  ⚠️ **F9 belongs to OBS** (zoom-to-mouse, OBS-dashboard repo) — never bind it here.
  SmoothDraw's hook passes F8 on (and every key while you are not drawing), so a shared key
  fires both apps at once. SmoothDraw's undo is Ctrl+Z **without** Alt, so Ctrl+Alt+Z stays
  this app's.

## Install / update (laptop + home PC)
Both machines run the same self-contained build — no .NET needed on the target. Both can
build since 2026-09-28 (the home PC's SDK is portable, on E:, "On the home PC" above), so each
can build and install its own copy in place; the `scp` route below is for pushing a
laptop build to the home PC.

| Machine | Installed at |
|---|---|
| Laptop | `%LOCALAPPDATA%\Programs\SmoothTools` |
| Home PC (where Omri records) | `E:\apps\SmoothTools` |

(The folder keeps its old name, SmoothTools, from when both apps lived in it.)

1. **Build**, from the repo root: `powershell -File deploy\publish.ps1` (the home PC: the
   one command in "On the home PC"). It writes `publish\SmoothTools\` (gitignored):
   `SmoothZoom\`, `start.vbs`, `install.ps1`. The exe icon is `assets/smoothzoom.ico`, drawn
   by `deploy/make_icons.py` (Pillow, run once, committed; re-running it gives the same
   bytes): a yellow ring on a dark tile. The tray icon is the same icon, read back from the exe.
2. **Laptop:** `& publish\SmoothTools\install.ps1 -Target "$env:LOCALAPPDATA\Programs\SmoothTools"`
   - **From a Claude desktop session**, run it through a one-off scheduled task, so what it
     creates under AppData is real — its settings seed is the kind of file that went astray
     before (Settings → the 🚨 bullet; the Start menu was not redirected, SmoothDraw CLAUDE.md).
     How it was done 2026-09-29: a wrapper `.ps1` in `publish\` (gitignored) that runs
     `install.ps1` inside `Start-Transcript` and writes a `.done` file at the end; register
     it as an Interactive, `RunLevel Limited` task for `$env:COMPUTERNAME\$env:USERNAME`,
     start it, wait for the `.done` file, read the transcript, unregister the task. A
     one-off read or settings edit goes the same way. ⚠️ Output piped to `Set-Content` is
     held open until the script ends: wait for the end before reading it.
   - **Installed on the laptop 2026-09-29 18:45** (build of 1a35425, after SmoothDraw's
     install): `running: SmoothZoom`, SmoothAnnotate's folder and "Draw - SmoothAnnotate"
     removed, the pipe's `status` said ring off. The real `settings.json` was seeded as v3
     then (none existed), and got the laptop's `HighlightRingSize: 60` at 18:48.
3. **Home PC:** `scp -r publish/SmoothTools omrii@100.111.186.101:E:/apps/SmoothTools-incoming-<unique>`, then
   over SSH `powershell -ExecutionPolicy Bypass -File E:\apps\SmoothTools-incoming-<unique>\install.ps1 -Target E:\apps\SmoothTools`,
   then delete that folder.
   ⚠️ **Give the upload folder a name of your own** (a timestamp works) **and check it doesn't
   exist first** — two sessions may be told "update the home PC" at once. `scp -r` into an
   existing folder nests the copy (`…-incoming\SmoothTools\…`) instead of failing, and the
   other session's cleanup can delete it half-written. An `install.ps1` run from a partial
   copy stops the app first, then copies broken or missing files over the good install.
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
- Stops SmoothZoom, waits for it to exit, and copies the new files in. Windows can hold an
  exe's file lock for a moment after the process ends, so the copy retries.
  **Run from the install folder itself** (`-Target` = where the script sits) it copies
  nothing and leaves SmoothZoom running — how the 2026-09-28 cleanup went in without a new
  `SmoothZoom.exe` (a new build would get a new Smart App Control verdict on the laptop).
- **Removes what SmoothAnnotate left** (since 2026-09-28): the install's `SmoothAnnotate\`
  folder — only a folder of that name with `SmoothAnnotate.exe` inside, deleted with
  `-LiteralPath`; a locked one warns and is finished by a re-run — and the Start-menu entry
  `SmoothTools\Draw - SmoothAnnotate.lnk`. It no longer starts, stops or copies
  SmoothAnnotate. ⚠️ **On a machine that still has SmoothAnnotate, install SmoothDraw
  first** (its CLAUDE.md → Install / update) and check it runs; only then this.
- Registers the logon scheduled task **"SmoothTools"**, which runs `start.vbs` from the install
  folder. `start.vbs` launches SmoothZoom with `--autostart` (only if the exe is there: a
  missing one would pop a "file not found" box, and the task stays blocked while it is
  open), and one that is already running exits quietly. The task starts it in the logged-on
  desktop. That is why it also works over SSH, where a process launched directly would run
  in invisible session 0.
  ⚠️ Run from a plain desktop shell on the home PC (2026-09-28), re-registering that task
  failed with **"Access is denied"**. It had been registered over SSH. Until then the
  script stopped right there, with the apps already stopped and the new files copied. Now
  it keeps the existing task when that task runs the same `start.vbs`, warns, and goes on.
  If the app is ever found stopped after an install, `Start-ScheduledTask SmoothTools`.
  ⚠️ Never make `start.vbs` ask WMI which apps are running: a process stopped a moment
  earlier stays listed while anything still holds a handle to it (install.ps1's own
  PowerShell does), so an app gets skipped after an update.
  ⚠️ The task runs at Task Scheduler's default priority, so SmoothZoom (started through
  `wscript`) runs **BelowNormal** (seen 2026-09-28). SmoothDraw's task asks for normal.
- Writes the **Start-menu** entry `Start Menu\Programs\SmoothTools\Cursor ring - SmoothZoom`.
  It runs the exe with `--toggle`, so a tap switches the ring on and off. It can be pinned
  to the taskbar or picked for the pen's top button. The name is English because
  WScript.Shell reads Hebrew-named `.lnk` files as empty.
- 🚨 **The laptop has Smart App Control ON** (last read 2026-09-29 ~18:40: `Get-MpComputerStatus` →
  `SmartAppControlState: On`; the home PC is Off. Omri was given the steps to turn it off
  himself, since it's a security setting; re-read it before relying on this line). SAC blocks
  unsigned programs it has no good cloud verdict for. **Every new build is judged again, and
  the verdict varies** — a build can be blocked and the next one let through (CHANGELOG). So
  after any update, check that the app is running on the laptop. Last seen: the build of
  1a35425 was let through, running within 10 s and no 3077 (2026-09-29 18:45). A block is CodeIntegrity
  event 3077, "did not meet the Enterprise signing level requirements". Launched by `start.vbs`, the block shows up as a
  "Windows Script Host" error box. The check: `Get-WinEvent -LogName
  'Microsoft-Windows-CodeIntegrity/Operational'`, event 3077 naming the exe. Never try to
  get around SAC. The only ways forward are Omri turning it off, or a real code-signing
  certificate.
- ⚠️ **A NEW build can start late, once — which is NOT a block.** On its first run an unknown
  unsigned exe is held for a cloud check (Defender cloud-protection events 2010 at that
  moment): it starts 20 s–2 min late, and the install prints no app on its `running:` line.
  Tell the two apart before waiting: a **block** is a CodeIntegrity
  3077 naming the exe (above), and waiting never ends it. No 3077 = wait and check again.
  During the hold the file also reads as "in use", so an install run straight after another
  can fail its copy; re-run it.
- Makes the task the **only** autostart. It seeds `%APPDATA%\SmoothZoom\settings.json` with
  `StartWithWindows: false` (only if there is no settings file yet) and removes SmoothZoom's
  HKCU `Run` value. A second instance would pop an "already running" box.
- Ends by printing `running: SmoothZoom`. (`obs.log` says `connected to OBS` only if
  `AutoRingWhileRecording` was switched back on.)
