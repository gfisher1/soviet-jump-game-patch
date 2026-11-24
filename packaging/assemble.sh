#!/usr/bin/env bash
# Builds the self-contained, offline install bundle for the patch.
#
#   dist/sjg-patch-<version>.zip   <- give this to end users
#
# The bundle contains BepInEx 5 x64 plus the compiled plugin, so the end user
# installs *nothing* and needs *no* internet connection: unzip, run
# Install.bat (Windows) or ./install.sh (Linux/Proton).
#
# Usage: packaging/assemble.sh [version]
#   version defaults to `git describe --tags`, e.g. v1.0.0.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/.." && pwd)"
dist="$repo/dist"

BEPINEX_VERSION="5.4.23.6"
bepinex_zip="BepInEx_win_x64_${BEPINEX_VERSION}.zip"
bepinex_url="https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/${bepinex_zip}"
cache="${SJG_BEPINEX_CACHE:-$dist/.cache}"

version="${1:-}"
if [[ -z "$version" ]]; then
  version="$(git -C "$repo" describe --tags --always 2>/dev/null || echo dev)"
fi

stage="$dist/.stage"
out="$dist/sjg-patch-${version}.zip"

mkdir -p "$dist" "$cache"
rm -rf "$stage"
mkdir -p "$stage"

# ---------------------------------------------------------------------------
# 1. Fetch BepInEx (cached)
# ---------------------------------------------------------------------------
zip_path="$cache/$bepinex_zip"
if [[ ! -f "$zip_path" ]]; then
  echo "==> Downloading BepInEx ${BEPINEX_VERSION} ..."
  if command -v curl >/dev/null 2>&1; then
    curl -fsSL -o "$zip_path.part" "$bepinex_url"
  elif command -v wget >/dev/null 2>&1; then
    wget -qO "$zip_path.part" "$bepinex_url"
  else
    echo "error: need curl or wget to download BepInEx." >&2
    exit 1
  fi
  mv "$zip_path.part" "$zip_path"
else
  echo "==> Using cached BepInEx ${BEPINEX_VERSION}"
fi

# ---------------------------------------------------------------------------
# 2. Stage the bundle
# ---------------------------------------------------------------------------
echo "==> Staging bundle ..."
if command -v unzip >/dev/null 2>&1; then
  unzip -o -q "$zip_path" -d "$stage"
else
  python3 - "$zip_path" "$stage" <<'PY'
import sys, zipfile
with zipfile.ZipFile(sys.argv[1]) as z:
    z.extractall(sys.argv[2])
PY
fi

[[ -f "$stage/BepInEx/core/BepInEx.dll" ]] || { echo "error: BepInEx extraction failed." >&2; exit 1; }

# The plugin.
[[ -f "$repo/dist/SjgPatch.dll" ]] || { echo "error: dist/SjgPatch.dll is missing (build it first)." >&2; exit 1; }
cp -f "$repo/dist/SjgPatch.dll" "$stage/SjgPatch.dll"

# Installers + readme.
cp -f "$repo/install/install.sh"     "$stage/install.sh"
cp -f "$repo/install/uninstall.sh"   "$stage/uninstall.sh"
cp -f "$repo/install/install.bat"    "$stage/Install.bat"
cp -f "$repo/install/uninstall.bat"  "$stage/Uninstall.bat"
cp -f "$here/README.txt"             "$stage/README.txt"
chmod +x "$stage/install.sh" "$stage/uninstall.sh"

# ---------------------------------------------------------------------------
# 3. Zip it up
# ---------------------------------------------------------------------------
echo "==> Writing $out"
rm -f "$out"
if command -v zip >/dev/null 2>&1; then
  ( cd "$stage" && zip -q -r -X "$out" . )
else
  python3 - "$out" "$stage" <<'PY'
import os, sys, zipfile
out, stage = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for root, _dirs, files in os.walk(stage):
        for name in files:
            full = os.path.join(root, name)
            arc  = os.path.relpath(full, stage)
            zi = zipfile.ZipInfo.from_file(full, arc)
            zi.compress_type = zipfile.ZIP_DEFLATED
            with open(full, "rb") as fh, z.open(zi, "w") as dst:
                dst.write(fh.read())
print("wrote", out)
PY
fi

rm -rf "$stage"
echo
ls -lh "$out"
