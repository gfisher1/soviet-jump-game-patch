#!/usr/bin/env bash
# Builds the SjgPatch BepInEx plugin with Mono's C# compiler (mcs).
#
# Usage:  ./build.sh [/path/to/game] [output-dir]
#   game dir defaults to $SJG_GAME_DIR, then "/project/Soviet Jump Game"
#   output   defaults to <repo>/dist
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/.." && pwd)"

game_dir="${1:-${SJG_GAME_DIR:-/project/Soviet Jump Game}}"
out_dir="${2:-$repo/dist}"

managed="$game_dir/SovietJumpGame_Data/Managed"
bepinex="$game_dir/BepInEx/core"

if [[ ! -d "$managed" ]]; then
  echo "error: could not find '$managed' (pass the game dir as the first argument)" >&2
  exit 1
fi
if [[ ! -f "$bepinex/BepInEx.dll" ]]; then
  echo "error: BepInEx is not installed in '$game_dir' (missing $bepinex/BepInEx.dll)." >&2
  echo "       run ../loader/fetch-bepinex.sh and extract it, or install BepInEx 5 x64 manually." >&2
  exit 1
fi

mkdir -p "$out_dir"

refs=(
  "$bepinex/BepInEx.dll"
  "$bepinex/0Harmony.dll"
  "$managed/UnityEngine.dll"
  "$managed/UnityEngine.CoreModule.dll"
  "$managed/Scripts.dll"
  "$managed/PlayFab.dll"
  "$managed/Facepunch.Steamworks.Win64.dll"
  "$managed/netstandard.dll"
  "$managed/bolt.dll"
  "$managed/bolt.user.dll"
  "$managed/udpkit.common.dll"
  "$managed/UnityEngine.InputLegacyModule.dll"
)

ref_args=()
for r in "${refs[@]}"; do ref_args+=("-r:$r"); done

echo "Compiling SjgPatch -> $out_dir/SjgPatch.dll"
mcs -target:library -optimize+ -out:"$out_dir/SjgPatch.dll" "${ref_args[@]}" "$here/src/"*.cs
echo "Done."
