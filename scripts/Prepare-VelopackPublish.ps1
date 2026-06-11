param(
    [Parameter(Mandatory = $true)]
    [string]$BuildOutput,

    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"

$source = Resolve-Path $BuildOutput
if (Test-Path -LiteralPath $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $PublishDir | Out-Null
Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $PublishDir -Recurse -Force
}

$clientMutableFiles = @(
    "HVACPro.config",
    "*.servoerp-license",
    "license-private.xml",
    "license-public.xml",
    "*.sqlite",
    "*.mdf",
    "*.ldf",
    "*.bak"
)

foreach ($pattern in $clientMutableFiles) {
    Get-ChildItem -Path $PublishDir -Filter $pattern -Recurse -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

$clientMutableDirs = @("DATABASE", "LOGS", "UPDATES", "CONFIG", "TEMP", "TEST_RESULTS", "PAYSLIPS", "RECEIPTS", "Invoice")
foreach ($dir in $clientMutableDirs) {
    Get-ChildItem -Path $PublishDir -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ieq $dir } |
        Remove-Item -Recurse -Force
}

$sampleConfig = Join-Path $PublishDir "HVACPro.sample.config"
Copy-Item -LiteralPath (Join-Path $source "HVACPro.config") -Destination $sampleConfig -Force -ErrorAction SilentlyContinue

"PUBLISH_DIR=$PublishDir"
