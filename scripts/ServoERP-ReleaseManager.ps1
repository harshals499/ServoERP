param(
    [string]$Version,
    [int]$PatchIncrement = 1,
    [string]$Title,
    [string[]]$Changes = @(),
    [switch]$Publish,
    [switch]$IncludeLegacyInstaller,
    [switch]$ForceCloseRunningApp,
    [switch]$SkipGitHubDownload,
    [switch]$SkipGitHubPublish,
    [switch]$SkipCloudflarePublish,
    [switch]$RunUiSmokeTest,
    [int]$VerificationAttempts = 18,
    [int]$VerificationDelaySeconds = 10
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sourceRoot = Join-Path $repoRoot "SOURCE_CODE"
$installerRoot = Join-Path $sourceRoot "Installer"
$versionFile = Join-Path $repoRoot "VERSION"
$changelogFile = Join-Path $repoRoot "CHANGELOG.md"
$logDir = Join-Path $repoRoot "LOGS"
$logPath = Join-Path $logDir "release-manager.log"
$solution = Join-Path $sourceRoot "HVAC_Pro_Desktop.sln"
$releaseExe = Join-Path $sourceRoot "bin\Release\HVAC_Pro_Desktop.exe"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$buildDir = Join-Path $artifactsRoot "build"
$publishDir = Join-Path $artifactsRoot "publish"
$velopackDir = Join-Path $artifactsRoot "velopack"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-ReleaseLog {
    param([string]$Message)

    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    Write-Host $Message
}

function Read-SemVer {
    param([Parameter(Mandatory = $true)][string]$Value)

    $match = [regex]::Match($Value.Trim(), "^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.\d+)?$")
    if (-not $match.Success) {
        throw "Version '$Value' is not semantic version MAJOR.MINOR.PATCH(.REVISION)."
    }

    return [pscustomobject]@{
        Major = [int]$match.Groups["major"].Value
        Minor = [int]$match.Groups["minor"].Value
        Patch = [int]$match.Groups["patch"].Value
    }
}

function Resolve-ReleaseVersion {
    param(
        [string]$RequestedVersion,
        [int]$RequestedPatchIncrement
    )

    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "VERSION file not found at $versionFile"
    }

    $currentVersion = (Get-Content -LiteralPath $versionFile -Raw).Trim()
    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        $parts = Read-SemVer -Value $RequestedVersion
        return [pscustomobject]@{
            PreviousSemVersion = $currentVersion
            SemVersion = "{0}.{1}.{2}" -f $parts.Major, $parts.Minor, $parts.Patch
            FullVersion = "{0}.{1}.{2}.0" -f $parts.Major, $parts.Minor, $parts.Patch
        }
    }

    $currentParts = Read-SemVer -Value $currentVersion
    $nextPatch = $currentParts.Patch + [Math]::Max(0, $RequestedPatchIncrement)
    return [pscustomobject]@{
        PreviousSemVersion = $currentVersion
        SemVersion = "{0}.{1}.{2}" -f $currentParts.Major, $currentParts.Minor, $nextPatch
        FullVersion = "{0}.{1}.{2}.0" -f $currentParts.Major, $currentParts.Minor, $nextPatch
    }
}

function Get-ChangesFromMarkdownChangelog {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$FullVersion
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    $lines = Get-Content -LiteralPath $Path
    $capture = $false
    $items = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line -match "^##\s+$([regex]::Escape($FullVersion))\s+-\s+") {
            $capture = $true
            continue
        }

        if ($capture -and $line -match "^##\s+") {
            break
        }

        if ($capture -and $line -match "^\s*-\s+(?<item>.+?)\s*$") {
            $items.Add($matches["item"].Trim())
        }
    }

    return @($items)
}

