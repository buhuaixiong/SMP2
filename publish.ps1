# ============================================
# Supplier System - Build & Publish (SPA + API)
# ============================================

[CmdletBinding()]
param(
    [string]$Runtime = "",
    [string]$Configuration = "Release",
    [string]$Output = "",
    [bool]$SelfContained = $false,
    [switch]$SingleFile,
    [bool]$CleanWwwroot = $true,
    [bool]$CleanOutput = $true,
    [bool]$DisablePrecompression = $true
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
$buildCommand = "pushd `"$AppDir`" && npm run build -w apps/web"
cmd /d /s /c $buildCommand
if ($LASTEXITCODE -ne 0) {
    throw "Frontend build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $DistDir)) {
    throw "Frontend build output not found: $DistDir"
}

Write-Host "[2/3] Copying frontend to API wwwroot..." -ForegroundColor Yellow
if ($CleanWwwroot -and (Test-Path $Wwwroot)) {
    Write-Host "  Cleaning existing wwwroot..." -ForegroundColor DarkGray
    Get-ChildItem -Path $Wwwroot -Force | Remove-Item -Recurse -Force
}
New-Item -Path $Wwwroot -ItemType Directory -Force | Out-Null
Copy-Item -Path (Join-Path $DistDir "*") -Destination $Wwwroot -Recurse -Force

Write-Host "[3/3] Publishing .NET API..." -ForegroundColor Yellow
if ($CleanOutput -and (Test-Path $Output)) {
    Write-Host "  Cleaning existing publish output..." -ForegroundColor DarkGray
    Get-ChildItem -Path $Output -Force | Remove-Item -Recurse -Force
}
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

if (-not $SelfContained) {
    $publishArgs += "/p:UseAppHost=false"
}

if ($DisablePrecompression) {
    Write-Host "  Static asset precompression: disabled" -ForegroundColor DarkGray
    $publishArgs += "/p:CompressionEnabled=false"
} else {
    Write-Host "  Static asset precompression: enabled" -ForegroundColor DarkGray
}

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
