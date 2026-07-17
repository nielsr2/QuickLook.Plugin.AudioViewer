# Packages the Release build of the plugin into a .qlplugin (a zip).
# Run from the repo root after: dotnet build -c Release
$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $root 'QuickLook.Plugin.AudioViewer\bin\Release'
$outZip     = Join-Path $root 'QuickLook.Plugin.AudioViewer.zip'
$outPlugin  = Join-Path $root 'QuickLook.Plugin.AudioViewer.qlplugin'

if (-not (Test-Path $releaseDir)) {
    throw "Release output not found at $releaseDir. Run 'dotnet build -c Release' first."
}

if (Test-Path $outZip)    { Remove-Item $outZip }
if (Test-Path $outPlugin) { Remove-Item $outPlugin }

# Exclude debug symbols and XML doc files; ship DLLs + config at the zip root.
$items = Get-ChildItem -Path $releaseDir -File |
    Where-Object { $_.Extension -notin '.pdb', '.xml' }

Compress-Archive -Path ($items.FullName) -DestinationPath $outZip -Force
Move-Item $outZip $outPlugin

Write-Host "Created $outPlugin"
