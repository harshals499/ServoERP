param(
    [string]$Version,
    [string]$Repository = "harshals499/ServoERP-Releases",
    [string]$ArtifactsDir,
    [string]$Token,
    [string]$Target = "main"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim() }
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
    "assets.win.json",
    "RELEASE_NOTES_$Version.md"
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
        try {
            $ghTokenOutput = (& gh auth token 2>$null)
            if ($LASTEXITCODE -eq 0) {
                $resolvedToken = $ghTokenOutput
            }
        }
        catch {
            $resolvedToken = ""
        }
    }
}
if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    throw "No GitHub token found. Sign in with GitHub Desktop/gh, or set GITHUB_TOKEN in this PowerShell window."
}

$notesPath = Join-Path $repoRoot "release\RELEASE_NOTES_$Version.md"
if (-not (Test-Path -LiteralPath $notesPath)) { throw "Release notes are required: $notesPath" }
& (Join-Path $PSScriptRoot 'Assert-ServoReleaseNotes.ps1') -Version $Version -NotesPath $notesPath -ForPackaging
& (Join-Path $PSScriptRoot 'Test-VelopackReleaseArtifacts.ps1') -OutputDir $artifactsRoot -ExpectedVersion $Version
$body = Get-Content -LiteralPath $notesPath -Raw

$env:GITHUB_TOKEN = $resolvedToken
node $nodeScript `
    --repo $Repository `
    --tag "v$Version" `
    --name "ServoERP $Version" `
    --target $Target `
    --body $body `
    --artifacts ($assetPaths -join ";")