function Resolve-ReleaseNotes {
    param(
        [string]$RequestedTitle,
        [string[]]$RequestedChanges,
        [string]$FullVersion
    )

    $resolvedChanges = @($RequestedChanges | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($resolvedChanges.Count -eq 0) {
        $resolvedChanges = @(Get-ChangesFromMarkdownChangelog -Path $changelogFile -FullVersion $FullVersion)
    }
    if ($resolvedChanges.Count -eq 0) {
        $resolvedChanges = @("Maintenance update.")
    }

    $resolvedTitle = if ([string]::IsNullOrWhiteSpace($RequestedTitle)) {
        "ServoERP update $FullVersion"
    }
    else {
        $RequestedTitle.Trim()
    }

    return [pscustomobject]@{
        Title = $resolvedTitle
        Changes = $resolvedChanges
    }
}

function Update-Stage {
    param(
        [hashtable]$Report,
        [string]$Name,
        [string]$Status,
        [string]$Detail
    )

    if (-not $Report.Contains("stages") -or $null -eq $Report["stages"]) {
        $Report["stages"] = [ordered]@{}
    }

    $Report["stages"][$Name] = [ordered]@{
        status = $Status
        detail = $Detail
        at = (Get-Date).ToString("s")
    }
}

function Invoke-LoggedStage {
    param(
        [hashtable]$Report,
        [string]$Name,
        [scriptblock]$Action
    )

    Write-ReleaseLog ("[{0}] started" -f $Name)
    Update-Stage -Report $Report -Name $Name -Status "running" -Detail ""
    try {
        & $Action
        Update-Stage -Report $Report -Name $Name -Status "passed" -Detail ""
        Write-ReleaseLog ("[{0}] passed" -f $Name)
    }
    catch {
        Update-Stage -Report $Report -Name $Name -Status "failed" -Detail $_.Exception.Message
        Write-ReleaseLog ("[{0}] failed: {1}" -f $Name, $_.Exception.Message)
        throw
    }
}

function Assert-FileExists {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file was not found: $Path"
    }
}

function Remove-PathIfExists {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
}

function Remove-StaleReleaseFiles {
    param([Parameter(Mandatory = $true)][string]$Root)

    Get-ChildItem -LiteralPath $Root -Filter 'marketing_site_deploy_*.zip' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip 1 |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
        }
}

function Upload-R2ObjectDirect {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$ObjectKey,
        [Parameter(Mandatory = $true)][string]$ContentType
    )

    $accountId = if ([string]::IsNullOrWhiteSpace($env:R2_ACCOUNT_ID)) { "ba80bcc2ebee2669dab5dbf15dc5f4da" } else { $env:R2_ACCOUNT_ID }
    $bucketName = if ([string]::IsNullOrWhiteSpace($env:R2_BUCKET)) { "servoerp-downloads" } else { $env:R2_BUCKET }
    $token = $env:CLOUDFLARE_API_TOKEN
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Missing CLOUDFLARE_API_TOKEN."
    }

    $headers = @{ Authorization = "Bearer $token" }
    $url = "https://api.cloudflare.com/client/v4/accounts/{0}/r2/buckets/{1}/objects/{2}" -f $accountId, $bucketName, $ObjectKey
    Invoke-RestMethod -Uri $url -Method Put -Headers $headers -InFile $FilePath -ContentType $ContentType | Out-Null
}

function Publish-InstallerWithFallback {
    param(
        [Parameter(Mandatory = $true)][string]$PrimaryInstallerPath,
        [Parameter(Mandatory = $true)][string]$FallbackInstallerPath,
        [Parameter(Mandatory = $true)][string]$PublicInstallerName
    )

    try {
        & (Join-Path $installerRoot "Publish-ServoERPR2Installer.ps1") -InstallerPath $PrimaryInstallerPath -ObjectKey $PublicInstallerName
        return [pscustomobject]@{
            SourcePath = $PrimaryInstallerPath
            Mode = "primary"
        }
    }
    catch {
        Write-ReleaseLog ("Primary installer upload failed, falling back to Velopack setup: {0}" -f $_.Exception.Message)
        Upload-R2ObjectDirect -FilePath $FallbackInstallerPath -ObjectKey $PublicInstallerName -ContentType "application/vnd.microsoft.portable-executable"
        return [pscustomobject]@{
            SourcePath = $FallbackInstallerPath
            Mode = "fallback"
        }
    }
}

function Upload-LegacyInstallerIfPresent {
    param(
        [string]$LegacyInstallerPath,
        [string]$PublicInstallerName
    )

    if ([string]::IsNullOrWhiteSpace($LegacyInstallerPath) -or -not (Test-Path -LiteralPath $LegacyInstallerPath)) {
        return $null
    }

    Upload-R2ObjectDirect -FilePath $LegacyInstallerPath -ObjectKey $PublicInstallerName -ContentType "application/vnd.microsoft.portable-executable"
    return "https://downloads.servoerp.in/{0}" -f $PublicInstallerName
}

