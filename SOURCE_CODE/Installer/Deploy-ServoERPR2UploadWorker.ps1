param(
    [string]$WorkerName = 'servoerp-r2-multipart-upload',
    [string]$BucketName = $(if ($env:R2_BUCKET) { $env:R2_BUCKET } else { 'servoerp-downloads' }),
    [string]$AccountId = $(if ($env:R2_ACCOUNT_ID) { $env:R2_ACCOUNT_ID } else { 'ba80bcc2ebee2669dab5dbf15dc5f4da' })
)

$ErrorActionPreference = 'Stop'

function Get-CloudflareToken {
    $token = [Environment]::GetEnvironmentVariable('CLOUDFLARE_API_TOKEN', 'Process')
    if ([string]::IsNullOrWhiteSpace($token)) {
        $token = [Environment]::GetEnvironmentVariable('CLOUDFLARE_API_TOKEN', 'User')
    }

    if ([string]::IsNullOrWhiteSpace($token)) {
        throw 'Missing CLOUDFLARE_API_TOKEN. Set the Cloudflare API token before deploying the upload Worker.'
    }

    return $token.Trim()
}

$token = Get-CloudflareToken
$env:CLOUDFLARE_API_TOKEN = $token

$workerSource = Join-Path $PSScriptRoot 'CloudflareR2MultipartWorker.js'
if (-not (Test-Path -LiteralPath $workerSource)) {
    throw "Worker source not found: $workerSource"
}

$workDir = Join-Path $env:TEMP 'ServoERP_R2UploadWorker'
New-Item -ItemType Directory -Force -Path $workDir | Out-Null
Copy-Item -LiteralPath $workerSource -Destination (Join-Path $workDir 'index.js') -Force

$uploadToken = [Guid]::NewGuid().ToString('N')
$config = @"
{
  "name": "$WorkerName",
  "main": "index.js",
  "compatibility_date": "2024-09-23",
  "workers_dev": true,
  "account_id": "$AccountId",
  "r2_buckets": [
    {
      "binding": "UPLOAD_BUCKET",
      "bucket_name": "$BucketName"
    }
  ],
  "vars": {
    "UPLOAD_TOKEN": "$uploadToken"
  }
}
"@

$configPath = Join-Path $workDir 'wrangler.jsonc'
Set-Content -LiteralPath $configPath -Value $config -Encoding UTF8

Write-Host ("Deploying Cloudflare R2 upload Worker {0}..." -f $WorkerName)
$deployOutput = & wrangler deploy --config $configPath 2>&1
$deployText = ($deployOutput | Out-String)
$deployText = [regex]::Replace($deployText, '\x1B\[[0-9;]*[A-Za-z]', '')
Write-Host $deployText

$match = [regex]::Match($deployText, 'https://\S+')
if (-not $match.Success) {
    throw 'Worker deploy succeeded but no workers.dev URL was found in the output.'
}

[pscustomobject]@{
    WorkerName = $WorkerName
    WorkerUrl = $match.Value
    UploadToken = $uploadToken
    BucketName = $BucketName
    AccountId = $AccountId
}
