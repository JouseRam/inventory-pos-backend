#!/usr/bin/env bash
# Builds the desktop register (Windows x64, self-contained) from Linux or macOS, where the Windows Forms SDK is not
# installed: same sources as InventoryPOS.Desktop, with Windows Forms taken from its NuGet packs.
# On Windows use build/publish-desktop.ps1 instead.
#   bash build/desktop-linux/publish.sh [carpeta-salida]
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-$here/../../artifacts/AnnasBeautySupply-Caja}"
if grep -q 'Value = "";' "$here/../../InventoryPOS.Desktop/LicensePublicKey.cs"; then
  echo "Falta la llave pública de licencias en InventoryPOS.Desktop/LicensePublicKey.cs" >&2; exit 1
fi
rm -rf "$out"
dotnet publish "$here/MatizPOS.csproj" -c Release -r win-x64 --self-contained true -o "$out" \
  -p:DebugType=None -p:DebugSymbols=false -p:EnableWindowsTargeting=true
rm -f "$out/appsettings.Development.json"
echo "Listo: $out — copia la carpeta a la caja y abre MatizPOS.exe."
