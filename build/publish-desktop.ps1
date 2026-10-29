# Publishes the desktop register as a self-contained folder (no .NET install needed on the register PC).
# Usage:  powershell -ExecutionPolicy Bypass -File build\publish-desktop.ps1 [-Output <folder>]
param(
    [string]$Output = (Join-Path $PSScriptRoot "..\artifacts\MatizPOS")
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\InventoryPOS.Desktop\InventoryPOS.Desktop.csproj"

if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }

dotnet publish $project -c Release -r win-x64 --self-contained true -o $Output -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# Development-only settings never ship to a register.
Get-ChildItem $Output -Filter "appsettings.Development.json" | Remove-Item -Force

$size = "{0:N0} MB" -f ((Get-ChildItem $Output -Recurse | Measure-Object Length -Sum).Sum / 1MB)
Write-Host ""
Write-Host "Listo: $Output ($size)"
Write-Host "Copia la carpeta a la caja y abre MatizPOS.exe. Los datos quedan en %LOCALAPPDATA%\MatizPOS."
