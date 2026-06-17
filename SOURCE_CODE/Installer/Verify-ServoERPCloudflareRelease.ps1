param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$BaseUrl = 'https://servoerp.in',

    [string]$DownloadHost = 'https://downloads.servoerp.in',

    [int]$MaxAttempts = 1,

    [int]$DelaySeconds = 0
)

$ErrorActionPreference = 'Stop'

function Assert-HttpOk {
    param(
        [Parameter(Mandatory = $true)][string]$Url
    )

    $response = Invoke-WebRequest -Uri $Url -Method Head -UseBasicParsing
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "Expected 2xx from $Url but received $($response.StatusCode)."
    }

    return $response
}

function Assert-TextEquals {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$Expected
    )

    $content = (Invoke-WebRequest -Uri $Url -UseBasicParsing).Content.Trim()
    if ($content -ne $Expected) {
        throw "Expected $Url to be '$Expected' but found '$content'."
    }
}

function Assert-JsonFieldEquals {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$Field,
        [Parameter(Mandatory = $true)][string]$Expected
    )

    $content = (Invoke-WebRequest -Uri $Url -UseBasicParsing).Content
    $content = [regex]::Replace($content, '^\uFEFF', '')
    $content = [regex]::Replace($content, '^[\u00EF\u00BB\u00BF]+', '')
    $json = $content | ConvertFrom-Json
    $actual = [string]$json.$Field
    if ($actual -ne $Expected) {
        throw "Expected $Field in $Url to be '$Expected' but found '$actual'."
    }
}

$installerName = "ServoERP_Setup_$Version.exe"
$latestUrl = "$BaseUrl/latest.json"
$latestContent = (Invoke-WebRequest -Uri $latestUrl -UseBasicParsing).Content
$latestContent = [regex]::Replace($latestContent, '^\uFEFF', '')
$latestContent = [regex]::Replace($latestContent, '^[\u00EF\u00BB\u00BF]+', '')
$latestJson = $latestContent | ConvertFrom-Json
$packageUrl = [string]$latestJson.packageUrl
$installerUrl = [string]$latestJson.installerUrl
if ([string]::IsNullOrWhiteSpace($installerUrl)) {
    $installerUrl = "$DownloadHost/$installerName"
}

$attempts = [Math]::Max(1, $MaxAttempts)
$delay = [Math]::Max(0, $DelaySeconds)
$lastError = $null

for ($attempt = 1; $attempt -le $attempts; $attempt++) {
    try {
        Assert-TextEquals -Url "$BaseUrl/version.txt" -Expected $Version
        Assert-JsonFieldEquals -Url $latestUrl -Field 'latestVersion' -Expected $Version
        if (-not [string]::IsNullOrWhiteSpace($packageUrl)) {
            Assert-HttpOk -Url $packageUrl | Out-Null
        }
        Assert-HttpOk -Url $installerUrl | Out-Null
        Assert-HttpOk -Url "$BaseUrl/download/" | Out-Null
        $lastError = $null
        break
    }
    catch {
        $lastError = $_
        if ($attempt -lt $attempts) {
            Start-Sleep -Seconds $delay
        }
    }
}

if ($lastError) {
    throw $lastError.Exception
}

Write-Host "Cloudflare release verification passed for ServoERP $Version."
