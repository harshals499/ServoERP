param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Title,

    [string]$DownloadUrl,

    [string[]]$Changes = @(),

    [switch]$NoBuild,

    [switch]$SkipPrerequisiteDownload,

    [switch]$ForceCloseRunningApp
)

$ErrorActionPreference = 'Stop'

function Set-TextFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Value
    )

    Set-Content -LiteralPath $Path -Value $Value -Encoding UTF8
}

function Update-RegexFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Replacement
    )

    $text = Get-Content -LiteralPath $Path -Raw
    $updated = [regex]::Replace($text, $Pattern, $Replacement)
    if ($updated -eq $text) {
        throw "No matching text was changed in $Path"
    }

    Set-Content -LiteralPath $Path -Value $updated -Encoding UTF8
}

function Update-DownloadPage {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Url
    )

    $text = Get-Content -LiteralPath $Path -Raw
    $text = [regex]::Replace($text, 'content="2; url=[^"]+"', 'content="2; url=' + $Url + '"')
    $text = [regex]::Replace($text, '<a href="[^"]+">\s*Download installer\s*</a>', '<a href="' + $Url + '">Download installer</a>')
    Set-Content -LiteralPath $Path -Value $text -Encoding UTF8
}

function Update-MarketingScript {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Url
    )

    $text = Get-Content -LiteralPath $Path -Raw
    $text = [regex]::Replace(
        $text,
        'const DOWNLOAD_URL =\s*"[^"]+";',
        'const DOWNLOAD_URL =' + [Environment]::NewLine + '  "' + $Url + '";')
    Set-Content -LiteralPath $Path -Value $text -Encoding UTF8
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$sourceRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$marketingRoot = Join-Path $repoRoot 'marketing_site'
$assemblyInfo = Join-Path $sourceRoot 'Properties\AssemblyInfo.cs'
$appConfig = Join-Path $sourceRoot 'HVACPro.config'
$versionTxt = Join-Path $marketingRoot 'version.txt'
$changelogPath = Join-Path $marketingRoot 'changelog.json'
$downloadPage = Join-Path $marketingRoot 'download\index.html'
$marketingScript = Join-Path $marketingRoot 'script.js'
$installerOutput = Join-Path $repoRoot 'installer_output'
$marketingZip = Join-Path $repoRoot ("marketing_site_deploy_{0}.zip" -f $Version)

$parsedVersion = $null
if (-not [Version]::TryParse($Version, [ref]$parsedVersion)) {
    throw "Version must be numeric, for example 1.0.13.0"
}

Write-Host "Preparing ServoERP release $Version"

Update-RegexFile -Path $assemblyInfo -Pattern 'AssemblyVersion\("[^"]+"\)' -Replacement ('AssemblyVersion("' + $Version + '")')
Update-RegexFile -Path $assemblyInfo -Pattern 'AssemblyFileVersion\("[^"]+"\)' -Replacement ('AssemblyFileVersion("' + $Version + '")')
Update-RegexFile -Path $appConfig -Pattern '<Version>[^<]+</Version>' -Replacement ('<Version>' + $Version + '</Version>')
Set-TextFile -Path $versionTxt -Value $Version

$changelog = Get-Content -LiteralPath $changelogPath -Raw | ConvertFrom-Json
$entryChanges = @(
    [pscustomobject]@{
        type = 'release'
        items = @($Changes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
)

if ($entryChanges[0].items.Count -eq 0) {
    $entryChanges[0].items = @('Maintenance update.')
}

$newEntry = [pscustomobject]@{
    version = $Version
    date = (Get-Date).ToString('yyyy-MM-dd')
    title = $Title
    changes = $entryChanges
}

$existingVersions = @($changelog.versions | Where-Object { $_.version -ne $Version })
$changelog.latestVersion = $Version
$changelog.updatedAt = (Get-Date).ToString('yyyy-MM-dd')
$changelog.download.installer = "ServoERP_Setup_$Version.exe"
$changelog.versions = @($newEntry) + $existingVersions
$changelog | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $changelogPath -Encoding UTF8

if (-not [string]::IsNullOrWhiteSpace($DownloadUrl)) {
    Update-MarketingScript -Path $marketingScript -Url $DownloadUrl.Trim()
    Update-DownloadPage -Path $downloadPage -Url $DownloadUrl.Trim()
}

if (-not $NoBuild) {
    $buildArgs = @()
    if ($SkipPrerequisiteDownload) {
        $buildArgs += '-SkipPrerequisiteDownload'
    }
    if ($ForceCloseRunningApp) {
        $buildArgs += '-ForceCloseRunningApp'
    }

    & (Join-Path $PSScriptRoot 'Build-ServoERPInstaller.ps1') @buildArgs
}

if (Test-Path -LiteralPath $marketingZip) {
    Remove-Item -LiteralPath $marketingZip -Force
}

Compress-Archive -Path (Join-Path $marketingRoot '*') -DestinationPath $marketingZip -Force

Write-Host ""
Write-Host "Release files prepared:"
Write-Host "  Installer folder: $installerOutput"
Write-Host "  Marketing deploy zip: $marketingZip"
Write-Host ""
Write-Host "Next manual steps:"
Write-Host "  1. Upload installer_output\ServoERP_Setup_$Version.exe to your release download location."
Write-Host "  2. Deploy $marketingZip contents to servoerp.in."
