param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw $Message
    }
}

$resolvedOutputDir = (Resolve-Path -LiteralPath $OutputDir).Path
$releasesPath = Join-Path $resolvedOutputDir "RELEASES"
Assert-PathExists -Path $releasesPath -Message "Velopack RELEASES file was not generated."

$setupCandidates = @(
    (Join-Path $resolvedOutputDir "ServoERP.Desktop-win-Setup.exe"),
    (Join-Path $resolvedOutputDir "ServoERP-win-Setup.exe")
)
$setupPath = $setupCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $setupPath) {
    throw "Velopack setup executable was not generated."
}

$releaseLines = Get-Content -LiteralPath $releasesPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
if (-not $releaseLines) {
    throw "Velopack RELEASES file is empty."
}

$matchingLine = $releaseLines | Where-Object { $_ -match [regex]::Escape("-$ExpectedVersion-") } | Select-Object -First 1
if (-not $matchingLine) {
    throw "Velopack RELEASES file does not contain expected version $ExpectedVersion."
}

$fullPackage = Get-ChildItem -LiteralPath $resolvedOutputDir -Filter "*.nupkg" -File |
    Where-Object { $_.Name -match [regex]::Escape("-$ExpectedVersion-") -and $_.Name -notmatch "-delta\.nupkg$" } |
    Select-Object -First 1
if (-not $fullPackage) {
    throw "Velopack full package for version $ExpectedVersion was not generated."
}

$releaseVersionCount = @(
    $releaseLines |
    ForEach-Object {
        if ($_ -match '-(?<version>\d+\.\d+\.\d+)-') {
            $matches["version"]
        }
    } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Sort-Object -Unique
).Count

if ($releaseVersionCount -lt 1) {
    throw "Velopack RELEASES file did not contain any parseable versions."
}

Write-Host "Velopack artifact check passed."
Write-Host "Setup: $setupPath"
Write-Host "Full package: $($fullPackage.Name)"
Write-Host "Release versions in feed: $releaseVersionCount"
