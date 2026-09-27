' Starts SmoothZoom + SmoothAnnotate from this folder, skipping any already running
' (a second instance would pop an "already running" box). Run at logon by the
' scheduled task "SmoothTools" that install.ps1 registers.
Set fso = CreateObject("Scripting.FileSystemObject")
dir = fso.GetParentFolderName(WScript.ScriptFullName)
Set wmi = GetObject("winmgmts:")
Set sh = CreateObject("WScript.Shell")
For Each app In Array("SmoothZoom", "SmoothAnnotate")
  If wmi.ExecQuery("SELECT * FROM Win32_Process WHERE Name='" & app & ".exe'").Count = 0 Then
    sh.Run """" & dir & "\" & app & "\" & app & ".exe""", 1, False
  End If
Next
