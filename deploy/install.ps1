# Installs the SmoothTools folder this script sits in (built by publish.ps1) to
# -Target, registers the logon task "SmoothTools" and starts both apps.
# Safe to re-run for an update: stops the running apps before copying.
# Run it on the machine itself — over SSH is fine, the task starts the apps in
# the logged-on desktop (a process started straight from SSH would be invisible).
param([Parameter(Mandatory)][string]$Target)
$ErrorActionPreference = 'Stop'

$old = @(Get-Process SmoothZoom, SmoothAnnotate -ErrorAction SilentlyContinue)
$old | Stop-Process -Force
$old | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force $Target | Out-Null
$src = (Resolve-Path $PSScriptRoot).Path.TrimEnd('\')
if ($src -ne (Resolve-Path $Target).Path.TrimEnd('\')) {
    # Windows can hold an exe's file lock for a moment after its process has exited
    for ($i = 1; ; $i++) {
        try { Copy-Item "$src\SmoothZoom", "$src\SmoothAnnotate" $Target -Recurse -Force; break }
        catch { if ($i -ge 10) { throw } ; Start-Sleep -Seconds 1 }
    }
    Copy-Item "$src\start.vbs", "$src\install.ps1" $Target -Force
}

# SmoothZoom's own "Start with Windows" (HKCU Run) would start it twice — the
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
$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument "`"$Target\start.vbs`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
try {
    Register-ScheduledTask -TaskName 'SmoothTools' -Action $action -Trigger $trigger -Principal $principal `
        -Settings $settings -Description 'SmoothZoom cursor ring + SmoothAnnotate (smoothzoom repo)' -Force | Out-Null
} catch {
    # A task registered over SSH can't be overwritten from a plain desktop shell ("Access is
    # denied", home PC 2026-09-28) — and stopping here left both apps stopped. The existing
    # task does the same job when it runs this same start.vbs.
    $have = Get-ScheduledTask -TaskName 'SmoothTools' -ErrorAction SilentlyContinue
    if (-not $have -or $have.Actions[0].Arguments -ne "`"$Target\start.vbs`"") { throw }
    Write-Warning "Kept the existing SmoothTools task (could not re-register it: $($_.Exception.Message))"
}

# Start menu (and so pinnable to the taskbar, and pickable for the pen's top button):
# each entry launches the exe with --toggle, which switches the RUNNING copy's ring /
# drawing on or off. English names: WScript.Shell cannot read back a Hebrew-named .lnk.
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'SmoothTools'
New-Item -ItemType Directory -Force $menu | Out-Null
$shell = New-Object -ComObject WScript.Shell
foreach ($s in @(
        @{ Name = 'Cursor ring - SmoothZoom'; App = 'SmoothZoom'; What = 'Cursor ring on/off' },
        @{ Name = 'Draw - SmoothAnnotate'; App = 'SmoothAnnotate'; What = 'Drawing on/off' })) {
    $exe = "$Target\$($s.App)\$($s.App).exe"
    $lnk = $shell.CreateShortcut("$menu\$($s.Name).lnk")
    $lnk.TargetPath = $exe
    $lnk.Arguments = '--toggle'
    $lnk.WorkingDirectory = Split-Path $exe
    $lnk.IconLocation = "$exe,0"
    $lnk.Description = $s.What
    $lnk.Save()
}

Start-ScheduledTask -TaskName 'SmoothTools'
Start-Sleep -Seconds 10   # first start of a compressed single-file exe is slow
$running = @(Get-Process SmoothZoom, SmoothAnnotate -ErrorAction SilentlyContinue).Name
"running: " + ($running -join ', ')
