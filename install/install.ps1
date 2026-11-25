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

    $cfg = Join-Path $GameDir "BepInEx\config\games.gfi.sjg.patch.cfg"
    if (Test-Path $cfg) { Remove-Item $cfg -Force; Write-Host "==> Removed patch config (games.gfi.sjg.patch.cfg)." }

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

# 3. Play mode (online / offline) + default server
# Set-Cfg merges one setting into the BepInEx config, so the installer can be
# re-run at any time to change the choice; other settings are left untouched.
function Set-Cfg {
    param([string]$Path, [string]$Section, [string]$Key, [string]$Value)

    $lines = @()
    if (Test-Path -LiteralPath $Path) { $lines = @(Get-Content -LiteralPath $Path) }

    $out = New-Object System.Collections.Generic.List[string]
    $inSection = $false
    $done = $false
    $seenSection = $false

    foreach ($line in $lines) {
        if ($line -match '^\s*\[(.+?)\]\s*$') {
            if ($inSection -and -not $done) { $out.Add("$Key = $Value"); $done = $true }
            $name = $Matches[1].Trim()
            $inSection = ($name -eq $Section)
            if ($inSection) { $seenSection = $true }
            $out.Add($line)
            continue
        }
        if ($inSection -and -not $done) {
            $k = ($line -replace '\s*=.*$', '').Trim()
            if ($k -eq $Key) { $out.Add("$Key = $Value"); $done = $true; continue }
        }
        $out.Add($line)
    }
    if ($inSection -and -not $done) { $out.Add("$Key = $Value"); $done = $true }
    if (-not $seenSection) {
        if ($out.Count -gt 0) { $out.Add("") }
        $out.Add("[$Section]")
        $out.Add("$Key = $Value")
    }

    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Set-Content -LiteralPath $Path -Value $out -Encoding ASCII
}

$defaultServer = "sjg.dnd.wtf"

# Online (the public server) is the default; offline keeps everything local and
# never touches the shut-down services.
$mode = "online"
$answer = Read-Host "Play online (public server) or offline (bots only)? [online/offline] [online]"
if ($answer -match '^\s*(off|local|solo|single)') { $mode = "offline" }

$server = $defaultServer
if ($mode -eq "online") {
    $server = Read-Host "Default server address [$defaultServer]"
    if ([string]::IsNullOrWhiteSpace($server)) { $server = $defaultServer }
}

$cfg = Join-Path $GameDir "BepInEx\config\games.gfi.sjg.patch.cfg"
Set-Cfg $cfg "General"   "OfflineMode"         "true"
Set-Cfg $cfg "Server"    "ApiBaseUrl"          $server
if ($mode -eq "offline") {
    Set-Cfg $cfg "Server"    "MatchmakingMode"     "Host"
    Set-Cfg $cfg "LocalMatch" "RegisterWithServer" "false"
    Write-Host "==> Offline mode: MATCHMAKING hosts a local match with bots."
} else {
    Set-Cfg $cfg "Server"    "MatchmakingMode"     "Vanilla"
    Set-Cfg $cfg "LocalMatch" "RegisterWithServer" "true"
    Write-Host "==> Online mode: MATCHMAKING joins the public server at $server."
}
Write-Host "==> Config updated: $cfg"

Write-Host ""
Write-Host "Done. Launch the game normally (through Steam)."
Write-Host "Re-run this installer at any time to switch between online and offline."
Write-Host "Config file: BepInEx\config\games.gfi.sjg.patch.cfg"
