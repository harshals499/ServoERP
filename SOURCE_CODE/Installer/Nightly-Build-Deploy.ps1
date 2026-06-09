# =============================================================================
#  ServoERP - Nightly Build and Deploy Script
#  Author   : Harshal Sonawane
#  Schedule : Every night at 23:30 Amsterdam time (CET/CEST)
#  Rule     : Script will HARD STOP if run before 23:30 Amsterdam time
# =============================================================================

param (
    [string]$SolutionPath  = "C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln",
    [string]$DeployPath    = "C:\Deploy\ServoERP",
    [string]$LogPath       = "C:\Deploy\logs",
    [string]$MSBuildPath   = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    [string]$Configuration = "Release",
    [int]   $AllowedHour   = 23,
    [int]   $AllowedMinute = 30
)

$ErrorActionPreference = "Stop"
$timestamp             = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$logFile               = Join-Path $LogPath "deploy_$timestamp.log"
$solutionFolder        = Split-Path $SolutionPath
$buildOutput           = Join-Path $solutionFolder "bin\$Configuration"
$amsterdamTimeZoneId   = "W. Europe Standard Time"

if (-not (Test-Path $LogPath)) { New-Item -ItemType Directory -Path $LogPath | Out-Null }

function Log {
    param([string]$Message, [string]$Level = "INFO")
    $entry = "[$(Get-Date -Format 'HH:mm:ss')] [$Level] $Message"
    Write-Host $entry
    Add-Content -Path $logFile -Value $entry
}

function Abort {
    param([string]$Reason)
    Log $Reason "BLOCKED"
    Log "Script exited without deploying." "BLOCKED"
    exit 1
}

function Get-AmsterdamNow {
    try {
        $zone = [System.TimeZoneInfo]::FindSystemTimeZoneById($amsterdamTimeZoneId)
        return [System.TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, $zone)
    }
    catch {
        Log "Amsterdam timezone lookup failed; falling back to machine local time. $_" "WARN"
        return Get-Date
    }
}

function Copy-FolderContents {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path $Destination)) {
        New-Item -ItemType Directory -Path $Destination | Out-Null
    }

    Get-ChildItem $Source -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($Source.Length).TrimStart('\')
        $target = Join-Path $Destination $relative
        $targetDir = Split-Path $target -Parent
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir | Out-Null
        }
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}

Log "=============================================="
Log " ServoERP Nightly Deploy Starting"
Log "=============================================="

$now    = Get-AmsterdamNow
$hour   = $now.Hour
$minute = $now.Minute

Log "Current Amsterdam time: $($now.ToString('HH:mm')) on $($now.ToString('dddd, dd MMM yyyy'))"

if ($hour -lt $AllowedHour) {
    Abort "DEPLOY BLOCKED - It is $($hour):$($minute.ToString('D2')). Deployment is only allowed at or after 23:30 Amsterdam time. Exiting."
}

if ($hour -eq $AllowedHour -and $minute -lt $AllowedMinute) {
    Abort "DEPLOY BLOCKED - It is $($hour):$($minute.ToString('D2')). Deployment window starts at 23:30 Amsterdam time. Exiting."
}

Log "Time check passed. Proceeding with nightly build and deploy."

Log "Validating required paths..."

if (-not (Test-Path $SolutionPath)) {
    Abort "Solution file not found at: $SolutionPath"
}

if (-not (Test-Path $MSBuildPath)) {
    Abort "MSBuild not found at: $MSBuildPath - check Visual Studio installation."
}

if (-not (Test-Path $DeployPath)) {
    Log "Deploy folder not found. Creating: $DeployPath"
    New-Item -ItemType Directory -Path $DeployPath | Out-Null
}

Log "All paths validated."

Log "Backing up previous deployment..."

$backupFolder = Join-Path $DeployPath "backup_$timestamp"
$deployedFiles = @(Get-ChildItem $DeployPath -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notlike "*\backup_*" })

if ($deployedFiles.Count -gt 0) {
    New-Item -ItemType Directory -Path $backupFolder | Out-Null
    foreach ($file in $deployedFiles) {
        $relative = $file.FullName.Substring($DeployPath.Length).TrimStart('\')
        $target = Join-Path $backupFolder $relative
        $targetDir = Split-Path $target -Parent
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir | Out-Null
        }
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    Log "Backup created at: $backupFolder"
} else {
    Log "No previous deployment found. Skipping backup."
}

Log "Cleaning previous build output..."

if (Test-Path $buildOutput) {
    Remove-Item -Path $buildOutput -Recurse -Force
    Log "Build output folder cleaned."
} else {
    Log "No previous build output found."
}

Log "Starting MSBuild for configuration: $Configuration"
Log "Solution: $SolutionPath"

$buildLog = Join-Path $LogPath "build_$timestamp.log"
$msbuildArgs = @(
    $SolutionPath,
    "/p:Configuration=$Configuration",
    "/t:Rebuild",
    "/m",
    "/nologo",
    "/flp:LogFile=$buildLog;Verbosity=minimal"
)

try {
    $process = Start-Process -FilePath $MSBuildPath `
                             -ArgumentList $msbuildArgs `
                             -Wait -PassThru -NoNewWindow

    if ($process.ExitCode -ne 0) {
        Log "MSBuild failed with exit code $($process.ExitCode). Check build log: $buildLog" "ERROR"
        Abort "BUILD FAILED - deployment cancelled to protect existing version."
    }

    Log "Build succeeded."
} catch {
    Abort "Exception during build: $_"
}

Log "Validating build output..."

$exeFile = Get-ChildItem $buildOutput -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $exeFile) {
    Abort "No .exe found in build output at $buildOutput - deploy cancelled."
}

Log "Found executable: $($exeFile.Name) ($([math]::Round($exeFile.Length/1KB, 1)) KB)"

Log "Deploying to: $DeployPath"

try {
    Copy-FolderContents -Source $buildOutput -Destination $DeployPath
    $count = @(Get-ChildItem $buildOutput -Recurse -File).Count
    Log "Deployed $count file(s) successfully."
} catch {
    Log "Deploy failed: $_" "ERROR"
    Log "Attempting rollback from backup..." "WARN"

    if (Test-Path $backupFolder) {
        Copy-FolderContents -Source $backupFolder -Destination $DeployPath
        Log "Rollback complete. Previous version restored." "WARN"
    } else {
        Log "No backup available for rollback." "ERROR"
    }

    exit 1
}

Log "Cleaning up old backups (keeping last 7)..."

$oldBackups = Get-ChildItem $DeployPath -Directory -Filter "backup_*" |
              Sort-Object CreationTime -Descending |
              Select-Object -Skip 7

foreach ($old in $oldBackups) {
    Remove-Item $old.FullName -Recurse -Force
    Log "Removed old backup: $($old.Name)"
}

# Step 8 - Trigger post-deploy smoke via Codex webhook
Log "Triggering Codex post-deploy smoke check..."
$newVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exeFile.FullName).FileVersion
$webhookUrl = "PASTE_YOUR_CODEX_WEBHOOK_URL_HERE"
Invoke-RestMethod -Uri $webhookUrl -Method POST -ContentType "application/json" -Body '{"event":"deploy_complete","version":"'+ $newVersion +'"}'
Log "Webhook fired. Check Codex review queue for smoke result."

Log "=============================================="
Log " ServoERP deploy COMPLETED SUCCESSFULLY"
Log " Time     : $(Get-Date -Format 'HH:mm:ss')"
Log " Deployed : $($exeFile.Name)"
Log " Location : $DeployPath"
Log " Log file : $logFile"
Log "=============================================="

exit 0
