param(
    [Parameter(Mandatory = $true)]
    [string]$TenantCode,

    [Parameter(Mandatory = $true)]
    [string]$CompanyName,

    [string]$Server = ".\SQLEXPRESS",
    [string]$DatabaseName,
    [string]$InstallRoot = "C:\HVAC_PRO_MSE",
    [switch]$UseSqlAuth,
    [string]$Username = "",
    [string]$Password = "",
    [switch]$SeedDemoData
)

$ErrorActionPreference = "Stop"

function Convert-ToSafeTenantCode {
    param([string]$Value)

    $safe = ($Value.Trim().ToUpperInvariant() -replace '[^A-Z0-9_]', '_') -replace '_+', '_'
    $safe = $safe.Trim('_')
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "TenantCode must contain at least one letter or number."
    }
    if ($safe.Length -gt 40) {
        $safe = $safe.Substring(0, 40)
    }
    return $safe
}

$safeTenant = Convert-ToSafeTenantCode $TenantCode
if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
    $DatabaseName = "HVAC_PRO_$safeTenant"
}

$configPath = Join-Path $InstallRoot "HVACPro.config"
if (!(Test-Path $configPath)) {
    $sourceConfig = Join-Path $InstallRoot "SOURCE_CODE\HVACPro.config"
    if (Test-Path $sourceConfig) {
        Copy-Item -LiteralPath $sourceConfig -Destination $configPath -Force
    } else {
        New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
        '<HVACProConfig />' | Set-Content -LiteralPath $configPath -Encoding UTF8
    }
}

[xml]$doc = Get-Content -LiteralPath $configPath
if ($null -eq $doc.HVACProConfig) {
    $doc.LoadXml("<HVACProConfig />")
}

function Set-ConfigValue {
    param(
        [xml]$Document,
        [string]$Section,
        [string]$Key,
        [string]$Value
    )

    $root = $Document.HVACProConfig
    $sectionNode = $root.SelectSingleNode($Section)
    if ($null -eq $sectionNode) {
        $sectionNode = $Document.CreateElement($Section)
        [void]$root.AppendChild($sectionNode)
    }

    $keyNode = $sectionNode.SelectSingleNode($Key)
    if ($null -eq $keyNode) {
        $keyNode = $Document.CreateElement($Key)
        [void]$sectionNode.AppendChild($keyNode)
    }

    $keyNode.InnerText = $Value
}

Set-ConfigValue $doc "Database" "Server" $Server
Set-ConfigValue $doc "Database" "DatabaseName" $DatabaseName
Set-ConfigValue $doc "Database" "UseWindowsAuth" ($(if ($UseSqlAuth) { "false" } else { "true" }))
Set-ConfigValue $doc "Database" "Username" ($(if ($UseSqlAuth) { $Username } else { "" }))
Set-ConfigValue $doc "Database" "Password" ($(if ($UseSqlAuth) { $Password } else { "" }))
Set-ConfigValue $doc "Tenant" "TenantCode" $safeTenant
Set-ConfigValue $doc "Tenant" "SeedDemoData" ($(if ($SeedDemoData) { "true" } else { "false" }))
Set-ConfigValue $doc "Company" "CompanyName" $CompanyName

$doc.Save($configPath)

$exePath = Join-Path $InstallRoot "HVAC_Pro_Desktop.exe"
if (Test-Path $exePath) {
    & $exePath /firstrun
}

Write-Host "Tenant provisioned."
Write-Host "TenantCode: $safeTenant"
Write-Host "Database: $DatabaseName"
Write-Host "Config: $configPath"
