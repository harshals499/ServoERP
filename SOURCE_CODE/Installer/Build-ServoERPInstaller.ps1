param(
    [switch]$SkipPrerequisiteDownload,
    [switch]$ForceCloseRunningApp,
    [switch]$IncludeLegacyInstaller
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$sourceRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$scriptsRoot = Join-Path $repoRoot 'scripts'
$assemblyInfo = Join-Path $sourceRoot 'Properties\AssemblyInfo.cs'
$versionInclude = Join-Path $PSScriptRoot 'ServoERP.version.iss'
$solution = Join-Path $sourceRoot 'HVAC_Pro_Desktop.sln'
$outputDir = Join-Path $repoRoot 'installer_output'
$iss = Join-Path $PSScriptRoot 'ServoERPSetup.iss'
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishDir = Join-Path $artifactsRoot 'publish'
$velopackDir = Join-Path $artifactsRoot 'velopack'

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
$semVersion = if ($version.Split('.').Count -ge 3) {
    ($version.Split('.')[0..2] -join '.')
}
else {
    $version
}

Set-Content -LiteralPath $versionInclude -Value "#define AppVersion `"$version`"" -Encoding ASCII
Write-Host "ServoERP version: $version"

if (-not $SkipPrerequisiteDownload -and $IncludeLegacyInstaller) {
    & (Join-Path $PSScriptRoot 'Download-Prerequisites.ps1')
}

Write-Host "Building ServoERP Release..."
dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $velopackDir) {
    Remove-Item -LiteralPath $velopackDir -Recurse -Force
}

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
    throw 'Velopack CLI (vpk) was not found. Install Velopack CLI before building the standard ServoERP installer.'
}

& (Join-Path $scriptsRoot 'Prepare-VelopackPublish.ps1') `
    -BuildOutput (Join-Path $sourceRoot 'bin\Release') `
    -PublishDir $publishDir | Out-Null

Write-Host "Packing Velopack setup..."
& $vpk.Source pack `
    --packId "ServoERP.Desktop" `
    --packTitle "ServoERP" `
    --packAuthors "Harshal Sonawane" `
    --packVersion $semVersion `
    --packDir $publishDir `
    --outputDir $velopackDir `
    --mainExe "HVAC_Pro_Desktop.exe" `
    --icon (Join-Path $sourceRoot 'app.ico') `
    --runtime win-x64 `
    --exclude "(?i)(^|[\\/])(HVACPro\.config|.*\.servoerp-license|.*\.sqlite|.*\.mdf|.*\.ldf|logs?|database|updates?)([\\/]|$)"

if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

$velopackSetup = Join-Path $velopackDir 'ServoERP.Desktop-win-Setup.exe'
$standardInstaller = Join-Path $outputDir ("ServoERP_Setup_{0}.exe" -f $version)
if (-not (Test-Path -LiteralPath $velopackSetup)) {
    throw "Velopack setup was not created: $velopackSetup"
}

Copy-Item -LiteralPath $velopackSetup -Destination $standardInstaller -Force

if ($IncludeLegacyInstaller) {
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
    Write-Host "Compiling legacy fallback installer with $iscc..."
    & $iscc $iss

    $legacyInstaller = Join-Path $outputDir ("ServoERP_Setup_{0}.exe" -f $version)
    $renamedLegacyInstaller = Join-Path $outputDir ("ServoERP_Legacy_Setup_{0}.exe" -f $version)
    if (Test-Path -LiteralPath $legacyInstaller) {
        Move-Item -LiteralPath $legacyInstaller -Destination $renamedLegacyInstaller -Force
    }

    Copy-Item -LiteralPath $velopackSetup -Destination $standardInstaller -Force
}

Write-Host "Installer output:"
Get-ChildItem -LiteralPath $outputDir -Filter '*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 8 FullName, Length, LastWriteTime
