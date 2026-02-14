# ============================================
# Supplier System - Build & Publish (SPA + API)
# ============================================

[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Output = "",
    [bool]$SelfContained = $true,
    [switch]$SingleFile,
    [switch]$CleanWwwroot
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = $ScriptDir

$AppDir = Join-Path $Root "app"
$WebDir = Join-Path $AppDir "apps\web"
$ApiDir = Join-Path $Root "SupplierSystem\src\SupplierSystem.Api"
$DistDir = Join-Path $WebDir "dist"
$Wwwroot = Join-Path $ApiDir "wwwroot"

if (-not $Output) {
    $Output = Join-Path $Root "artifacts\publish"
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Supplier System - Build & Publish" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/3] Building Vue frontend..." -ForegroundColor Yellow
Push-Location $AppDir
try {
    npm run build -w apps/web
} finally {
    Pop-Location
}

if (-not (Test-Path $DistDir)) {
    throw "Frontend build output not found: $DistDir"
}

Write-Host "[2/3] Copying frontend to API wwwroot..." -ForegroundColor Yellow
if ($CleanWwwroot -and (Test-Path $Wwwroot)) {
    Get-ChildItem -Path $Wwwroot -Force | Remove-Item -Recurse -Force
}
New-Item -Path $Wwwroot -ItemType Directory -Force | Out-Null
Copy-Item -Path (Join-Path $DistDir "*") -Destination $Wwwroot -Recurse -Force

Write-Host "[3/3] Publishing .NET API..." -ForegroundColor Yellow
New-Item -Path $Output -ItemType Directory -Force | Out-Null

$publishArgs = @(
    "publish",
    (Join-Path $ApiDir "SupplierSystem.Api.csproj"),
    "-c", $Configuration,
    "-o", $Output
)

if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
    $publishArgs += @("-r", $Runtime)
}

$publishArgs += @("--self-contained", $SelfContained.ToString().ToLowerInvariant())

if ($SingleFile) {
    $publishArgs += "/p:PublishSingleFile=true"
    $publishArgs += "/p:IncludeNativeLibrariesForSelfExtract=true"
}

dotnet @publishArgs

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Publish output: $Output" -ForegroundColor Green
if ($SelfContained -and $Runtime.StartsWith("win")) {
    Write-Host "  Run: $Output\\SupplierSystem.Api.exe" -ForegroundColor Green
} else {
    Write-Host "  Run: dotnet $Output\\SupplierSystem.Api.dll" -ForegroundColor Green
}
Write-Host "============================================" -ForegroundColor Cyan
