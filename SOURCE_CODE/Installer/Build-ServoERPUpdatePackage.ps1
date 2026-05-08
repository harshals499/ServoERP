param(
    [switch]$NoBuild,
    [switch]$ForceCloseRunningApp
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$sourceRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$assemblyInfo = Join-Path $sourceRoot 'Properties\AssemblyInfo.cs'
$solution = Join-Path $sourceRoot 'HVAC_Pro_Desktop.sln'
$releaseDir = Join-Path $sourceRoot 'bin\Release'
$outputDir = Join-Path $repoRoot 'update_output'

$runningApps = @(Get-Process -Name 'HVAC_Pro_Desktop','ServoERP' -ErrorAction SilentlyContinue)
if ($runningApps.Count -gt 0) {
    if (-not $ForceCloseRunningApp) {
        $names = ($runningApps | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
        throw "ServoERP is running and will lock Release output: $names. Close the app, or rerun with -ForceCloseRunningApp."
    }

    $runningApps | Stop-Process -Force
    Start-Sleep -Seconds 2
}

$assemblyText = Get-Content -LiteralPath $assemblyInfo -Raw
$match = [regex]::Match($assemblyText, 'AssemblyVersion\("(?<version>[^"]+)"\)')
if (-not $match.Success) {
    throw "Could not read AssemblyVersion from $assemblyInfo"
}

$version = $match.Groups['version'].Value
if (-not $NoBuild) {
    dotnet build $solution -c Release
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$stage = Join-Path $outputDir ("stage_" + $version)
$zip = Join-Path $outputDir ("ServoERP_Update_{0}.zip" -f $version)

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

New-Item -ItemType Directory -Force -Path $stage | Out-Null

$filePatterns = @(
    'HVAC_Pro_Desktop.exe',
    'HVAC_Pro_Desktop.exe.config',
    '*.dll',
    '*.xml',
    'app.ico'
)

foreach ($pattern in $filePatterns) {
    Get-ChildItem -LiteralPath $releaseDir -Filter $pattern -File -ErrorAction SilentlyContinue |
        Copy-Item -Destination $stage -Force
}

foreach ($folder in @('Resources', 'runtimes')) {
    $source = Join-Path $releaseDir $folder
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $stage $folder) -Recurse -Force
    }
}

$tenantTool = Join-Path $repoRoot 'TOOLS\Provision-TenantDatabase.ps1'
if (Test-Path -LiteralPath $tenantTool) {
    $toolStage = Join-Path $stage 'TOOLS'
    New-Item -ItemType Directory -Force -Path $toolStage | Out-Null
    Copy-Item -LiteralPath $tenantTool -Destination $toolStage -Force
}

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Remove-Item -LiteralPath $stage -Recurse -Force

Write-Host "Update package created:"
Get-Item -LiteralPath $zip | Select-Object FullName, Length, LastWriteTime
