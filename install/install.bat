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

rem ---- play mode (online / offline) + default server ---------------------
set "DEFAULT_SERVER=sjg.dnd.wtf"
set "MODE=online"
set "ANSWER="
set /p "ANSWER=Play online (public server) or offline (bots only)? [online/offline] [online]: "
if /i "%ANSWER:~0,3%"=="off" set "MODE=offline"

set "SERVER=%DEFAULT_SERVER%"
if /i "%MODE%"=="online" call :ask_server

set "CFG=%GAME%\BepInEx\config\games.gfi.sjg.patch.cfg"
if not exist "%GAME%\BepInEx\config" mkdir "%GAME%\BepInEx\config"

rem  Merge the choice into the config so this installer can be re-run at any
rem  time to switch modes; other settings are left untouched.
call :set_cfg General   OfflineMode         true
call :set_cfg Server    ApiBaseUrl          "%SERVER%"

if /i "%MODE%"=="offline" goto :mode_offline
call :set_cfg Server    MatchmakingMode     Vanilla
call :set_cfg LocalMatch RegisterWithServer true
echo ==^> Online mode: MATCHMAKING joins the public server at %SERVER%.
goto :mode_done

:mode_offline
call :set_cfg Server    MatchmakingMode     Host
call :set_cfg LocalMatch RegisterWithServer false
echo ==^> Offline mode: MATCHMAKING hosts a local match with bots.
:mode_done

echo ==^> Config updated: %CFG%
echo.
echo Done. Launch the game normally through Steam.
echo Re-run this installer at any time to switch between online and offline.
echo Config file:
echo   %CFG%
echo.
pause
exit /b 0

:ask_server
set "SERVER="
set /p "SERVER=Default server address [%DEFAULT_SERVER%]: "
if "%SERVER%"=="" set "SERVER=%DEFAULT_SERVER%"
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

rem ---------------------------------------------------------------------
rem  set_cfg <section> <key> <value>
rem  Merge one setting into the BepInEx config: update it if present,
rem  otherwise add it (creating the section if needed). Other settings are
rem  preserved, so the installer can be re-run to change modes.
rem ---------------------------------------------------------------------
:set_cfg
setlocal EnableDelayedExpansion
set "SEC=%~1"
set "KEY=%~2"
set "VAL=%~3"
set "TMP=%CFG%.new"
set "INSEC=0"
set "DONE=0"
set "SEEN=0"
break>"%TMP%"
if exist "%CFG%" for /f "usebackq delims=" %%L in ("%CFG%") do call :scf_line "%%L"
if "!INSEC!"=="1" if "!DONE!"=="0" >>"%TMP%" echo !KEY! = !VAL!
if "!SEEN!"=="0" (
  >>"%TMP%" echo.
  >>"%TMP%" echo [!SEC!]
  >>"%TMP%" echo !KEY! = !VAL!
)
move /y "%TMP%" "%CFG%" >nul
endlocal
exit /b 0

:scf_line
set "LINE=%~1"
if "!LINE:~0,1!!LINE:~-1!"=="[]" (
  call :scf_section
) else if "!INSEC!"=="1" (
  call :scf_key
) else (
  >>"%TMP%" echo !LINE!
)
exit /b 0

:scf_section
if "!INSEC!"=="1" if "!DONE!"=="0" (
  >>"%TMP%" echo !KEY! = !VAL!
  set "DONE=1"
)
set "NAME=!LINE:~1,-1!"
set "INSEC=0"
if /i "!NAME!"=="!SEC!" set "INSEC=1"
if "!INSEC!"=="1" set "SEEN=1"
>>"%TMP%" echo !LINE!
exit /b 0

:scf_key
for /f "tokens=1 delims==" %%K in ("!LINE!") do (
  set "K=%%K"
  for /f "tokens=* delims= " %%A in ("!K!") do set "K=%%A"
  if /i "!K!"=="!KEY!" (
    >>"%TMP%" echo !KEY! = !VAL!
    set "DONE=1"
    exit /b 0
  )
)
>>"%TMP%" echo !LINE!
exit /b 0
