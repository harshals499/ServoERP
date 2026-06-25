param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$CandidateVersion,

    [string]$Token
)

$ErrorActionPreference = "Stop"

function Convert-ToComparableVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $text = $Value.Trim().TrimStart('v', 'V')
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Version '$Value' is empty."
    }

    $suffixIndex = $text.IndexOfAny([char[]]@('-', '+'))
    if ($suffixIndex -ge 0) {
        $text = $text.Substring(0, $suffixIndex)
    }

    $parts = $text.Split('.')
    if ($parts.Length -lt 2 -or $parts.Length -gt 4) {
        throw "Version '$Value' must have 2 to 4 numeric parts."
    }

    $numbers = @(0, 0, 0, 0)
    for ($i = 0; $i -lt $parts.Length; $i++) {
        $parsed = 0
        if (-not [int]::TryParse($parts[$i], [ref]$parsed) -or $parsed -lt 0) {
            throw "Version '$Value' contains a non-numeric part."
        }

        $numbers[$i] = $parsed
    }

    return [Version]::new($numbers[0], $numbers[1], $numbers[2], $numbers[3])
}

$candidateComparable = Convert-ToComparableVersion -Value $CandidateVersion
$latestReleaseApi = "https://api.github.com/repos/$Repository/releases/latest"
$headers = @{
    "User-Agent" = "ServoERP-Release-Guard"
    "Accept" = "application/vnd.github+json"
}

if (-not [string]::IsNullOrWhiteSpace($Token)) {
    $headers["Authorization"] = "Bearer $Token"
}

try {
    $latest = Invoke-RestMethod -Uri $latestReleaseApi -Headers $headers -Method Get
} catch {
    $message = $_.Exception.Message
    if ($message -match "404") {
        Write-Host "No published GitHub release exists yet. Candidate version $CandidateVersion is allowed."
        exit 0
    }

    throw
}

$latestTag = ""
if ($null -ne $latest -and $null -ne $latest.tag_name) {
    $latestTag = [string]$latest.tag_name
}
if ([string]::IsNullOrWhiteSpace($latestTag)) {
    throw "Latest GitHub release did not include tag_name."
}

$latestComparable = Convert-ToComparableVersion -Value $latestTag
if ($candidateComparable -le $latestComparable) {
    throw "Candidate version $CandidateVersion must be greater than latest published release $latestTag."
}

Write-Host "Release version check passed. Candidate $CandidateVersion is newer than published $latestTag."
