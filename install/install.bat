@echo off
setlocal EnableExtensions
rem =====================================================================
rem  Soviet Jump Game - patch installer (Windows)
rem
rem  Just double-click this file. It finds the game, copies the bundled
rem  BepInEx loader and SjgPatch.dll into it. No internet connection,
rem  no admin rights and nothing else to install.
rem
rem  Or run:  Install.bat "C:\path\to\Soviet Jump Game"
rem =====================================================================

set "SRC=%~dp0"
set "GAME=%~1"
set "GAME=%GAME:"=%"

call :find_game
if not defined GAME (
  echo Could not find "Soviet Jump Game" automatically.
  echo.
  echo Drag the game folder onto this .bat file, or paste its full path
  echo ^(the folder that contains SovietJumpGame.exe^).
  echo.
  set /p "GAME=Game folder: "
  set "GAME=%GAME:"=%"
)

if not exist "%GAME%\SovietJumpGame.exe" (
  echo.
  echo error: "%GAME%" does not contain SovietJumpGame.exe
  echo.
  pause
  exit /b 1
)

echo.
echo ==^> Game dir: %GAME%
echo.

if not exist "%SRC%BepInEx\core\BepInEx.dll" (
  echo error: the bundled BepInEx folder was not found next to this script.
  echo.
  echo   * If you downloaded a release, extract the whole .zip first and run
  echo     Install.bat from inside the extracted folder.
  echo   * If you cloned the git repo, use install\install.ps1 instead
  echo     ^(it downloads BepInEx for you^).
  echo.
  pause
  exit /b 1
)

echo ==^> Installing BepInEx ...
robocopy "%SRC%BepInEx" "%GAME%\BepInEx" /E /NFL /NDL /NJH /NJS /NP >nul
copy /y "%SRC%winhttp.dll"         "%GAME%\" >nul
copy /y "%SRC%doorstop_config.ini" "%GAME%\" >nul
copy /y "%SRC%.doorstop_version"   "%GAME%\" >nul

echo ==^> Installing plugin ...
if not exist "%GAME%\BepInEx\plugins" mkdir "%GAME%\BepInEx\plugins"
copy /y "%SRC%SjgPatch.dll" "%GAME%\BepInEx\plugins\SjgPatch.dll" >nul

echo.
echo Done. Launch the game normally through Steam.
echo Config file (created on first run):
echo   %GAME%\BepInEx\config\games.gfi.sjg.patch.cfg
echo.
pause
exit /b 0

rem ---------------------------------------------------------------------
:find_game
if defined GAME if exist "%GAME%\SovietJumpGame.exe" exit /b 0
set "GAME="

rem 1) Ask Steam where it is installed (HKCU\Software\Valve\Steam -> SteamPath).
for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul') do (
  if exist "%%B\steamapps\common\Soviet Jump Game\SovietJumpGame.exe" (
    set "GAME=%%B\steamapps\common\Soviet Jump Game"
  )
)
if defined GAME exit /b 0

rem 2) Fall back to the usual library locations on every drive.
for %%D in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (
  for %%P in (
    "%%D:\Program Files (x86)\Steam\steamapps\common\Soviet Jump Game"
    "%%D:\Program Files\Steam\steamapps\common\Soviet Jump Game"
    "%%D:\Steam\steamapps\common\Soviet Jump Game"
    "%%D:\SteamLibrary\steamapps\common\Soviet Jump Game"
    "%%D:\Games\SteamLibrary\steamapps\common\Soviet Jump Game"
    "%%D:\steamapps\common\Soviet Jump Game"
  ) do if exist "%%~P\SovietJumpGame.exe" set "GAME=%%~P"
)
exit /b 0
