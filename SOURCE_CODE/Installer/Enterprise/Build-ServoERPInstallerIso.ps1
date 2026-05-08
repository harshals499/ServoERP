param(
    [string]$Version,
    [switch]$RebuildInstaller
)

$ErrorActionPreference = 'Stop'

$enterpriseRoot = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $enterpriseRoot '..\..\..')
$sourceRoot = Resolve-Path (Join-Path $enterpriseRoot '..\..')
$outputRoot = Join-Path $repoRoot 'installer_output\enterprise'
$isoStageRoot = Join-Path $outputRoot 'iso-stage'
$isoPath = Join-Path $outputRoot 'ServoERP-Enterprise.iso'
$assemblyInfo = Join-Path $sourceRoot 'Properties\AssemblyInfo.cs'

if (-not $Version) {
    $assemblyText = Get-Content -LiteralPath $assemblyInfo -Raw
    $match = [regex]::Match($assemblyText, 'AssemblyVersion\("(?<version>[^"]+)"\)')
    if (-not $match.Success) {
        throw "Could not read AssemblyVersion from $assemblyInfo"
    }
    $Version = $match.Groups['version'].Value
}

$versionedIsoPath = Join-Path $outputRoot "ServoERP-Enterprise-$Version.iso"
$setupExe = Join-Path $outputRoot "ServoERP.Setup.$Version.exe"
$appMsi = Join-Path $outputRoot "ServoERP.App.$Version.msi"

if ($RebuildInstaller) {
    & (Join-Path $enterpriseRoot 'Build-EnterpriseInstaller.ps1') -SkipPrerequisiteDownload -ForceCloseRunningApp
}

foreach ($required in @($setupExe, $appMsi)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required installer artifact not found: $required"
    }
}

if (Test-Path -LiteralPath $isoStageRoot) {
    Remove-Item -LiteralPath $isoStageRoot -Recurse -Force
}

$folders = @(
    $isoStageRoot,
    (Join-Path $isoStageRoot 'Install'),
    (Join-Path $isoStageRoot 'Docs'),
    (Join-Path $isoStageRoot 'Prerequisites')
)
$folders | ForEach-Object { New-Item -ItemType Directory -Force -Path $_ | Out-Null }

Copy-Item -LiteralPath $setupExe -Destination (Join-Path $isoStageRoot 'Install') -Force
Copy-Item -LiteralPath $appMsi -Destination (Join-Path $isoStageRoot 'Install') -Force

$deploymentDoc = Join-Path $enterpriseRoot 'docs\EnterpriseDeployment.md'
if (Test-Path -LiteralPath $deploymentDoc) {
    Copy-Item -LiteralPath $deploymentDoc -Destination (Join-Path $isoStageRoot 'Docs') -Force
}

$prereqDir = Resolve-Path (Join-Path $enterpriseRoot '..\Prerequisites')
Copy-Item -Path (Join-Path $prereqDir '*') -Destination (Join-Path $isoStageRoot 'Prerequisites') -Recurse -Force

@"
@echo off
setlocal
cd /d "%~dp0"
set LOGDIR=C:\ProgramData\ServoERP\Logs
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set LOGFILE=%LOGDIR%\ServoERP-Bundle.log
echo Starting ServoERP Enterprise Setup...
echo Log file: %LOGFILE%
"%~dp0Install\ServoERP.Setup.$Version.exe" /log "%LOGFILE%"
set EXITCODE=%ERRORLEVEL%
echo ServoERP setup exit code: %EXITCODE%
if not "%EXITCODE%"=="0" if not "%EXITCODE%"=="3010" (
  echo Setup failed. Review "%LOGFILE%".
  pause
)
endlocal
exit /b %EXITCODE%
"@ | Set-Content -LiteralPath (Join-Path $isoStageRoot 'Setup-ServoERP.cmd') -Encoding ASCII

@"
@echo off
setlocal
cd /d "%~dp0"
set LOGDIR=C:\ProgramData\ServoERP\Logs
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set LOGFILE=%LOGDIR%\ServoERP-Bundle-Silent.log
echo Installing ServoERP silently. Log file: %LOGFILE%
"%~dp0Install\ServoERP.Setup.$Version.exe" /quiet /norestart /log "%LOGFILE%"
set EXITCODE=%ERRORLEVEL%
echo ServoERP silent setup exit code: %EXITCODE%
endlocal
exit /b %EXITCODE%
"@ | Set-Content -LiteralPath (Join-Path $isoStageRoot 'SilentInstall-ServoERP.cmd') -Encoding ASCII