function Build-ReleaseReportPath {
    param([Parameter(Mandatory = $true)][string]$FullVersion)

    $dir = Join-Path $artifactsRoot ("release\{0}" -f $FullVersion)
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    return Join-Path $dir "release-report.json"
}

$versionInfo = Resolve-ReleaseVersion -RequestedVersion $Version -RequestedPatchIncrement $PatchIncrement
$notes = Resolve-ReleaseNotes -RequestedTitle $Title -RequestedChanges $Changes -FullVersion $versionInfo.FullVersion
$report = [ordered]@{
    product = "ServoERP"
    startedAt = (Get-Date).ToString("s")
    publish = [bool]$Publish
    includeLegacyInstaller = [bool]$IncludeLegacyInstaller
    previousSemVersion = $versionInfo.PreviousSemVersion
    semVersion = $versionInfo.SemVersion
    fullVersion = $versionInfo.FullVersion
    title = $notes.Title
    changes = @($notes.Changes)
    stages = [ordered]@{}
    artifacts = [ordered]@{}
}

$reportPath = Build-ReleaseReportPath -FullVersion $versionInfo.FullVersion

try {
    Invoke-LoggedStage -Report $report -Name "cleanup" -Action {
        Remove-PathIfExists -Path $publishDir
        Remove-PathIfExists -Path $velopackDir
        Remove-StaleReleaseFiles -Root $repoRoot
    }

    Invoke-LoggedStage -Report $report -Name "set-version" -Action {
        & (Join-Path $PSScriptRoot "Set-ServoVersion.ps1") -Version $versionInfo.SemVersion | Out-Null
        & (Join-Path $installerRoot "Release-ServoERP.ps1") -Version $versionInfo.FullVersion -Title $notes.Title -Changes $notes.Changes -NoBuild -SkipPrerequisiteDownload -SkipMarketingZip
    }

    Invoke-LoggedStage -Report $report -Name "build-release" -Action {
        & msbuild $solution /m /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
        if ($LASTEXITCODE -ne 0) {
            throw "MSBuild failed with exit code $LASTEXITCODE."
        }
        Assert-FileExists -Path $releaseExe
        $report.artifacts.releaseExe = $releaseExe
    }

    if ($RunUiSmokeTest) {
        Invoke-LoggedStage -Report $report -Name "ui-smoketest" -Action {
            & $releaseExe /smoketest
            if ($LASTEXITCODE -ne 0) {
                throw "UI smoke test exited with code $LASTEXITCODE."
            }
        }
    }

    Invoke-LoggedStage -Report $report -Name "prepare-velopack" -Action {
        & (Join-Path $PSScriptRoot "Prepare-VelopackPublish.ps1") -BuildOutput (Join-Path $sourceRoot "bin\Release") -PublishDir $publishDir | Out-Null
        if (-not $SkipGitHubDownload) {
            vpk download github --repoUrl "https://github.com/harshals499/ServoERP" --outputDir $velopackDir
            if ($LASTEXITCODE -ne 0) {
                throw "vpk download github failed with exit code $LASTEXITCODE."
            }
        }

        vpk pack `
            --packId "ServoERP.Desktop" `
            --packTitle "ServoERP" `
            --packAuthors "Harshal Sonawane" `
            --packVersion $versionInfo.SemVersion `
            --packDir $publishDir `
            --outputDir $velopackDir `
            --mainExe "HVAC_Pro_Desktop.exe" `
            --icon (Join-Path $sourceRoot "app.ico") `
            --runtime win-x64 `
            --exclude "(?i)(^|[\\/])(HVACPro\.config|.*\.servoerp-license|.*\.sqlite|.*\.mdf|.*\.ldf|logs?|database|updates?)([\\/]|$)"
        if ($LASTEXITCODE -ne 0) {
            throw "vpk pack failed with exit code $LASTEXITCODE."
        }

        $report.artifacts.velopackSetup = Join-Path $velopackDir "ServoERP.Desktop-win-Setup.exe"
        $report.artifacts.velopackFull = Join-Path $velopackDir ("ServoERP.Desktop-{0}-full.nupkg" -f $versionInfo.SemVersion)
        Assert-FileExists -Path $report.artifacts.velopackSetup
        Assert-FileExists -Path $report.artifacts.velopackFull
    }

    Invoke-LoggedStage -Report $report -Name "build-update-package" -Action {
        & (Join-Path $installerRoot "Build-ServoERPUpdatePackage.ps1") -NoBuild -ForceCloseRunningApp:$ForceCloseRunningApp
        $report.artifacts.updateZip = Join-Path $repoRoot ("update_output\ServoERP_Update_{0}.zip" -f $versionInfo.FullVersion)
        Assert-FileExists -Path $report.artifacts.updateZip
    }

    if ($IncludeLegacyInstaller) {
        Invoke-LoggedStage -Report $report -Name "build-legacy-installer" -Action {
            & (Join-Path $installerRoot "Build-ServoERPInstaller.ps1") -SkipPrerequisiteDownload -ForceCloseRunningApp:$ForceCloseRunningApp -IncludeLegacyInstaller
            if ($LASTEXITCODE -ne 0) {
                throw "Build-ServoERPInstaller failed with exit code $LASTEXITCODE."
            }
            $report.artifacts.legacyInstaller = Join-Path $repoRoot ("installer_output\ServoERP_Legacy_Setup_{0}.exe" -f $versionInfo.FullVersion)
            Assert-FileExists -Path $report.artifacts.legacyInstaller
        }
    }

    if ($Publish) {
        if (-not $SkipGitHubPublish) {
            Invoke-LoggedStage -Report $report -Name "publish-github" -Action {
                $env:GITHUB_TOKEN = gh auth token
                if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
                    throw "GitHub authentication token was not available from gh auth token."
                }

                vpk upload github `
                    --repoUrl "https://github.com/harshals499/ServoERP" `
                    --token $env:GITHUB_TOKEN `
                    --outputDir $velopackDir `
                    --publish `
                    --releaseName ("ServoERP {0}" -f $versionInfo.SemVersion) `
                    --tag ("v{0}" -f $versionInfo.SemVersion) `
                    --targetCommitish "main"

                $report.artifacts.githubRelease = "https://github.com/harshals499/ServoERP/releases/tag/v{0}" -f $versionInfo.SemVersion
            }
        }

        if (-not $SkipCloudflarePublish) {
            Invoke-LoggedStage -Report $report -Name "publish-cloudflare" -Action {
                $publicInstallerName = "ServoERP_Setup_{0}.exe" -f $versionInfo.FullVersion
                $primaryInstallerPath = $report.artifacts.velopackSetup

                $uploadResult = Publish-InstallerWithFallback `
                    -PrimaryInstallerPath $primaryInstallerPath `
                    -FallbackInstallerPath $report.artifacts.velopackSetup `
                    -PublicInstallerName $publicInstallerName

                if ($IncludeLegacyInstaller -and $report.artifacts.legacyInstaller) {
                    $report.artifacts.publicLegacyInstaller = Upload-LegacyInstallerIfPresent `
                        -LegacyInstallerPath $report.artifacts.legacyInstaller `
                        -PublicInstallerName ("ServoERP_Legacy_Setup_{0}.exe" -f $versionInfo.FullVersion)
                }

                Upload-R2ObjectDirect `
                    -FilePath $report.artifacts.updateZip `
                    -ObjectKey ("updates/ServoERP_Update_{0}.zip" -f $versionInfo.FullVersion) `
                    -ContentType "application/zip"

                & (Join-Path $installerRoot "Publish-ServoERPCloudflare.ps1") `
                    -Version $versionInfo.FullVersion `
                    -SkipInstallerUpload `
                    -SkipVerification

                & (Join-Path $installerRoot "Verify-ServoERPCloudflareRelease.ps1") `
                    -Version $versionInfo.FullVersion `
                    -MaxAttempts $VerificationAttempts `
                    -DelaySeconds $VerificationDelaySeconds

                $report.artifacts.publicInstaller = "https://downloads.servoerp.in/{0}" -f $publicInstallerName
                $report.artifacts.publicUpdateZip = "https://downloads.servoerp.in/updates/ServoERP_Update_{0}.zip" -f $versionInfo.FullVersion
                $report.artifacts.installerUploadMode = $uploadResult.Mode
                $report.artifacts.installerUploadSource = $uploadResult.SourcePath
            }
        }
    }

    $report.completedAt = (Get-Date).ToString("s")
    $report.status = "passed"
}
catch {
    $report.completedAt = (Get-Date).ToString("s")
    $report.status = "failed"
    $report.error = $_.Exception.Message
    throw
}
finally {
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-ReleaseLog ("Release report written to {0}" -f $reportPath)
}
