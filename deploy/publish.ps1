# Builds both apps as self-contained single-file exes into publish\SmoothTools\,
# together with start.vbs and install.ps1 — a folder that installs on any
# Windows machine without .NET. Needs the .NET 8 SDK (laptop only).
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo 'publish\SmoothTools'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }

foreach ($app in 'SmoothZoom', 'SmoothAnnotate') {
    dotnet publish "$repo\src\$app\$app.csproj" -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none `
        -o "$out\$app" -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "publish failed: $app" }
}
Copy-Item "$PSScriptRoot\start.vbs", "$PSScriptRoot\install.ps1" $out
Write-Host "Ready: $out  (then: & '$out\install.ps1' -Target <dir>)"
