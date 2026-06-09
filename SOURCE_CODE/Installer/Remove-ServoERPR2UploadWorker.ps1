param(
    [string]$WorkerName = 'servoerp-r2-multipart-upload'
)

$ErrorActionPreference = 'Stop'

function Get-CloudflareToken {
    $token = [Environment]::GetEnvironmentVariable('CLOUDFLARE_API_TOKEN', 'Process')
    if ([string]::IsNullOrWhiteSpace($token)) {
        $token = [Environment]::GetEnvironmentVariable('CLOUDFLARE_API_TOKEN', 'User')
    }

    if ([string]::IsNullOrWhiteSpace($token)) {
        throw 'Missing CLOUDFLARE_API_TOKEN. Set the Cloudflare API token before removing the upload Worker.'
    }

    return $token.Trim()
}

$env:CLOUDFLARE_API_TOKEN = Get-CloudflareToken
Write-Host ("Removing Cloudflare R2 upload Worker {0}..." -f $WorkerName)
& wrangler delete $WorkerName --force
Write-Host 'Upload Worker removed.'
