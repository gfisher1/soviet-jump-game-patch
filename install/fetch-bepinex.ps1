# Downloads and extracts BepInEx 5 (x64) into the game directory (Windows).
param(
    [Parameter(Position = 0)][string]$GameDir
)

$ErrorActionPreference = "Stop"
$BepInExVersion = "5.4.23.6"
$url = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"

if (-not $GameDir) { $GameDir = $env:SJG_GAME_DIR }
if (-not $GameDir) { Write-Error "usage: fetch-bepinex.ps1 -GameDir 'C:\path\to\game'"; exit 1 }

$tmp = Join-Path $env:TEMP ("sjg_bepinex_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$zip = Join-Path $tmp "bepinex.zip"

try {
    Write-Host "==> Downloading BepInEx $BepInExVersion ..."
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

    Write-Host "==> Extracting into $GameDir"
    Expand-Archive -Path $zip -DestinationPath $GameDir -Force
    Write-Host "==> BepInEx $BepInExVersion installed."
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
