param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Title,

    [string]$DownloadUrl,

    [string[]]$Changes = @(),

    [switch]$NoBuild,

    [switch]$SkipPrerequisiteDownload,

    [switch]$ForceCloseRunningApp,

    [switch]$PublishCloudflare,

    [switch]$SkipMarketingZip
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
    if (-not [regex]::IsMatch($text, $Pattern)) {
        throw "No matching text was found in $Path"
    }

    $updated = [regex]::Replace($text, $Pattern, $Replacement)
    if ($updated -eq $text) {
        return
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

function Remove-ExistingFileWithRetry {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$MaxAttempts = 5,
        [int]$DelayMilliseconds = 500
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $attempts = [Math]::Max(1, $MaxAttempts)
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -ge $attempts) {
                throw
            }

            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }
}

function Resolve-ArchiveOutputPath {
    param(
        [Parameter(Mandatory = $true)][string]$PreferredPath
    )

    if (-not (Test-Path -LiteralPath $PreferredPath)) {
        return $PreferredPath
    }

    try {
        Remove-ExistingFileWithRetry -Path $PreferredPath
        return $PreferredPath
    }
    catch {
        $directory = Split-Path -Path $PreferredPath -Parent
        $name = [System.IO.Path]::GetFileNameWithoutExtension($PreferredPath)
        $extension = [System.IO.Path]::GetExtension($PreferredPath)
        return Join-Path $directory ("{0}_{1}{2}" -f $name, (Get-Date).ToString('yyyyMMdd-HHmmss'), $extension)
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$sourceRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$marketingRoot = Join-Path $repoRoot 'marketing_site'
$assemblyInfo = Join-Path $sourceRoot 'Properties\AssemblyInfo.cs'
$appConfig = Join-Path $sourceRoot 'HVACPro.config'
$versionTxt = Join-Path $marketingRoot 'version.txt'
$changelogPath = Join-Path $marketingRoot 'changelog.json'
$latestPath = Join-Path $marketingRoot 'latest.json'
$downloadPage = Join-Path $marketingRoot 'download\index.html'
$marketingScript = Join-Path $marketingRoot 'script.js'
$installerOutput = Join-Path $repoRoot 'installer_output'
$marketingZip = Join-Path $repoRoot ("marketing_site_deploy_{0}.zip" -f $Version)

$parsedVersion = $null
if (-not [Version]::TryParse($Version, [ref]$parsedVersion)) {
    throw "Version must be numeric, for example 1.0.13.0"
}

$semVersion = "{0}.{1}.{2}" -f $parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build
$githubPackageUrl = "https://github.com/harshals499/ServoERP/releases/download/v$semVersion/ServoERP.Desktop-$semVersion-full.nupkg"

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
$downloadUrlValue = "https://downloads.servoerp.in/ServoERP_Setup_$Version.exe"
$installerNameValue = "ServoERP_Setup_$Version.exe"
if ($null -eq $changelog.download.PSObject.Properties['url']) {
    $changelog.download | Add-Member -NotePropertyName 'url' -NotePropertyValue $downloadUrlValue
}
else {
    $changelog.download.url = $downloadUrlValue
}
if ($null -eq $changelog.download.PSObject.Properties['installer']) {
    $changelog.download | Add-Member -NotePropertyName 'installer' -NotePropertyValue $installerNameValue
}
else {
    $changelog.download.installer = $installerNameValue
}
$changelog.download.packageUrl = $githubPackageUrl
$changelog.versions = @($newEntry) + $existingVersions
$changelog | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $changelogPath -Encoding UTF8

if (Test-Path -LiteralPath $latestPath) {
    $latest = Get-Content -LiteralPath $latestPath -Raw | ConvertFrom-Json
    $latest.latestVersion = $Version
    $latest.updatedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    if ($null -eq $latest.PSObject.Properties['downloadAvailable']) {
        $latest | Add-Member -NotePropertyName 'downloadAvailable' -NotePropertyValue $true
    }
    else {
        $latest.downloadAvailable = $true
    }

    if ($null -eq $latest.PSObject.Properties['updateAvailable']) {
        $latest | Add-Member -NotePropertyName 'updateAvailable' -NotePropertyValue $true
    }
    else {
        $latest.updateAvailable = $true
    }

    if ($null -eq $latest.PSObject.Properties['installerUrl']) {
        $latest | Add-Member -NotePropertyName 'installerUrl' -NotePropertyValue "https://downloads.servoerp.in/ServoERP_Setup_$Version.exe"
    }
    else {
        $latest.installerUrl = "https://downloads.servoerp.in/ServoERP_Setup_$Version.exe"
    }

    if ($null -eq $latest.PSObject.Properties['packageUrl']) {
        $latest | Add-Member -NotePropertyName 'packageUrl' -NotePropertyValue $githubPackageUrl
    }
    else {
        $latest.packageUrl = $githubPackageUrl
    }

    if ($null -eq $latest.PSObject.Properties['notes']) {
        $latest | Add-Member -NotePropertyName 'notes' -NotePropertyValue "ServoERP release package and installer are available for this version."
    }
    else {
        $latest.notes = "ServoERP release package and installer are available for this version."
    }
    $latest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $latestPath -Encoding UTF8
}

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

if (-not $SkipMarketingZip) {
    $marketingZip = Resolve-ArchiveOutputPath -PreferredPath $marketingZip
    Compress-Archive -Path (Join-Path $marketingRoot '*') -DestinationPath $marketingZip -Force
}

if ($PublishCloudflare) {
    & (Join-Path $PSScriptRoot 'Publish-ServoERPCloudflare.ps1') -Version $Version
}

Write-Host ""
Write-Host "Release files prepared:"
Write-Host "  Installer folder: $installerOutput"
Write-Host "  Marketing deploy zip: $(if ($SkipMarketingZip) { '(skipped)' } else { $marketingZip })"
Write-Host "  Standard installer: installer_output\ServoERP_Setup_$Version.exe (Velopack-managed)"
if (-not $PublishCloudflare) {
    Write-Host ""
    Write-Host "Next manual steps:"
    Write-Host "  1. Upload installer_output\ServoERP_Setup_$Version.exe to your release download location."
    Write-Host "  2. Deploy $marketingZip contents to servoerp.in."
}
