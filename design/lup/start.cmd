@echo off
setlocal
cd /d "%~dp0"
echo LUP: http://localhost:3001
echo Stop med Ctrl+C.
where py >nul 2>nul
if not errorlevel 1 (
  py -3 -m http.server 3001 --bind 127.0.0.1
) else (
  python -m http.server 3001 --bind 127.0.0.1
)
