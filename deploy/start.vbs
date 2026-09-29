' Starts SmoothZoom from this folder. Run at logon by the scheduled task "SmoothTools"
' that install.ps1 registers. (Until 2026-09-28 it also started SmoothAnnotate, now
' SmoothDraw: its own repo, install and logon task.)
' --autostart: an app that is already running just exits quietly. That is the only
' "already running" check - asking WMI instead failed: a process stopped a moment ago
' is still listed while anything holds a handle to it (install.ps1's own PowerShell did),
' so an app was skipped after an update.
Set fso = CreateObject("Scripting.FileSystemObject")
dir = fso.GetParentFolderName(WScript.ScriptFullName)
Set sh = CreateObject("WScript.Shell")
exe = dir & "\SmoothZoom\SmoothZoom.exe"
' A missing exe would stop here with a "file not found" box, and the task stays blocked
' while that box is open (every later start of it refused, 2026-09-27)
If fso.FileExists(exe) Then sh.Run """" & exe & """ --autostart", 1, False
