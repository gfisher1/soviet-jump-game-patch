@echo off
rem Convenience wrapper for install.ps1 (Windows).
rem Usage: install.bat "C:\path\to\Soviet Jump Game"   [or -Remove [-WithBepInEx]]
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
endlocal
