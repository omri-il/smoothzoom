' Starts SmoothZoom + SmoothAnnotate from this folder. Run at logon by the scheduled
' task "SmoothTools" that install.ps1 registers.
' --autostart: an app that is already running just exits quietly. That is the only
' "already running" check - asking WMI instead failed: a process stopped a moment ago
' is still listed while anything holds a handle to it (install.ps1's own PowerShell did),
' so SmoothAnnotate was skipped after an update.
Set fso = CreateObject("Scripting.FileSystemObject")
dir = fso.GetParentFolderName(WScript.ScriptFullName)
Set sh = CreateObject("WScript.Shell")
For Each app In Array("SmoothZoom", "SmoothAnnotate")
  sh.Run """" & dir & "\" & app & "\" & app & ".exe"" --autostart", 1, False
Next
