# ============================================
# Supplier System - Start Production (LAN)
# ============================================

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = $ScriptDir
$PublishDir = Join-Path $Root "artifacts\publish"
$UploadsSource = Join-Path $Root "SupplierSystem\src\SupplierSystem.Api\uploads"
$DefaultUploadsTarget = "\\apeafs01\DEPT\PUR\SMP\uploads"
$UploadsTarget = if ([string]::IsNullOrWhiteSpace($env:UPLOADS_PATH)) { $DefaultUploadsTarget } else { $env:UPLOADS_PATH.Trim() }
$env:UPLOADS_PATH = $UploadsTarget
$env:PR_EXCEL_TEMPLATE_PATH = Join-Path $Root "app\template\InquiryIndirectmaterialPR.xlsm"

function Ensure-DirectoryReady {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$RetryCount = 3,
        [int]$RetryDelaySeconds = 2
    )

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        try {
            New-Item -Path $Path -ItemType Directory -Force -ErrorAction Stop | Out-Null
            return
        } catch {
            if ($attempt -eq $RetryCount) {
                throw "Failed to prepare uploads directory '$Path'. Last error: $($_.Exception.Message). If this is a network share, verify share path and permissions (for example: net view \\apeafs01)."
            }

            Write-Host "Uploads path unavailable ($Path), retrying in $RetryDelaySeconds seconds ..." -ForegroundColor Yellow
            Start-Sleep -Seconds $RetryDelaySeconds
        }
    }
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish output not found: $PublishDir. Run .\\publish.ps1 first."
}

$migrationMarker = Join-Path $UploadsTarget ".migrated_from_local"
if (Test-Path $UploadsSource) {
    Ensure-DirectoryReady -Path $UploadsTarget
    if (-not (Test-Path $migrationMarker)) {
        Write-Host "Migrating uploads to $UploadsTarget ..." -ForegroundColor Yellow
        & robocopy $UploadsSource $UploadsTarget /E /COPY:DAT /R:1 /W:1 /XO /XN /XC | Out-Null
        $robocopyExit = $LASTEXITCODE
        if ($robocopyExit -ge 8) {
            throw "Robocopy failed with exit code $robocopyExit. Check share permissions."
        }
        Set-Content -Path $migrationMarker -Value ("migrated from " + $UploadsSource + " at " + (Get-Date).ToString("s"))
    } else {
        Write-Host "Uploads migration marker found; skipping copy." -ForegroundColor DarkGray
    }
}

$env:ASPNETCORE_ENVIRONMENT = "Production"
Push-Location $PublishDir

$exePath = Join-Path $PublishDir "SupplierSystem.Api.exe"
$dllPath = Join-Path $PublishDir "SupplierSystem.Api.dll"

try {
    if (Test-Path $exePath) {
        Write-Host "Starting API: $exePath" -ForegroundColor Green
        & $exePath
    } elseif (Test-Path $dllPath) {
        Write-Host "Starting API: dotnet $dllPath" -ForegroundColor Green
        dotnet $dllPath
    } else {
        throw "No runnable publish output found in: $PublishDir"
    }
} finally {
    Pop-Location
}
