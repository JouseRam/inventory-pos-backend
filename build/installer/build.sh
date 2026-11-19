#!/usr/bin/env bash
# Builds the one-file installer of the desktop register (Instalar-AnnasBeautySupply.exe) from Linux or macOS:
# publishes the register (build/desktop-linux) and packs it with NSIS (apt install nsis / brew install makensis).
#   bash build/installer/build.sh [instalador.exe]
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="${1:-$root/artifacts/Instalar-AnnasBeautySupply.exe}"
app="$root/artifacts/AnnasBeautySupply-Caja"
mkdir -p "$(dirname "$out")"
bash "$root/build/desktop-linux/publish.sh" "$app"
version="$(grep -oP '(?<=<Version>)[^<]+' "$root/build/desktop-linux/MatizPOS.csproj")"
(cd "$here" && makensis -V2 -DSRC="$app" -DOUT="$(realpath -m "$out")" -DVERSION="$version" AnnasBeautySupply.nsi)
echo "Instalador: $out"
