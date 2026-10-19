@echo off
setlocal enabledelayedexpansion
title SpendWise UI Preview Launcher

echo ===================================================
echo   SPENDWISE UI PREVIEW LAUNCHER
echo ===================================================
echo.

set "TARGET_URL=http://localhost:5173/preview/ui"
set "PORT=5173"

:: 1. Check if server on port 5173 is already active
netstat -ano | findstr ":%PORT%" | findstr "LISTENING" >nul
if !errorlevel! equ 0 (
    echo [INFO] Vite Dev Server is ALREADY running on port %PORT%.
    echo [INFO] Skipping server startup to prevent duplicate instances.
    goto :OPEN_BROWSER
)

:: 2. If server not running, start Vite dev server on port 5173 in a separate window
echo [INFO] Vite Dev Server is NOT running on port %PORT%.
echo [INFO] Starting SpendWise Vite Dev Server...

start "SpendWise Dev Server (Port 5173)" cmd /k "npx vite --host=0.0.0.0 --port=5173"

:: 3. Wait for the server to be responsive (up to 20 seconds)
echo [INFO] Waiting for server at http://localhost:%PORT% to become ready...
set /a ATTEMPTS=0

:WAIT_LOOP
timeout /t 1 /nobreak >nul
set /a ATTEMPTS+=1

netstat -ano | findstr ":%PORT%" | findstr "LISTENING" >nul
if !errorlevel! equ 0 (
    echo [SUCCESS] Server is up and listening on port %PORT%!
    goto :OPEN_BROWSER
)

if !ATTEMPTS! geq 20 (
    echo.
    echo [ERROR] Timeout: Vite Dev Server did not start within 20 seconds.
    echo Please make sure Node.js is installed and run 'npx vite --port=5173' manually in this folder.
    echo.
    pause
    exit /b 1
)

goto :WAIT_LOOP

:OPEN_BROWSER
echo.
echo [INFO] Opening default browser to SpendWise UI Preview Center:
echo        %TARGET_URL%
echo.

start "" "%TARGET_URL%"

echo [DONE] Browser launched.
echo [TIP]  Keep the 'SpendWise Dev Server' window open to keep the server running.
echo        Close that window or press Ctrl+C in it whenever you want to stop the server.
ping 127.0.0.1 -n 3 >nul
exit /b 0
