# SmoothZoom — history

**Not auto-loaded.** Split out of `CLAUDE.md` on 2026-09-27 (`/compress-docs`). `CLAUDE.md`
keeps the rules; this file keeps why they exist — the dated incidents and decisions behind
them. Newest first. Git history has the full detail. Entries before 2026-09-28 cover both
apps, from when SmoothAnnotate (now SmoothDraw, its own repo) lived here too.

## 2026-09-28/29 — the drawing app moved out: SmoothAnnotate → SmoothDraw

Omri wanted the drawing app ("Smooth Draw", as he calls it) in a folder of its own, fully
apart from the ring — the same "separation between the tools" as the day before. It became
**SmoothDraw**: repo `omri-il/SmoothDraw` (`C:\Users\omrii\Projects\SmoothDraw`), made from a
fresh clone of this repo with `git filter-repo`, so its 31 commits of history came along.
It has its own install (`E:\apps\SmoothDraw`, logon task "SmoothDraw", Start-menu
`SmoothDraw.lnk`) and took over SmoothAnnotate's settings and dot place on first start.
The same session rebuilt its toolbar, added undo/redo and pen thickness (SmoothDraw's
CHANGELOG).

What changed here:
- `src/SmoothAnnotate`, its icon, its half of `make_icons.py`, its solution entry and
  `tools/touch-test.ps1` (which only tested it) are gone. `src/Shared/ControlPipe.cs` stays;
  the same file is in SmoothDraw.
- `start.vbs` starts SmoothZoom only, and only if the exe is there.
- `install.ps1` no longer starts, stops or copies SmoothAnnotate. It deletes what it left
  behind (the install's `SmoothAnnotate\` folder, guarded to a folder of that name with its
  exe inside, `-LiteralPath`; the Start-menu entry "Draw - SmoothAnnotate"). Run from the
  install folder itself it copies nothing and leaves SmoothZoom running, which is how the
  cleanup went in without rebuilding `SmoothZoom.exe` — a new build would have needed a new
  Smart App Control verdict on the laptop.
- `publish.ps1` takes `-Dotnet` / `-NuGetConfig`, so the home PC's portable SDK builds with
  one command; the clean config now lives at `E:\tools\nuget-clean.config`.

