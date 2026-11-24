#!/usr/bin/env bash
# Installs the Soviet Jump Game patch (BepInEx + SjgPatch plugin).
#
# Works in two layouts:
#   * Release bundle  -- this script sits next to a bundled `BepInEx/` and
#     `SjgPatch.dll`: everything is copied from the bundle. No internet needed.
#   * Git checkout    -- no bundled BepInEx: it is downloaded once (curl/wget +
#     unzip) and, if `dist/SjgPatch.dll` is missing, built with Mono.
#
# Usage: ./install.sh ["/path/to/Soviet Jump Game"]
#   The game folder is auto-detected for standard Steam libraries when omitted.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BEPINEX_VERSION="5.4.23.6"

# Where the repo/bundle root is (bundle: $here, checkout: $here/..).
if [[ -d "$here/plugin" || -f "$here/dist/SjgPatch.dll" ]]; then
  root="$here"
else
  root="$(cd "$here/.." && pwd)"
fi

# ---------------------------------------------------------------------------
# Locate the game
# ---------------------------------------------------------------------------
find_game() {
  local c
  for c in "$1" "${SJG_GAME_DIR:-}" \
           "$HOME/.steam/steam/steamapps/common/Soviet Jump Game" \
           "$HOME/.local/share/Steam/steamapps/common/Soviet Jump Game" \
           "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/Soviet Jump Game"; do
    [[ -n "$c" && -f "$c/SovietJumpGame.exe" ]] && { printf '%s' "$c"; return 0; }
  done

  # Other Steam libraries listed in libraryfolders.vdf.
  local steam vdf lib
  for steam in "$HOME/.steam/steam" "$HOME/.local/share/Steam" \
               "$HOME/.var/app/com.valvesoftware.Steam/data/Steam"; do
    vdf="$steam/steamapps/libraryfolders.vdf"
    [[ -f "$vdf" ]] || continue
    while IFS= read -r lib; do
      lib="${lib%/}"
      lib="${lib//\\\\/\/}"
      if [[ -f "$lib/steamapps/common/Soviet Jump Game/SovietJumpGame.exe" ]]; then
        printf '%s' "$lib/steamapps/common/Soviet Jump Game"; return 0
      fi
    done < <(grep -oE '"path"[[:space:]]*"[^"]+"' "$vdf" 2>/dev/null \
             | sed -E 's/.*"path"[[:space:]]*"([^"]+)".*/\1/')
  done
  return 1
}

game_dir="$(find_game "${1:-}")" || {
  echo "error: could not find 'Soviet Jump Game' automatically." >&2
  echo "usage: $0 \"/path/to/Soviet Jump Game\"" >&2
  exit 1
}
echo "==> Game dir: $game_dir"

# ---------------------------------------------------------------------------
# BepInEx
# ---------------------------------------------------------------------------
if [[ -f "$game_dir/BepInEx/core/BepInEx.dll" ]]; then
  echo "==> BepInEx already present."
elif [[ -f "$here/BepInEx/core/BepInEx.dll" ]]; then
  echo "==> Installing bundled BepInEx ${BEPINEX_VERSION} ..."
  cp -rf "$here/BepInEx" "$game_dir/"
  for f in winhttp.dll doorstop_config.ini .doorstop_version; do
    [[ -e "$here/$f" ]] && cp -f "$here/$f" "$game_dir/"
  done
else
  echo "==> Bundled BepInEx not found; downloading ${BEPINEX_VERSION} ..."
  fetch=""
  for c in "$here/fetch-bepinex.sh" "$here/install/fetch-bepinex.sh"; do
    [[ -f "$c" ]] && { fetch="$c"; break; }
  done
  [[ -n "$fetch" ]] || { echo "error: fetch-bepinex.sh not found." >&2; exit 1; }
  "$fetch" "$game_dir"
fi

# ---------------------------------------------------------------------------
# Plugin
# ---------------------------------------------------------------------------
dll=""
for c in "$here/SjgPatch.dll" "$root/dist/SjgPatch.dll"; do
  [[ -f "$c" ]] && { dll="$c"; break; }
done
if [[ -z "$dll" ]]; then
  echo "==> SjgPatch.dll not found; building with Mono ..."
  "$root/plugin/build.sh" "$game_dir"
  dll="$root/dist/SjgPatch.dll"
fi
mkdir -p "$game_dir/BepInEx/plugins"
cp -f "$dll" "$game_dir/BepInEx/plugins/SjgPatch.dll"
echo "==> Installed plugin -> BepInEx/plugins/SjgPatch.dll"

echo
echo "Done. Launch the game normally (through Steam)."
echo
echo "Steam / Proton (Linux) users: add this to the game's Launch Options:"
echo "    WINEDLLOVERRIDES=\"winhttp=n,b\" %command%"
echo
echo "Config file (created on first run): BepInEx/config/games.gfi.sjg.patch.cfg"
