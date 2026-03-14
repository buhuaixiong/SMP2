param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$VitestArgs
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$webRoot = Split-Path -Parent $scriptRoot
$appsRoot = Split-Path -Parent $webRoot
$appRoot = Split-Path -Parent $appsRoot
$vitestPath = Join-Path $appRoot 'node_modules\vitest\vitest.mjs'

if (-not (Test-Path $vitestPath)) {
    throw "Vitest entry not found: $vitestPath"
}

$driveName = $null
$workingWebRoot = $webRoot
$workingVitestPath = $vitestPath

if ($webRoot.StartsWith('\\')) {
    $driveName = 'V'
    while (Get-PSDrive -Name $driveName -ErrorAction SilentlyContinue) {
        $driveName = [char]([int][char]$driveName + 1)
    }

    $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $appRoot -Scope Script
    $workingWebRoot = "$driveName`:\apps\web"
    $workingVitestPath = "$driveName`:\node_modules\vitest\vitest.mjs"
}

try {
    Set-Location $workingWebRoot
    & node $workingVitestPath @VitestArgs
    exit $LASTEXITCODE
}
finally {
    if ($driveName) {
        Remove-PSDrive -Name $driveName -Scope Script -ErrorAction SilentlyContinue
    }
}
