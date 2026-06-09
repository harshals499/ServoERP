param(
    [string]$CloudflareApiToken,
    [string]$R2AccessKeyId,
    [string]$R2SecretAccessKey,
    [string]$R2AccountId = 'ba80bcc2ebee2669dab5dbf15dc5f4da',
    [string]$R2Bucket = 'servoerp-downloads',
    [string]$R2EndpointUrl = 'https://ba80bcc2ebee2669dab5dbf15dc5f4da.r2.cloudflarestorage.com'
)

$ErrorActionPreference = 'Stop'

function Set-UserEnvironmentValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    [Environment]::SetEnvironmentVariable($Name, $Value.Trim(), 'User')
    Set-Item -Path ("env:{0}" -f $Name) -Value $Value.Trim()
    Write-Host "Saved $Name to user environment."
}

Set-UserEnvironmentValue -Name 'CLOUDFLARE_API_TOKEN' -Value $CloudflareApiToken
Set-UserEnvironmentValue -Name 'R2_ACCESS_KEY_ID' -Value $R2AccessKeyId
Set-UserEnvironmentValue -Name 'R2_SECRET_ACCESS_KEY' -Value $R2SecretAccessKey
Set-UserEnvironmentValue -Name 'R2_ACCOUNT_ID' -Value $R2AccountId
Set-UserEnvironmentValue -Name 'R2_BUCKET' -Value $R2Bucket
Set-UserEnvironmentValue -Name 'R2_ENDPOINT_URL' -Value $R2EndpointUrl

$awsScriptsPath = 'C:\Users\Administrator\AppData\Roaming\Python\Python313\Scripts'
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$pathParts = @($userPath -split ';' | Where-Object { $_ })
if ($pathParts -notcontains $awsScriptsPath) {
    $pathParts += $awsScriptsPath
    [Environment]::SetEnvironmentVariable('Path', ($pathParts -join ';'), 'User')
    $env:Path += ';' + $awsScriptsPath
    Write-Host 'Added AWS CLI scripts folder to user PATH.'
}

Write-Host 'ServoERP Cloudflare environment configuration saved.'
Write-Host 'If R2 access keys are omitted, large installers will use the Cloudflare multipart Worker fallback.'