Found on the way (CLAUDE.md corrected): SmoothZoom is **System-DPI-aware**, not PerMonitorV2
as the docs said — `ApplicationHighDpiMode` only feeds WinForms' startup code. And started
through the logon task, it runs at **BelowNormal** priority (Task Scheduler's default).

The text of `CLAUDE.md` before the split: `git show e1be3aa:CLAUDE.md` (SmoothDraw's copy of
the history has it too, as `49c653b`).

## 2026-09-28 — separated from the OBS dashboard; nothing turns on by itself any more

Omri: the ring and the drawing "keep opening by itself", and he wanted "a separation
between the tools". The logs showed two causes, and neither was a bug in the pipe:

- **Drawing:** the keyboard hook acted on Ctrl+1…8, Ctrl+V, F10–F12 and the Ctrl+Alt keys
  in every program. The debug log that morning had `PEN` at 09:33:19 with no `F8 pressed`
  and no `control:` line (Ctrl+1, a browser tab switch), then `IMAGE PASTED` at 09:33:31
  (Ctrl+V with a screenshot on the clipboard, in another app). Now only F8 works
  everywhere; the rest only while drawing.
- **Ring:** auto-on while recording. The remote's click log has `ring:off` at 09:14:08,
  and `obs.log` has `recording started` at 09:15:00, which switched it straight back on.
  Omri chose "only when I turn it on": `AutoRingWhileRecording` false, applied by settings
  v3 on the first start of the new build.

He asked whether OBS could skip the toolbar, as it skips the dashboard's remote: yes, the
same `WDA_EXCLUDEFROMCAPTURE`, now on the toolbar and the mode labels. With the toolbar out
of the videos, the reason for hiding it completely was gone, so the default became the
small dot (click = pen, drag = move, place remembered). The dot replaced the remote's ✏️
button, which went with the rest of the remote's row (OBS-dashboard repo, same day).
Verified on the home PC: OBS's own frame had nothing where the dot was on screen.

The build was made on the home PC for the first time, with a portable SDK on E: (C: is
nearly full). Two machine quirks turned up, both in CLAUDE.md: a leftover Visual Studio
NuGet fallback folder that breaks every restore there, and `install.ps1` stopping at
"Access is denied" on the SSH-registered logon task, which left both apps stopped until
the task was started by hand. The script now keeps the existing task.

## 2026-09-27 (night) — a home-PC deploy that deleted E:\apps

Deploying the ✕ fix to the home PC, an inline SSH command carried the upload folder's
name as `E:\apps\\$IN`. The backslashes were eaten on the way; PowerShell saw
`E:\apps$IN` with `$IN` empty, so the install looked for `E:\apps\install.ps1` (not
found), and the cleanup ran `Remove-Item -Recurse -Force E:\apps`. Only the two running
exes survived (locked). `start.vbs` and `install.ps1` were gone, so the logon task would
have started nothing. The follow-up test ran on the OLD build, and its ✕ click quit
SmoothAnnotate, as the old ✕ did. A restart attempt then hung on a "script not found" box,
and while that `wscript` lived, every later start of the SmoothTools task was refused
(0x800710E0: one instance was still running).

Repair: the upload was re-sent as 8 MB chunks (a single `scp` twice stopped at ~52 MB),
reassembled and SHA-256-checked, and installed through a guarded script. Then the stuck
`wscript` was closed and the task started again. Result: all four files back, both apps
running, and the ✕ fix verified with a real click (the app stays, drawing turns off). The
rules are in CLAUDE.md → Install / update, step 3.

## 2026-09-27 (evening) — the toolbar ✕ stops drawing instead of quitting

The toolbar's ✕ called `Application.Current.Shutdown()`. On the laptop SmoothAnnotate
vanished twice that way (15:53 and 20:10: the log ends with MOUSE, with no crash and no
Smart App Control block), and the OBS remote's drawing buttons went grey until a restart.
Omri chose that ✕ only stops drawing, like the toolbar's mouse button. Quitting stays in
the tray menu.

## 2026-09-27 — the ring, the OBS remote, the laptop

One long day across several sessions: the tools went from "exists in the repo" to installed
on both machines and driven from the OBS dashboard.

- **Home PC deploy, and the upload-folder collision.** Two sessions were both told "update
  the home PC" and uploaded into the same `E:\apps\SmoothTools-incoming` about a minute
  apart (15:44). `scp -r` nested the second copy inside the first; the first session's
  cleanup deleted it half-written. No harm: the second `install.ps1` found nothing to run,
  and the home PC ended up on the first session's verified build of b743508. → rule: an
  upload folder of your own.
- **Finger drawing (b3b7f94, b743508).** The first touch version made "only the pen draws"
  (`IgnoreTouch`). The same day Omri chose instead that a finger draws too, except while the
  pen is near (`IgnoreTouchNearPen`, palm rejection). Also: F11 became a real on/off (from the
  laser it went to the pen before, so the screen stayed covered), Esc stops drawing, and the
  hit layer got holes under the toolbar and the remote — before that, a finger tap on the
  toolbar landed on the canvas. `tools/touch-test.ps1` added; all 7 of its checks passed on
  the laptop. Its quirks cost an afternoon (they are listed in CLAUDE.md).
- **`--toggle` hung forever (fixed in 3211443).** `ControlPipeServer.Send` waited on async
  pipe calls from the second launch's UI thread; the continuations queued behind that wait,
  so each Start-menu tap left a stuck copy after delivering its command — two stuck SmoothZoom
  copies on the laptop. Those callers never read their reply, and the server's
  `WaitForPipeDrain()` (no timeout) held a pipe instance per stuck caller. Fixed: `Send` runs
  on the pool; the server waits at most 2 s for the caller to hang up.
- **Empty pipe replies (fixed in 4ae855b).** One 2 s timer covered read + handle + write, so a
  slow UI moment cancelled the write and the caller got an empty reply. Now the reply has its
  own timer and UI work gives up after 1.5 s with `busy`.
- **Smart App Control on the laptop.** The new HP OmniBook Ultra Flip 14 has SAC ON (the home
  PC is off). One build's SmoothAnnotate was blocked at 15:00:05 (CodeIntegrity 3077 + 3118,
  "Windows Script Host: An Application Control policy has blocked this file (800711C7)" from
  `start.vbs`). The next build — only the pipe fix changed — was let through for both apps
  after ~20 s, and later builds too. The first "late start" was at first mistaken for a
  Defender scan; the 3077 event is what tells a block from a delay. Omri was given the steps
  to turn SAC off himself (a security setting); as of 15:49 it was still on.
- **Defender/cloud first-run hold.** A brand-new build of SmoothAnnotate started 20 s–2 min
  late while the install printed `running: SmoothZoom` only (Defender cloud-protection events
  2010 at that moment); no 3077, so not a block.
- **`start.vbs` and WMI (fixed in 4ae855b).** It asked WMI which apps were running; right after
  an update the just-stopped SmoothAnnotate was still listed (install.ps1's PowerShell held a
  handle to it), so it was skipped. Now both apps start with `--autostart` and exit quietly
  if already running.
