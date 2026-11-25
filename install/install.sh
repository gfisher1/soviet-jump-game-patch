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

# ---------------------------------------------------------------------------
# Play mode (online / offline) + default server
# ---------------------------------------------------------------------------
# set_cfg <file> <section> <key> <value>
# Merge one setting into the BepInEx config. The installer may be re-run at any
# time to change the choice: managed settings are updated in place and anything
# the installer does not manage is left alone.
set_cfg() {
  local file="$1" section="$2" key="$3" value="$4"
  mkdir -p "$(dirname "$file")"
  [[ -f "$file" ]] || : > "$file"
  local tmp; tmp="$(mktemp "${TMPDIR:-/tmp}/sjgcfg.XXXXXX")"
  awk -v section="$section" -v key="$key" -v value="$value" '
    function emit() { print key " = " value; done = 1 }
    /^[[:space:]]*\[[^]]*\][[:space:]]*$/ {
      if (insec && !done) emit()
      name = $0
      sub(/^[[:space:]]*\[/, "", name)
      sub(/\][[:space:]]*$/, "", name)
      insec = (name == section)
      if (insec) seen = 1
      print
      next
    }
    {
      if (insec && !done) {
        k = $0
        sub(/[[:space:]]*=.*$/, "", k)
        gsub(/^[[:space:]]+|[[:space:]]+$/, "", k)
        if (k == key) { emit(); next }
      }
      print
    }
    END {
      if (insec && !done) emit()
      if (!seen) { if (NR > 0) print ""; print "[" section "]"; print key " = " value }
    }
  ' "$file" > "$tmp"
  mv "$tmp" "$file"
}

default_server="sjg.dnd.wtf"

# How should the game be played? Online (the public server) is the default;
# offline keeps everything local and never touches the shut-down services.
mode="online"
if [[ -t 0 ]]; then
  printf 'Play online (public server) or offline (bots only)? [online/offline] [online]: '
  reply=""
  IFS= read -r reply || true
  case "${reply,,}" in
    off*|local*|solo*|single*) mode="offline" ;;
    *)                         mode="online" ;;
  esac
fi

server="$default_server"
if [[ "$mode" == "online" && -t 0 ]]; then
  printf 'Default server address [%s]: ' "$default_server"
  reply=""
  IFS= read -r reply || true
  if [[ -n "$reply" ]]; then server="$reply"; fi
fi

cfg="$game_dir/BepInEx/config/games.gfi.sjg.patch.cfg"
set_cfg "$cfg" General   OfflineMode         true
set_cfg "$cfg" Server    ApiBaseUrl          "$server"
if [[ "$mode" == "offline" ]]; then
  set_cfg "$cfg" Server    MatchmakingMode     Host
  set_cfg "$cfg" LocalMatch RegisterWithServer false
  echo "==> Offline mode: MATCHMAKING hosts a local match with bots (no account, no server)."
else
  set_cfg "$cfg" Server    MatchmakingMode     Vanilla
  set_cfg "$cfg" LocalMatch RegisterWithServer true
  echo "==> Online mode: MATCHMAKING joins the public server at $server."
fi
echo "==> Config updated: $cfg"

echo
echo "Done. Launch the game normally (through Steam)."
echo
echo "Steam / Proton (Linux) users: add this to the game's Launch Options:"
echo "    WINEDLLOVERRIDES=\"winhttp=n,b\" %command%"
echo
echo "Re-run this installer at any time to switch between online and offline."
echo "Config file: BepInEx/config/games.gfi.sjg.patch.cfg"
