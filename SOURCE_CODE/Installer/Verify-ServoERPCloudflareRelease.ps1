param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$BaseUrl = 'https://servoerp.in',

    [string]$DownloadHost = 'https://downloads.servoerp.in'
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
$zipName = "ServoERP_Update_$Version.zip"

Assert-TextEquals -Url "$BaseUrl/version.txt" -Expected $Version
Assert-JsonFieldEquals -Url "$BaseUrl/latest.json" -Field 'latestVersion' -Expected $Version
Assert-HttpOk -Url "$BaseUrl/updates/$zipName" | Out-Null
Assert-HttpOk -Url "$DownloadHost/$installerName" | Out-Null
Assert-HttpOk -Url "$BaseUrl/download/" | Out-Null

Write-Host "Cloudflare release verification passed for ServoERP $Version."
