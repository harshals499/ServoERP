param(
    [Parameter(Mandatory = $true)]
    [string]$BuildOutput,

    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"

$source = Resolve-Path $BuildOutput
$releaseVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $source 'HVAC_Pro_Desktop.exe')).Version
$releaseVersion = '{0}.{1}.{2}' -f $releaseVersion.Major,$releaseVersion.Minor,$releaseVersion.Build
& (Join-Path $PSScriptRoot 'Assert-ServoReleaseNotes.ps1') -Version $releaseVersion -ForPackaging

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
Get-ChildItem -LiteralPath $PublishDir -Filter 'official_invoice_header.png' -Recurse -File |
    Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ClientClean.config") -Destination $sampleConfig -Force

"PUBLISH_DIR=$PublishDir"

Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) "release\RELEASE_NOTES_$releaseVersion.md") -Destination $PublishDir -Force
