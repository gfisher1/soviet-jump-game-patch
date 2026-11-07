#!/usr/bin/env bash
# Installs the Soviet Jump Game patch (BepInEx + SjgPatch plugin) into a game directory.
#
# Usage: ./install.sh [/path/to/game]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/.." && pwd)"
BEPINEX_VERSION="5.4.23.6"

game_dir="${1:-${SJG_GAME_DIR:-}}"
if [[ -z "$game_dir" ]]; then
  echo "usage: $0 /path/to/Soviet\\ Jump\\ Game" >&2
  exit 1
fi
if [[ ! -f "$game_dir/SovietJumpGame.exe" ]]; then
  echo "error: '$game_dir' does not look like the Soviet Jump Game folder (no SovietJumpGame.exe)." >&2
  exit 1
fi

echo "==> Game dir: $game_dir"

# 1. BepInEx
if [[ ! -f "$game_dir/BepInEx/core/BepInEx.dll" ]]; then
  echo "==> Installing BepInEx ${BEPINEX_VERSION} ..."
  "$here/fetch-bepinex.sh" "$game_dir"
else
  echo "==> BepInEx already present."
fi

# 2. Plugin
if [[ ! -f "$repo/dist/SjgPatch.dll" ]]; then
  echo "==> SjgPatch.dll not built; building ..."
  "$repo/plugin/build.sh" "$game_dir"
fi
mkdir -p "$game_dir/BepInEx/plugins"
cp -f "$repo/dist/SjgPatch.dll" "$game_dir/BepInEx/plugins/SjgPatch.dll"
echo "==> Installed plugin -> BepInEx/plugins/SjgPatch.dll"

echo
echo "Done. Launch the game normally (through Steam)."
echo
echo "Steam / Proton (Linux) users: add this to the game's Launch Options:"
echo "    WINEDLLOVERRIDES=\"winhttp=n,b\" %command%"
echo
echo "Config file (created on first run): BepInEx/config/games.gfi.sjg.patch.cfg"
