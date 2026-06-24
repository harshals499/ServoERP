param(
    [string]$Version = "1.1.199",
    [string]$Repository = "harshals499/ServoERP",
    [string]$ArtifactsDir,
    [string]$Token,
    [string]$Target = "main"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $ArtifactsDir = Join-Path $repoRoot "artifacts\velopack-local-$Version"
}

$artifactsRoot = Resolve-Path -LiteralPath $ArtifactsDir
$nodeScript = Join-Path $PSScriptRoot "Publish-GitHubReleaseNode.mjs"

$assetNames = @(
    "ServoERP.Desktop-win-Setup.exe",
    "ServoERP.Desktop-$Version-full.nupkg",
    "ServoERP.Desktop-win-Portable.zip",
    "RELEASES",
    "releases.win.json",
    "assets.win.json"
)

$assetPaths = New-Object System.Collections.Generic.List[string]
foreach ($assetName in $assetNames) {
    $assetPath = Join-Path $artifactsRoot $assetName
    if (Test-Path -LiteralPath $assetPath) {
        $assetPaths.Add((Resolve-Path -LiteralPath $assetPath).Path)
    }
}

if ($assetPaths.Count -lt 3) {
    throw "Expected Velopack assets were not found in $artifactsRoot."
}

$resolvedToken = $Token
if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    $resolvedToken = $env:GITHUB_TOKEN
}
if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    $resolvedToken = $env:GH_TOKEN
}
if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if ($gh) {
        $resolvedToken = (& gh auth token 2>$null)
    }
}
if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    throw "No GitHub token found. Sign in with GitHub Desktop/gh, or set GITHUB_TOKEN in this PowerShell window."
}

$body = @"
ServoERP client update $Version

- Restores Attendance as its own HR & Payroll sidebar page.
- Keeps Attendance available under existing HR/Payroll license entitlements.
"@

$env:GITHUB_TOKEN = $resolvedToken
node $nodeScript `
    --repo $Repository `
    --tag "v$Version" `
    --name "ServoERP $Version" `
    --target $Target `
    --body $body `
    --artifacts ($assetPaths -join ";")
