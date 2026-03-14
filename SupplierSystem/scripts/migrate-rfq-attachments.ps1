# ============================================
# Migrate RFQ attachments into per-RFQ folders
# ============================================

param(
    [string]$UploadsRoot,
    [string]$ConnectionString,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SupplierRoot = Resolve-Path (Join-Path $ScriptDir "..")

if (-not $UploadsRoot) {
    $UploadsRoot = Join-Path $SupplierRoot "src\SupplierSystem.Api\uploads\rfq-attachments"
}

if (-not (Test-Path $UploadsRoot)) {
    throw "Uploads root not found: $UploadsRoot"
}

if (-not $ConnectionString) {
    $settingsPath = Join-Path $SupplierRoot "src\SupplierSystem.Api\appsettings.json"
    if (-not (Test-Path $settingsPath)) {
        throw "appsettings.json not found at $settingsPath. Provide -ConnectionString."
    }
    $settings = Get-Content -Raw -Path $settingsPath | ConvertFrom-Json
    $ConnectionString = $settings.ConnectionStrings.SupplierSystem
}

if (-not $ConnectionString) {
    throw "Connection string is empty. Provide -ConnectionString."
}

Add-Type -AssemblyName System.Data

$connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$connection.Open()

$movedQuote = 0
$missingQuote = 0
$skippedQuote = 0

$movedPrice = 0
$missingPrice = 0
$updatedPrice = 0
$skippedPrice = 0

function Move-FileIfNeeded {
    param(
        [string]$SourcePath,
        [string]$DestPath
    )

    if (-not $SourcePath -or -not (Test-Path $SourcePath)) {
        return $false
    }

    $destDir = Split-Path -Parent $DestPath
    if (-not (Test-Path $destDir)) {
        if (-not $DryRun) {
            New-Item -ItemType Directory -Path $destDir | Out-Null
        }
    }

    if (Test-Path $DestPath) {
        return $true
    }

    if (-not $DryRun) {
        Move-Item -Path $SourcePath -Destination $DestPath
    }

    return $true
}

try {
    # Quote attachments
    $quoteCmd = $connection.CreateCommand()
    $quoteCmd.CommandText = @"
SELECT qa.stored_name AS stored_name, q.rfq_id AS rfq_id
FROM quote_attachments qa
JOIN quotes q ON q.id = qa.quote_id
"@

    $reader = $quoteCmd.ExecuteReader()
    while ($reader.Read()) {
        $storedName = [string]$reader["stored_name"]
        $rfqId = [int]$reader["rfq_id"]

        if (-not $storedName -or $rfqId -le 0) {
            $skippedQuote++
            continue
        }

        $sourcePath = Join-Path $UploadsRoot $storedName
        $destPath = Join-Path (Join-Path $UploadsRoot $rfqId) $storedName

        if (Test-Path $destPath) {
            $skippedQuote++
            continue
        }

        if (Move-FileIfNeeded -SourcePath $sourcePath -DestPath $destPath) {
            $movedQuote++
        }
        else {
            $missingQuote++
        }
    }
    $reader.Close()

    # Price comparison attachments
    $priceCmd = $connection.CreateCommand()
    $priceCmd.CommandText = @"
SELECT id, rfq_id, stored_file_name, file_path
FROM price_comparison_attachments
"@

    $updateCmd = $connection.CreateCommand()
    $updateCmd.CommandText = "UPDATE price_comparison_attachments SET file_path = @path WHERE id = @id"
    $null = $updateCmd.Parameters.Add("@path", [System.Data.SqlDbType]::NVarChar, 1024)
    $null = $updateCmd.Parameters.Add("@id", [System.Data.SqlDbType]::Int)

    $reader = $priceCmd.ExecuteReader()
    while ($reader.Read()) {
        $id = [int]$reader["id"]
        $rfqId = [int]$reader["rfq_id"]
        $storedName = $reader["stored_file_name"]
        $filePath = $reader["file_path"]

        $storedName = if ($storedName -ne $null -and [string]$storedName) {
            [string]$storedName
        } elseif ($filePath -ne $null -and [string]$filePath) {
            Split-Path -Leaf ([string]$filePath)
        } else {
            ""
        }

        if (-not $storedName -or $rfqId -le 0) {
            $skippedPrice++
            continue
        }

        $destPath = Join-Path (Join-Path $UploadsRoot $rfqId) $storedName

        $sourcePath = $null
        if ($filePath -ne $null -and [string]$filePath) {
            $candidate = [string]$filePath
            if (Test-Path $candidate) {
                $sourcePath = $candidate
            }
        }
        if (-not $sourcePath) {
            $candidate = Join-Path $UploadsRoot $storedName
            if (Test-Path $candidate) {
                $sourcePath = $candidate
            }
        }

        if (Test-Path $destPath) {
            $skippedPrice++
        }
        elseif (Move-FileIfNeeded -SourcePath $sourcePath -DestPath $destPath) {
            $movedPrice++
        }
        else {
            $missingPrice++
        }

        $newPath = $destPath
        if (-not $DryRun -and $newPath -and $newPath -ne $filePath) {
            $updateCmd.Parameters["@path"].Value = $newPath
            $updateCmd.Parameters["@id"].Value = $id
            $null = $updateCmd.ExecuteNonQuery()
            $updatedPrice++
        } elseif ($DryRun -and $newPath -and $newPath -ne $filePath) {
            $updatedPrice++
        }
    }
    $reader.Close()
}
finally {
    $connection.Close()
}

Write-Host "Quote attachments moved: $movedQuote" -ForegroundColor Green
Write-Host "Quote attachments missing: $missingQuote" -ForegroundColor Yellow
Write-Host "Quote attachments skipped: $skippedQuote" -ForegroundColor Gray
Write-Host "Price comparison moved: $movedPrice" -ForegroundColor Green
Write-Host "Price comparison missing: $missingPrice" -ForegroundColor Yellow
Write-Host "Price comparison updated path: $updatedPrice" -ForegroundColor Green
Write-Host "Price comparison skipped: $skippedPrice" -ForegroundColor Gray

if ($DryRun) {
    Write-Host "DryRun enabled. No files or database rows were modified." -ForegroundColor Cyan
}
