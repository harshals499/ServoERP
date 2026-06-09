param(
    [switch]$SkipPrerequisiteDownload,
    [switch]$ForceCloseRunningApp
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$sourceRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$assemblyInfo = Join-Path $sourceRoot 'Properties\AssemblyInfo.cs'
$versionInclude = Join-Path $PSScriptRoot 'ServoERP.version.iss'
$solution = Join-Path $sourceRoot 'HVAC_Pro_Desktop.sln'
$outputDir = Join-Path $repoRoot 'installer_output'
$iss = Join-Path $PSScriptRoot 'ServoERPSetup.iss'

$runningApps = @(Get-Process -Name 'HVAC_Pro_Desktop','ServoERP' -ErrorAction SilentlyContinue)
if ($runningApps.Count -gt 0) {
    if (-not $ForceCloseRunningApp) {
        $names = ($runningApps | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
        throw "ServoERP is running and will lock the Release output: $names. Close the app, or rerun with -ForceCloseRunningApp."
    }

    Write-Host "Closing running ServoERP process before build..."
    $runningApps | Stop-Process -Force
    Start-Sleep -Seconds 2
}

$assemblyText = Get-Content -LiteralPath $assemblyInfo -Raw
$match = [regex]::Match($assemblyText, 'AssemblyVersion\("(?<version>[^"]+)"\)')
if (-not $match.Success) {
    throw "Could not read AssemblyVersion from $assemblyInfo"
}

$version = $match.Groups['version'].Value
Set-Content -LiteralPath $versionInclude -Value "#define AppVersion `"$version`"" -Encoding ASCII
Write-Host "ServoERP version: $version"

if (-not $SkipPrerequisiteDownload) {
    & (Join-Path $PSScriptRoot 'Download-Prerequisites.ps1')
}

Write-Host "Building ServoERP Release..."
dotnet build $solution -c Release

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    'C:\Program Files (x86)\Inno Setup 7\ISCC.exe',
    'C:\Program Files\Inno Setup 7\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

if (-not $isccCandidates) {
    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6, then rerun this script.'
}

$iscc = @($isccCandidates)[0]
Write-Host "Compiling installer with $iscc..."
& $iscc $iss

Write-Host "Installer output:"
Get-ChildItem -LiteralPath $outputDir -Filter '*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 5 FullName, Length, LastWriteTime
