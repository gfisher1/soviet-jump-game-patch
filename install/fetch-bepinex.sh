#!/usr/bin/env bash
# Downloads and extracts BepInEx 5 (x64) into the game directory.
#
# Usage: ./fetch-bepinex.sh [/path/to/game]
set -euo pipefail

BEPINEX_VERSION="5.4.23.6"
url="https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/BepInEx_win_x64_${BEPINEX_VERSION}.zip"

game_dir="${1:-${SJG_GAME_DIR:-}}"
if [[ -z "$game_dir" ]]; then
  echo "usage: $0 /path/to/game" >&2
  exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

echo "==> Downloading BepInEx ${BEPINEX_VERSION} ..."
if command -v curl >/dev/null 2>&1; then
  curl -fsSL -o "$tmp/bepinex.zip" "$url"
elif command -v wget >/dev/null 2>&1; then
  wget -qO "$tmp/bepinex.zip" "$url"
else
  echo "error: need curl or wget." >&2
  exit 1
fi

echo "==> Extracting into $game_dir"
unzip -o -q "$tmp/bepinex.zip" -d "$game_dir"
echo "==> BepInEx ${BEPINEX_VERSION} installed."
