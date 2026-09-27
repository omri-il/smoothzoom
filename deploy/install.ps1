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
    '{ "Version": 2, "StartWithWindows": false }' | Set-Content $cfg -Encoding ASCII
}
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ((Get-ItemProperty $run).PSObject.Properties.Name -contains 'SmoothZoom') { Remove-ItemProperty $run -Name SmoothZoom }

# $env:USERDOMAIN is WORKGROUP over SSH, and a task for WORKGROUP\user never runs
$user = "$env:COMPUTERNAME\$env:USERNAME"
$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument "`"$Target\start.vbs`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
Register-ScheduledTask -TaskName 'SmoothTools' -Action $action -Trigger $trigger -Principal $principal `
    -Settings $settings -Description 'SmoothZoom cursor ring + SmoothAnnotate (smoothzoom repo)' -Force | Out-Null

Start-ScheduledTask -TaskName 'SmoothTools'
Start-Sleep -Seconds 10   # first start of a compressed single-file exe is slow
$running = @(Get-Process SmoothZoom, SmoothAnnotate -ErrorAction SilentlyContinue).Name
"running: " + ($running -join ', ')
