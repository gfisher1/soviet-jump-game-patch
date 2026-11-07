#!/usr/bin/env bash
# Removes the Soviet Jump Game patch from a game directory.
#
# Usage: ./uninstall.sh [/path/to/game]
set -euo pipefail

game_dir="${1:-${SJG_GAME_DIR:-}}"
if [[ -z "$game_dir" ]]; then
  echo "usage: $0 /path/to/game" >&2
  exit 1
fi

rm -f "$game_dir/BepInEx/plugins/SjgPatch.dll"
echo "==> Removed plugin."

if [[ "${1:-}" == "--with-bepinex" || "${SJG_REMOVE_BEPINEX:-}" == "1" ]]; then
  rm -rf "$game_dir/BepInEx" \
         "$game_dir/doorstop_config.ini" \
         "$game_dir/.doorstop_version" \
         "$game_dir/winhttp.dll"
  echo "==> Removed BepInEx + doorstop."
else
  echo "==> Left BepInEx in place (pass --with-bepinex to remove it too)."
fi
