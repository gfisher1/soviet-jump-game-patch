#!/usr/bin/env bash
# Removes the Soviet Jump Game patch from a game directory.
#
# Usage: ./uninstall.sh ["/path/to/game"] [--with-bepinex]
set -euo pipefail

game_dir=""
with_bepinex="${SJG_REMOVE_BEPINEX:-0}"
for a in "$@"; do
  case "$a" in
    --with-bepinex) with_bepinex=1 ;;
    *)              game_dir="$a" ;;
  esac
done

if [[ -z "$game_dir" ]]; then
  for c in "${SJG_GAME_DIR:-}" \
           "$HOME/.steam/steam/steamapps/common/Soviet Jump Game" \
           "$HOME/.local/share/Steam/steamapps/common/Soviet Jump Game" \
           "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/Soviet Jump Game"; do
    [[ -n "$c" && -f "$c/SovietJumpGame.exe" ]] && { game_dir="$c"; break; }
  done
fi
if [[ -z "$game_dir" ]]; then
  echo "usage: $0 \"/path/to/game\" [--with-bepinex]" >&2
  exit 1
fi

rm -f "$game_dir/BepInEx/plugins/SjgPatch.dll"
echo "==> Removed plugin."

if [[ "$with_bepinex" == "1" ]]; then
  rm -rf "$game_dir/BepInEx" \
         "$game_dir/doorstop_config.ini" \
         "$game_dir/.doorstop_version" \
         "$game_dir/winhttp.dll"
  echo "==> Removed BepInEx + doorstop."
else
  echo "==> Left BepInEx in place (pass --with-bepinex to remove it too)."
fi
