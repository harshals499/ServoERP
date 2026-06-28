param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$CandidateVersion,

    [string]$Token,

    [string]$CurrentCommit,

    [string]$GitHubEnvPath,

    [switch]$AllowAlreadyPublishedCurrentCommit
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

function Set-ReleaseShouldPublish {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($GitHubEnvPath)) {
        $flag = if ($Value) { "true" } else { "false" }
        Add-Content -LiteralPath $GitHubEnvPath -Value "RELEASE_SHOULD_PUBLISH=$flag"
    }
}

function Resolve-GitCommit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Revision
    )

    if ([string]::IsNullOrWhiteSpace($Revision)) {
        return ""
    }

    try {
        $resolved = & git rev-parse "$Revision^{commit}" 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($resolved)) {
            return ([string]$resolved).Trim()
        }
    } catch {
        return ""
    }

    return ""
}

function Test-SameGitCommit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Left,

        [Parameter(Mandatory = $true)]
        [string]$Right
    )

    $leftCommit = Resolve-GitCommit -Revision $Left
    $rightCommit = Resolve-GitCommit -Revision $Right

    return (
        -not [string]::IsNullOrWhiteSpace($leftCommit) -and
        -not [string]::IsNullOrWhiteSpace($rightCommit) -and
        $leftCommit.Equals($rightCommit, [System.StringComparison]::OrdinalIgnoreCase)
    )
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
        Set-ReleaseShouldPublish -Value $true
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
    $latestTarget = ""
    if ($null -ne $latest -and $null -ne $latest.target_commitish) {
        $latestTarget = [string]$latest.target_commitish
    }

    $isAlreadyPublishedCurrentCommit = $false
    if ($AllowAlreadyPublishedCurrentCommit -and $candidateComparable -eq $latestComparable -and -not [string]::IsNullOrWhiteSpace($CurrentCommit)) {
        $isAlreadyPublishedCurrentCommit = (
            (Test-SameGitCommit -Left $latestTag -Right $CurrentCommit) -or
            (-not [string]::IsNullOrWhiteSpace($latestTarget) -and (Test-SameGitCommit -Left $latestTarget -Right $CurrentCommit))
        )
    }

    if ($isAlreadyPublishedCurrentCommit) {
        Set-ReleaseShouldPublish -Value $false
        Write-Host "Release $latestTag is already published for the current commit. Skipping duplicate package and release publish."
        exit 0
    }

    Set-ReleaseShouldPublish -Value $false
    throw "Candidate version $CandidateVersion must be greater than latest published release $latestTag. If this commit needs to ship, bump VERSION before pushing."
}

Set-ReleaseShouldPublish -Value $true
Write-Host "Release version check passed. Candidate $CandidateVersion is newer than published $latestTag."
