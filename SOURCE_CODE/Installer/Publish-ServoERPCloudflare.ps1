param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$PagesProjectName = 'royal-moon-64ef',

    [string]$PagesBranch = 'ServoERP',

    [string]$BucketName = $(if ($env:R2_BUCKET) { $env:R2_BUCKET } else { 'servoerp-downloads' }),

    [string]$AccountId = $(if ($env:R2_ACCOUNT_ID) { $env:R2_ACCOUNT_ID } else { 'ba80bcc2ebee2669dab5dbf15dc5f4da' }),

    [string]$EndpointUrl = $(if ($env:R2_ENDPOINT_URL) { $env:R2_ENDPOINT_URL } else { '' }),

    [switch]$SkipInstallerUpload,

    [switch]$SkipPagesDeploy,

    [switch]$ForceWorkerMultipart,

    [switch]$KeepUploadWorker,

    [switch]$SkipVerification
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$installerPath = Join-Path $repoRoot ("installer_output\ServoERP_Setup_{0}.exe" -f $Version)
$marketingRoot = Join-Path $repoRoot 'marketing_site'
$objectKey = Split-Path -Path $installerPath -Leaf

if (-not $SkipInstallerUpload) {
    & (Join-Path $PSScriptRoot 'Publish-ServoERPR2Installer.ps1') `
        -InstallerPath $installerPath `
        -ObjectKey $objectKey `
        -BucketName $BucketName `
        -AccountId $AccountId `
        -EndpointUrl $EndpointUrl `
        -ForceWorkerMultipart:$ForceWorkerMultipart `
        -KeepUploadWorker:$KeepUploadWorker
}

if (-not $SkipPagesDeploy) {
    if ([string]::IsNullOrWhiteSpace($env:CLOUDFLARE_API_TOKEN)) {
        throw 'Missing CLOUDFLARE_API_TOKEN. Set the Cloudflare API token before deploying Pages.'
    }

    Write-Host ("Deploying marketing site to Cloudflare Pages project {0} branch {1}..." -f $PagesProjectName, $PagesBranch)
    wrangler pages deploy $marketingRoot --project-name $PagesProjectName --branch $PagesBranch --commit-dirty=true
}

if (-not $SkipVerification) {
    & (Join-Path $PSScriptRoot 'Verify-ServoERPCloudflareRelease.ps1') -Version $Version
}

Write-Host "Cloudflare publish flow complete."
