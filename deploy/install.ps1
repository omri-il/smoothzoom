# Installs the SmoothTools folder this script sits in (built by publish.ps1) to -Target,
# registers the logon task "SmoothTools" and starts SmoothZoom. Safe to re-run for an
# update: it stops the running app before copying. Run it on the machine itself; over
# SSH is fine, because the task starts the app in the logged-on desktop (a process
# started straight from SSH would be invisible).
#
# Also safe to run from the install folder itself (-Target = where it sits): then it
# copies nothing and leaves SmoothZoom running. That is how the 2026-09-28 cleanup went
# in without a new SmoothZoom.exe.
#
# SmoothAnnotate is no longer here: it became SmoothDraw, with its own repo, folder,
# logon task and Start-menu entry. This script only removes what it left behind.
param([Parameter(Mandatory)][string]$Target)
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $Target | Out-Null
$src = (Resolve-Path -LiteralPath $PSScriptRoot).Path.TrimEnd('\')
$dst = (Resolve-Path -LiteralPath $Target).Path.TrimEnd('\')
if ($src -ne $dst) {
    $old = @(Get-Process SmoothZoom -ErrorAction SilentlyContinue)
    $old | Stop-Process -Force
    $old | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
    # Windows can hold an exe's file lock for a moment after its process has exited
    for ($i = 1; ; $i++) {
        try { Copy-Item -LiteralPath "$src\SmoothZoom" -Destination $dst -Recurse -Force; break }
        catch { if ($i -ge 10) { throw } ; Start-Sleep -Seconds 1 }
    }
    Copy-Item -LiteralPath "$src\start.vbs", "$src\install.ps1" -Destination $dst -Force
}

# What SmoothAnnotate left in this install: its folder and its Start-menu entry. Only a
# folder that really is it (named so, with its exe inside) - never anything wider.
$leftover = Join-Path $dst 'SmoothAnnotate'
if ((Split-Path -Leaf $leftover) -eq 'SmoothAnnotate' -and
    (Test-Path -LiteralPath (Join-Path $leftover 'SmoothAnnotate.exe'))) {
    Get-Process SmoothAnnotate -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    try { Remove-Item -LiteralPath $leftover -Recurse -Force; "removed $leftover" }
    catch { Write-Warning "Could not remove $leftover yet ($($_.Exception.Message)); re-run to finish" }
}
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'SmoothTools'
$oldLnk = Join-Path $menu 'Draw - SmoothAnnotate.lnk'
if (Test-Path -LiteralPath $oldLnk) { Remove-Item -LiteralPath $oldLnk -Force; "removed $oldLnk" }

# SmoothZoom's own "Start with Windows" (HKCU Run) would start it twice - the
# task is the only autostart. Seed settings only when there are none yet.
$cfg = "$env:APPDATA\SmoothZoom\settings.json"
if (-not (Test-Path $cfg)) {
    New-Item -ItemType Directory -Force (Split-Path $cfg) | Out-Null
    '{ "Version": 3, "StartWithWindows": false }' | Set-Content $cfg -Encoding ASCII
}
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ((Get-ItemProperty $run).PSObject.Properties.Name -contains 'SmoothZoom') { Remove-ItemProperty $run -Name SmoothZoom }

# $env:USERDOMAIN is WORKGROUP over SSH, and a task for WORKGROUP\user never runs
$user = "$env:COMPUTERNAME\$env:USERNAME"
$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument "`"$dst\start.vbs`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
try {
    Register-ScheduledTask -TaskName 'SmoothTools' -Action $action -Trigger $trigger -Principal $principal `
        -Settings $settings -Description 'SmoothZoom cursor ring + screen zoom (smoothzoom repo)' -Force | Out-Null
} catch {
    # A task registered over SSH can't be overwritten from a plain desktop shell ("Access is
    # denied", home PC 2026-09-28) - and stopping here left the app stopped. The existing
    # task does the same job when it runs this same start.vbs.
    $have = Get-ScheduledTask -TaskName 'SmoothTools' -ErrorAction SilentlyContinue
    if (-not $have -or $have.Actions[0].Arguments -ne "`"$dst\start.vbs`"") { throw }
    Write-Warning "Kept the existing SmoothTools task (could not re-register it: $($_.Exception.Message))"
}

# Start menu (and so pinnable to the taskbar, and pickable for the pen's top button):
# the entry launches the exe with --toggle, which switches the RUNNING copy's ring on or
# off. An English name: WScript.Shell cannot read back a Hebrew-named .lnk.
New-Item -ItemType Directory -Force $menu | Out-Null
$exe = "$dst\SmoothZoom\SmoothZoom.exe"
$lnk = (New-Object -ComObject WScript.Shell).CreateShortcut("$menu\Cursor ring - SmoothZoom.lnk")
$lnk.TargetPath = $exe
$lnk.Arguments = '--toggle'
$lnk.WorkingDirectory = Split-Path $exe
$lnk.IconLocation = "$exe,0"
$lnk.Description = 'Cursor ring on/off'
$lnk.Save()

Start-ScheduledTask -TaskName 'SmoothTools'
Start-Sleep -Seconds 10   # first start of a compressed single-file exe is slow
$running = @(Get-Process SmoothZoom -ErrorAction SilentlyContinue).Name
"running: " + ($running -join ', ')