- **Control pipe, Start menu, OBS remote (4ae855b).** Named pipes `SmoothZoom.control` /
  `SmoothAnnotate.control`; `--toggle` / `--autostart`; Start-menu entries "Cursor ring -
  SmoothZoom" and "Draw - SmoothAnnotate"; app icons; the OBS dashboard's remote got ring /
  draw / laser / clear buttons (OBS-dashboard cca68ff). Measured: warm replies 1–6 ms, the
  first calls after a start up to ~1 s.
- **Laptop install + one installer (2f9aba9).** `deploy/publish.ps1` + `install.ps1` +
  `start.vbs`; the laptop got the .NET 8 SDK (8.0.425, winget) because the home PC has none.
- **Cursor ring for OBS (4e3f93a).** Yellow ring with click ripple; turns on by itself while
  OBS records (`ObsRecordingWatcher`). SmoothAnnotate's draw toggle moved from F9 to F8,
  because F9 is OBS-dashboard's zoom-to-mouse and SmoothAnnotate's hook passes keys on (one
  press fired both). The toolbar hides outside draw mode (`HideToolbarWhenIdle`) — the first
  home-PC test showed it sitting on the recorded screen.

## Verbatim: lines rewritten or moved out of CLAUDE.md on 2026-09-27

Every line below is exactly as it stood in `CLAUDE.md` before the compression (commit
82ed227), kept so that no fact is lost in a rewrite. The rules they carried are still in
`CLAUDE.md`, reworded without the dates and stories.

```text
| F8 | Toggle draw mode (was F9 until 2026-09-27 — F9 is OBS zoom-to-mouse) |
| F11 | Laser pointer on/off — pressed again it goes back to the mouse (until 2026-09-27 it went to the pen, so the screen stayed covered) |
- **Mouse/Pointer** — exits draw mode. With `HideToolbarWhenIdle` (default **on** since 2026-09-27) the toolbar **hides completely** — it sits on the recorded screen, so any visible toolbar or dot ends up in every OBS video; F8 / Ctrl+1-8 bring it back. With it off: collapses to a small floating dot; click the dot to re-expand
  - **Holes in the hit layer** (since 2026-09-27) under the toolbar and the OBS remote (`UpdateHitLayer`). A tap there falls through the overlay to the window below. **This is what makes finger taps work:** a finger doesn't hover, so the timer below never sees it coming. Before this, a tap on the toolbar landed on the canvas.
  ⚠️ **Until that fix, one 2 s timer covered read + handle + write.** A slow UI moment then
  cancelled the write, and the caller got an EMPTY reply.
- ⚠️ **`ControlPipeServer.Send` runs on the thread pool** (`Task.Run`). The first version
  waited on the async pipe calls straight from the second launch's `OnStartup`, which is
  the UI thread. Their continuations queued for that same waiting thread, so every
  `--toggle` copy hung forever after delivering its command (2026-09-27: two copies of
  SmoothZoom stuck).
  and those hung callers held an instance each. After replying, it waits at most 2 s for
  the caller to hang up (`WaitForHangUpAsync`).
  `Views/PenInkCanvas.cs`). Omri chose this on 2026-09-27, the same day, over the first
  version's "only the pen draws": he wants to draw with his hand too. `false` = a finger
  always draws.
All 7 checks passed on the laptop on 2026-09-27. The quirks below cost an afternoon; the
script works around each one:
   exist first.** On 2026-09-27 two sessions, both told "update the home PC", uploaded into
   the same `SmoothTools-incoming` a minute apart. `scp -r` into an existing folder nests
   the copy (`…-incoming\SmoothTools\…`) instead of failing. The first session's cleanup
   then deleted the second one's half-written files. No harm was done, because the second
   install found nothing to copy. But an `install.ps1` run from a partial copy stops both
   apps first, and then copies broken or missing files over the good install.
  ⚠️ `start.vbs` used to ask WMI which apps were running. That failed after an update: a
  process stopped a moment earlier stays listed while anything still holds a handle to it
  (install.ps1's own PowerShell did), so SmoothAnnotate was skipped.
  himself, since it's a security setting; re-read it before relying on this line). SAC blocks unsigned programs it has no
  good cloud verdict for. **Every new build is judged again, and the verdict varies.** On
  2026-09-27 one build ran SmoothZoom and blocked SmoothAnnotate (CodeIntegrity event
  3077, "did not meet the Enterprise signing level requirements"). The next build, with
  only the pipe fix, was let through for both after ~20 s. So after any update, check that
  both apps are running on the laptop. Launched by `start.vbs`, the block shows up as a
  moment): SmoothAnnotate started 20 s–2 min late on 2026-09-27, while the install printed
  `running: SmoothZoom` only. Tell the two apart before waiting: a **block** is a CodeIntegrity
```
