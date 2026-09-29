# Builds SmoothZoom as a self-contained single-file exe into publish\SmoothTools\,
# together with start.vbs and install.ps1: a folder that installs on any Windows machine
# without .NET. (SmoothDraw, the drawing app, has its own repo and its own publish.ps1.)
#
#   Laptop (SDK on PATH):  powershell -File deploy\publish.ps1
#   Home PC (portable SDK, CLAUDE.md -> "On the home PC"):
#     powershell -File deploy\publish.ps1 -Dotnet E:\tools\dotnet\dotnet.exe -NuGetConfig E:\tools\nuget-clean.config
#
# -NuGetConfig restores with that config first, then publishes without restoring. The
# home PC needs it: a leftover machine-wide Visual Studio config breaks every plain restore.
param(
    [string]$Dotnet = 'dotnet',
    [string]$NuGetConfig
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$proj = "$repo\src\SmoothZoom\SmoothZoom.csproj"
$out = Join-Path $repo 'publish\SmoothTools'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }

# The same properties for the restore, or it would not fetch the win-x64 runtime packs
$props = @('-r', 'win-x64', '-p:SelfContained=true', '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true')
$publish = @('publish', $proj, '-c', 'Release') + $props + @('-p:DebugType=none', '-o', "$out\SmoothZoom", '-nologo', '-v', 'q')
if ($NuGetConfig) {
    & $Dotnet restore $proj @props --configfile $NuGetConfig -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'restore failed' }
    $publish += '--no-restore'
}
& $Dotnet @publish
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }
Copy-Item -LiteralPath "$PSScriptRoot\start.vbs", "$PSScriptRoot\install.ps1" -Destination $out
Write-Host "Ready: $out  (then: & '$out\install.ps1' -Target <dir>)"
