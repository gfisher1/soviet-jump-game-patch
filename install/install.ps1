# Installs the Soviet Jump Game patch (BepInEx + SjgPatch plugin) on Windows.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File install.ps1 -GameDir "C:\path\to\Soviet Jump Game"
#   (or: install.bat "C:\path\to\Soviet Jump Game")
#
#   -GameDir      Path to the folder containing SovietJumpGame.exe
#   -Remove       Uninstall the plugin instead (add -WithBepInEx to remove BepInEx too)
#   -WithBepInEx  With -Remove: also delete BepInEx + doorstop files
param(
    [Parameter(Position = 0)][string]$GameDir,
    [switch]$Remove,
    [switch]$WithBepInEx
)

$ErrorActionPreference = "Stop"

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $here

if (-not $GameDir) { $GameDir = $env:SJG_GAME_DIR }
if (-not $GameDir) {
    Write-Error "usage: install.ps1 -GameDir 'C:\path\to\Soviet Jump Game'"
    exit 1
}

if (-not (Test-Path (Join-Path $GameDir "SovietJumpGame.exe"))) {
    Write-Error "'$GameDir' does not look like the Soviet Jump Game folder (no SovietJumpGame.exe)."
    exit 1
}

Write-Host "==> Game dir: $GameDir"

$pluginDir = Join-Path $GameDir "BepInEx\plugins"

if ($Remove) {
    $dll = Join-Path $pluginDir "SjgPatch.dll"
    if (Test-Path $dll) { Remove-Item $dll -Force; Write-Host "==> Removed plugin." }
    else { Write-Host "==> Plugin not present." }

    if ($WithBepInEx) {
        foreach ($p in @("BepInEx", "doorstop_config.ini", ".doorstop_version", "winhttp.dll")) {
            $t = Join-Path $GameDir $p
            if (Test-Path $t) { Remove-Item $t -Recurse -Force }
        }
        Write-Host "==> Removed BepInEx + doorstop."
    } else {
        Write-Host "==> Left BepInEx in place (use -WithBepInEx to remove it too)."
    }
    exit 0
}

# 1. BepInEx
if (-not (Test-Path (Join-Path $GameDir "BepInEx\core\BepInEx.dll"))) {
    Write-Host "==> Installing BepInEx ..."
    & (Join-Path $here "fetch-bepinex.ps1") -GameDir $GameDir
} else {
    Write-Host "==> BepInEx already present."
}

# 2. Plugin
$dist = Join-Path $repo "dist\SjgPatch.dll"
if (-not (Test-Path $dist)) {
    Write-Error "dist\SjgPatch.dll not found. Build it (see README) or use a release download."
    exit 1
}
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item $dist (Join-Path $pluginDir "SjgPatch.dll") -Force
Write-Host "==> Installed plugin -> BepInEx\plugins\SjgPatch.dll"

Write-Host ""
Write-Host "Done. Launch the game normally (through Steam)."
Write-Host "Config file (created on first run): BepInEx\config\games.gfi.sjg.patch.cfg"
