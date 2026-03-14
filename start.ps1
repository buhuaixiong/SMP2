# ============================================
# Supplier System - One Click Start
# ============================================

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Supplier System - Start All Services" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = $ScriptDir  # Script is already in project root

Write-Host "[1/3] Starting .NET API (port 5001) ..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory "$ProjectRoot\SupplierSystem" -PassThru -NoNewWindow

Write-Host "[2/3] Starting Vue Frontend (port 5173) ..." -ForegroundColor Yellow
Start-Process -FilePath "npm" -ArgumentList "run", "dev:web" -WorkingDirectory "$ProjectRoot\app" -PassThru -NoNewWindow

Write-Host ""
Write-Host "[3/3] All services started!" -ForegroundColor Green
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Frontend: http://localhost:5173" -ForegroundColor Green
Write-Host "  API:      http://localhost:5001" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Enter to stop all services" -ForegroundColor Gray
Write-Host ""

Read-Host | Out-Null
Write-Host "Stopping services..." -ForegroundColor Yellow
taskkill /F /IM dotnet.exe 2>$null | Out-Null
taskkill /F /IM node.exe 2>$null | Out-Null
Write-Host "All services stopped" -ForegroundColor Green