@"
[AutoRun]
open=Setup-ServoERP.cmd
icon=Install\ServoERP.Setup.$Version.exe
label=ServoERP Enterprise $Version
"@ | Set-Content -LiteralPath (Join-Path $isoStageRoot 'autorun.inf') -Encoding ASCII

@"
ServoERP Enterprise Installer ISO
Version: $Version

Interactive install:
  Setup-ServoERP.cmd

Silent install:
  SilentInstall-ServoERP.cmd

Main installer:
  Install\ServoERP.Setup.$Version.exe

MSI-only enterprise deployment:
  Install\ServoERP.App.$Version.msi

Documentation:
  Docs\EnterpriseDeployment.md
"@ | Set-Content -LiteralPath (Join-Path $isoStageRoot 'README.txt') -Encoding ASCII

$oscdimg = @(
    (Get-Command oscdimg.exe -ErrorAction SilentlyContinue).Source
    (Get-ChildItem 'C:\Program Files (x86)\Windows Kits' -Recurse -Filter oscdimg.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName)
) | Where-Object { $_ } | Select-Object -First 1

if ($oscdimg) {
    & $oscdimg -m -o -u2 -udfver102 -l"SERVOERP_$($Version.Replace('.','_'))" $isoStageRoot $versionedIsoPath
    if ($LASTEXITCODE -ne 0) {
        throw 'oscdimg failed.'
    }
}
else {
    $python = (Get-Command python -ErrorAction SilentlyContinue).Source
    if (-not $python) {
        throw 'Neither oscdimg.exe nor python is available to generate the ISO.'
    }

    cmd /c "`"$python`" -m pip show pycdlib >nul 2>nul"
    if ($LASTEXITCODE -ne 0) {
        & $python -m pip install pycdlib
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not install pycdlib fallback ISO writer.'
        }
    }

    $isoWriter = Join-Path $outputRoot 'write_iso.py'
@'
import os
import sys
import pycdlib

stage, output, volume = sys.argv[1], sys.argv[2], sys.argv[3][:16]
iso = pycdlib.PyCdlib()
iso.new(interchange_level=3, joliet=3, udf='2.60', vol_ident=volume[:32])

def iso_name(name):
    base, ext = os.path.splitext(name.upper())
    stem = ''.join(c if c.isalnum() else '_' for c in base)
    if ext:
        extension = ''.join(c if c.isalnum() else '_' for c in ext.lstrip('.'))
        return f"{stem[:30]}.{extension[:3]}"
    return stem[:31]

def add_tree(root, rel=''):
    current = os.path.join(root, rel)
    for entry in sorted(os.listdir(current)):
        full = os.path.join(current, entry)
        child_rel = os.path.join(rel, entry) if rel else entry
        joliet_path = '/' + child_rel.replace(os.sep, '/')
        udf_path = joliet_path
        iso_path = '/' + '/'.join(iso_name(p) for p in child_rel.split(os.sep))
        if os.path.isdir(full):
            iso.add_directory(iso_path=iso_path, joliet_path=joliet_path, udf_path=udf_path)
            add_tree(root, child_rel)
        else:
            iso.add_file(full, iso_path=iso_path + ';1', joliet_path=joliet_path, udf_path=udf_path)

add_tree(stage)
iso.write(output)
iso.close()
'@ | Set-Content -LiteralPath $isoWriter -Encoding ASCII

    & $python $isoWriter $isoStageRoot $versionedIsoPath "SERVOERP_$($Version.Replace('.','_'))"
    if ($LASTEXITCODE -ne 0) {
        throw 'pycdlib ISO generation failed.'
    }
}

Copy-Item -LiteralPath $versionedIsoPath -Destination $isoPath -Force

Write-Host 'ServoERP installer ISO created:'
Get-Item -LiteralPath $versionedIsoPath, $isoPath | Select-Object FullName, Length, LastWriteTime
