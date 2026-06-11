param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [string]$ObjectKey,

    [string]$BucketName = $(if ($env:R2_BUCKET) { $env:R2_BUCKET } else { 'servoerp-downloads' }),

    [string]$AccountId = $(if ($env:R2_ACCOUNT_ID) { $env:R2_ACCOUNT_ID } else { 'ba80bcc2ebee2669dab5dbf15dc5f4da' }),

    [string]$EndpointUrl = $(if ($env:R2_ENDPOINT_URL) { $env:R2_ENDPOINT_URL } else { '' }),

    [switch]$ForceWorkerMultipart,

    [switch]$KeepUploadWorker
)

$ErrorActionPreference = 'Stop'

$DIRECT_UPLOAD_LIMIT_BYTES = 300MB
$MULTIPART_CHUNK_SIZE_BYTES = 10MB

function Get-AwsCliCommand {
    $awsCommand = Get-Command aws.exe -ErrorAction SilentlyContinue
    if ($awsCommand) {
        return @{
            FilePath = $awsCommand.Source
            Prefix = @()
        }
    }

    $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
    if ($pythonCommand) {
        try {
            & $pythonCommand.Source -m awscli --version | Out-Null
            return @{
                FilePath = $pythonCommand.Source
                Prefix = @('-m', 'awscli')
            }
        }
        catch {
        }
    }

    throw 'AWS CLI was not found. Install it with "python -m pip install --user awscli" or add aws.exe to PATH.'
}

function Get-RequiredEnvironmentValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = [Environment]::GetEnvironmentVariable($Name, 'User')
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Missing required environment variable: $Name"
    }

    return $value.Trim()
}

function Get-OptionalEnvironmentValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = [Environment]::GetEnvironmentVariable($Name, 'User')
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value.Trim()
}

function Upload-ViaAwsCli {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$TargetBucket,
        [Parameter(Mandatory = $true)][string]$TargetEndpoint
    )

    $accessKey = Get-RequiredEnvironmentValue -Name 'R2_ACCESS_KEY_ID'
    $secretKey = Get-RequiredEnvironmentValue -Name 'R2_SECRET_ACCESS_KEY'
    $aws = Get-AwsCliCommand
    $previousAccessKey = $env:AWS_ACCESS_KEY_ID
    $previousSecretKey = $env:AWS_SECRET_ACCESS_KEY
    $previousRegion = $env:AWS_DEFAULT_REGION

    $env:AWS_ACCESS_KEY_ID = $accessKey
    $env:AWS_SECRET_ACCESS_KEY = $secretKey
    $env:AWS_DEFAULT_REGION = 'auto'

    $awsArgs = @()
    $awsArgs += $aws.Prefix
    $awsArgs += @(
        's3',
        'cp',
        $Path,
        ("s3://{0}/{1}" -f $TargetBucket, $Key),
        '--endpoint-url',
        $TargetEndpoint,
        '--only-show-errors',
        '--content-type',
        'application/vnd.microsoft.portable-executable'
    )

    Write-Host ("Uploading installer to R2 bucket {0} as {1} via S3-compatible API..." -f $TargetBucket, $Key)
    try {
        & $aws.FilePath @awsArgs
    }
    finally {
        $env:AWS_ACCESS_KEY_ID = $previousAccessKey
        $env:AWS_SECRET_ACCESS_KEY = $previousSecretKey
        $env:AWS_DEFAULT_REGION = $previousRegion
    }
}

