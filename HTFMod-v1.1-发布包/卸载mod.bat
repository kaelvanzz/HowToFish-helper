@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "mod-manage.ps1" -Mode uninstall
pause

