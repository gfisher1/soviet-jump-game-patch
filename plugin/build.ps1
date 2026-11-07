# Builds the SjgPatch plugin on Windows using Mono's mcs (if installed).
# The prebuilt dist/SjgPatch.dll works on both Windows and Linux, so building is optional.
#
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1 -GameDir "C:\path\to\Soviet Jump Game"
param(
    [Parameter(Position = 0)][string]$GameDir,
    [string]$OutDir
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $here

if (-not $GameDir) { $GameDir = $env:SJG_GAME_DIR }
if (-not $GameDir) { $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Soviet Jump Game" }
if (-not $OutDir) { $OutDir = Join-Path $repo "dist" }

$managed = Join-Path $GameDir "SovietJumpGame_Data\Managed"
$bepinex = Join-Path $GameDir "BepInEx\core"

if (-not (Test-Path $managed)) { Write-Error "could not find '$managed'"; exit 1 }

$mcs = Get-Command mcs -ErrorAction SilentlyContinue
if (-not $mcs) {
    Write-Warning "Mono 'mcs' was not found on PATH."
    Write-Host    "Use the prebuilt dist\SjgPatch.dll (works on Windows and Linux), or install Mono and re-run."
    exit 1
}

$refs = @(
    "$bepinex\BepInEx.dll",
    "$bepinex\0Harmony.dll",
    "$managed\UnityEngine.dll",
    "$managed\UnityEngine.CoreModule.dll",
    "$managed\UnityEngine.InputLegacyModule.dll",
    "$managed\UnityEngine.IMGUIModule.dll",
    "$managed\UnityEngine.UnityWebRequestModule.dll",
    "$managed\Scripts.dll",
    "$managed\PlayFab.dll",
    "$managed\Facepunch.Steamworks.Win64.dll",
    "$managed\netstandard.dll",
    "$managed\bolt.dll",
    "$managed\bolt.user.dll",
    "$managed\udpkit.common.dll",
    "$managed\Newtonsoft.Json.dll",
    "$managed\QFSW.QC.dll",
    "$managed\UniRx.Async.dll"
) | ForEach-Object { "-r:$_" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$sources = Get-ChildItem (Join-Path $here "src") -Filter *.cs | ForEach-Object { $_.FullName }

Write-Host "Compiling SjgPatch -> $OutDir\SjgPatch.dll"
& mcs -target:library -optimize+ "-out:$OutDir\SjgPatch.dll" @refs @sources
if ($LASTEXITCODE -ne 0) { Write-Error "build failed"; exit $LASTEXITCODE }
Write-Host "Done."