function Upload-ViaWorkerMultipart {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$TargetBucket,
        [Parameter(Mandatory = $true)][string]$TargetAccountId
    )

    $deployment = & (Join-Path $PSScriptRoot 'Deploy-ServoERPR2UploadWorker.ps1') -BucketName $TargetBucket -AccountId $TargetAccountId
    $workerName = $deployment.WorkerName
    $workerUrl = $deployment.WorkerUrl.TrimEnd('/')
    $uploadToken = $deployment.UploadToken
    $headers = @{ 'x-upload-token' = $uploadToken }
    $encodedKey = @(($Key -split '/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
    $uploadId = $null
    $parts = @()
    $stream = $null

    try {
        Write-Host ("Uploading installer via multipart Worker {0}..." -f $workerUrl)
        $create = Invoke-RestMethod -Method Post -Uri ("{0}/{1}?action=mpu-create" -f $workerUrl, $encodedKey) -Headers $headers
        $uploadId = $create.uploadId
        if ([string]::IsNullOrWhiteSpace($uploadId)) {
            throw 'Multipart upload did not return an uploadId.'
        }

        $stream = [System.IO.File]::OpenRead($Path)
        $totalParts = [int][Math]::Ceiling($stream.Length / [double]$MULTIPART_CHUNK_SIZE_BYTES)
        $chunkPath = Join-Path ([System.IO.Path]::GetTempPath()) ("servoerp-r2-part-{0}.bin" -f ([Guid]::NewGuid().ToString('N')))
        for ($index = 0; $index -lt $totalParts; $index++) {
            $remaining = $stream.Length - $stream.Position
            $bytesToRead = [Math]::Min([int64]$MULTIPART_CHUNK_SIZE_BYTES, $remaining)
            $buffer = New-Object byte[] $bytesToRead
            $read = $stream.Read($buffer, 0, $buffer.Length)
            if ($read -ne $buffer.Length) {
                throw "Failed to read chunk $($index + 1) from installer."
            }

            [System.IO.File]::WriteAllBytes($chunkPath, $buffer)
            $partNumber = $index + 1
            $part = Invoke-RestMethod `
                -Method Put `
                -Uri ("{0}/{1}?action=mpu-uploadpart&uploadId={2}&partNumber={3}" -f $workerUrl, $encodedKey, [Uri]::EscapeDataString($uploadId), $partNumber) `
                -Headers $headers `
                -InFile $chunkPath `
                -ContentType 'application/octet-stream'

            $parts += [pscustomobject]@{
                    partNumber = $part.partNumber
                    etag = $part.etag
                }

            Write-Host ("Uploaded part {0}/{1}" -f $partNumber, $totalParts)
        }

        $body = @{ parts = @($parts) } | ConvertTo-Json -Depth 6
        $complete = Invoke-RestMethod `
            -Method Post `
            -Uri ("{0}/{1}?action=mpu-complete&uploadId={2}" -f $workerUrl, $encodedKey, [Uri]::EscapeDataString($uploadId)) `
            -Headers $headers `
            -Body $body `
            -ContentType 'application/json'

        if (-not $complete.etag) {
            throw 'Multipart upload complete response did not include an etag.'
        }

        Write-Host ("Multipart upload complete. ETag: {0}" -f $complete.etag)
    }
    catch {
        if ($uploadId) {
            try {
                Invoke-RestMethod -Method Post -Uri ("{0}/{1}?action=mpu-abort&uploadId={2}" -f $workerUrl, $encodedKey, [Uri]::EscapeDataString($uploadId)) -Headers $headers | Out-Null
            }
            catch {
            }
        }

        throw
    }
    finally {
        if ($stream) {
            $stream.Dispose()
        }

        if ($chunkPath -and (Test-Path -LiteralPath $chunkPath)) {
            Remove-Item -LiteralPath $chunkPath -Force
        }

        if (-not $KeepUploadWorker) {
            try {
                & (Join-Path $PSScriptRoot 'Remove-ServoERPR2UploadWorker.ps1') -WorkerName $workerName | Out-Null
            }
            catch {
                Write-Warning ("Upload Worker cleanup failed: {0}" -f $_.Exception.Message)
            }
        }
    }
}

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "Installer not found: $InstallerPath"
}

if ([string]::IsNullOrWhiteSpace($ObjectKey)) {
    $ObjectKey = Split-Path -Path $InstallerPath -Leaf
}

if ([string]::IsNullOrWhiteSpace($EndpointUrl)) {
    if ([string]::IsNullOrWhiteSpace($AccountId)) {
        throw 'R2 endpoint is not configured. Set R2_ENDPOINT_URL or R2_ACCOUNT_ID.'
    }

    $EndpointUrl = "https://$AccountId.r2.cloudflarestorage.com"
}

$installerFile = Get-Item -LiteralPath $InstallerPath
$hasStaticKeys = -not [string]::IsNullOrWhiteSpace((Get-OptionalEnvironmentValue -Name 'R2_ACCESS_KEY_ID')) -and
    -not [string]::IsNullOrWhiteSpace((Get-OptionalEnvironmentValue -Name 'R2_SECRET_ACCESS_KEY'))

if (-not $ForceWorkerMultipart -and $hasStaticKeys) {
    Upload-ViaAwsCli -Path $InstallerPath -Key $ObjectKey -TargetBucket $BucketName -TargetEndpoint $EndpointUrl
}
elseif ($installerFile.Length -le $DIRECT_UPLOAD_LIMIT_BYTES -and -not $ForceWorkerMultipart) {
    Write-Host ("Static R2 keys are missing. Falling back to Cloudflare API direct upload for {0} bytes." -f $installerFile.Length)
    $cloudflareToken = Get-RequiredEnvironmentValue -Name 'CLOUDFLARE_API_TOKEN'
    $headers = @{ Authorization = "Bearer $cloudflareToken" }
    Invoke-RestMethod `
        -Method Put `
        -Uri ("https://api.cloudflare.com/client/v4/accounts/{0}/r2/buckets/{1}/objects/{2}" -f $AccountId, $BucketName, $ObjectKey) `
        -Headers $headers `
        -InFile $InstallerPath `
        -ContentType 'application/vnd.microsoft.portable-executable' | Out-Null
}
else {
    Upload-ViaWorkerMultipart -Path $InstallerPath -Key $ObjectKey -TargetBucket $BucketName -TargetAccountId $AccountId
}

$publicUrl = "https://downloads.servoerp.in/$ObjectKey"
Write-Host "R2 upload complete."
Write-Host "Expected public URL: $publicUrl"
