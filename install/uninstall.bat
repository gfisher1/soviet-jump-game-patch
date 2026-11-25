@echo off
setlocal EnableExtensions
rem =====================================================================
rem  Soviet Jump Game - patch uninstaller (Windows)
rem
rem  Double-click to remove SjgPatch.dll from the game.
rem  Pass -WithBepInEx to also remove the BepInEx loader.
rem
rem  Usage:  Uninstall.bat ["C:\path\to\game"] [-WithBepInEx]
rem =====================================================================

set "GAME="
set "WITHBEP=0"
for %%A in (%*) do (
  if /i "%%~A"=="-WithBepInEx" (set "WITHBEP=1") else (set "GAME=%%~A")
)

if not defined GAME (
  echo Drag the game folder onto this .bat file, or paste its full path.
  set /p "GAME=Game folder: "
  set "GAME=%GAME:"=%"
)

if not exist "%GAME%\SovietJumpGame.exe" (
  echo error: "%GAME%" does not contain SovietJumpGame.exe
  pause
  exit /b 1
)

if exist "%GAME%\BepInEx\plugins\SjgPatch.dll" (
  del /f /q "%GAME%\BepInEx\plugins\SjgPatch.dll"
  echo ==^> Removed plugin.
) else (
  echo ==^> Plugin not present.
)

if exist "%GAME%\BepInEx\config\games.gfi.sjg.patch.cfg" (
  del /f /q "%GAME%\BepInEx\config\games.gfi.sjg.patch.cfg"
  echo ==^> Removed patch config (games.gfi.sjg.patch.cfg).
)

if "%WITHBEP%"=="1" (
  if exist "%GAME%\BepInEx" rmdir /s /q "%GAME%\BepInEx"
  del /f /q "%GAME%\doorstop_config.ini" "%GAME%\.doorstop_version" "%GAME%\winhttp.dll" 2>nul
  echo ==^> Removed BepInEx + doorstop.
) else (
  echo ==^> Left BepInEx in place ^(pass -WithBepInEx to remove it too^).
)

echo.
pause
exit /b 0
