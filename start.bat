@echo off
chcp 65001 >nul
echo ============================================
echo   Supplier System - Start All Services
echo ============================================
echo.
echo [1/3] Starting .NET API (port 5001) ...
echo.
cd /d "%~dp0SupplierSystem\src\SupplierSystem.Api"
start "SupplierSystem API" cmd /c "dotnet run & pause"
cd /d "%~dp0"

echo [2/3] Starting Vue Frontend (port 5173) ...
start "Vue Frontend" cmd /c "cd /d "%~dp0app" && npm run dev:web & pause"

echo.
echo [3/3] All services started!
echo.
echo ============================================
echo   Frontend: http://localhost:5173
echo   API:      http://localhost:5001
echo ============================================
echo.
pause
