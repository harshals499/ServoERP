param(
    [switch]$SkipPrerequisiteDownload,
    [switch]$ForceCloseRunningApp,
    [switch]$MsiOnly
)

$ErrorActionPreference = 'Stop'

$enterpriseRoot = $PSScriptRoot
$installerRoot = Resolve-Path (Join-Path $enterpriseRoot '..')
$sourceRoot = Resolve-Path (Join-Path $installerRoot '..')
$repoRoot = Resolve-Path (Join-Path $sourceRoot '..')
$assemblyInfo = Join-Path $sourceRoot 'Properties\AssemblyInfo.cs'
$solution = Join-Path $sourceRoot 'HVAC_Pro_Desktop.sln'
$releaseDir = Join-Path $sourceRoot 'bin\Release'
$prereqDir = Join-Path $installerRoot 'Prerequisites'
$outputRoot = Join-Path $repoRoot 'installer_output\enterprise'
$stageDir = Join-Path $outputRoot 'stage\app'
$configStageDir = Join-Path $outputRoot 'stage\config'
$wixDir = Join-Path $enterpriseRoot 'wix'
$appWxs = Join-Path $wixDir 'ServoERP.App.wxs'
$bundleWxs = Join-Path $wixDir 'ServoERP.Bundle.wxs'
$toolsDir = Join-Path $enterpriseRoot 'tools'
$sqlPrereqHelperSource = Join-Path $toolsDir 'SqlExpressPrereqInstaller.cs'
$licenseRtf = Join-Path $installerRoot 'ServoERP-Terms.rtf'

function Assert-File([string]$Path, [string]$Purpose) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Purpose not found: $Path"
    }
}

$runningApps = @(Get-Process -Name 'HVAC_Pro_Desktop','ServoERP' -ErrorAction SilentlyContinue)
if ($runningApps.Count -gt 0) {
    if (-not $ForceCloseRunningApp) {
        $names = ($runningApps | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
        throw "ServoERP is running and will lock the Release output: $names. Close it or rerun with -ForceCloseRunningApp."
    }

    Write-Host "Closing running ServoERP process before packaging..."
    $runningApps | Stop-Process -Force
    Start-Sleep -Seconds 2
}

$assemblyText = Get-Content -LiteralPath $assemblyInfo -Raw
$match = [regex]::Match($assemblyText, 'AssemblyVersion\("(?<version>[^"]+)"\)')
if (-not $match.Success) {
    throw "Could not read AssemblyVersion from $assemblyInfo"
}
$version = $match.Groups['version'].Value

Write-Host "ServoERP enterprise installer version: $version"

Push-Location $repoRoot
try {
    if (-not (Test-Path '.config\dotnet-tools.json')) {
        dotnet new tool-manifest | Out-Null
    }

    $toolList = dotnet tool list
    if ($toolList -notmatch 'wix') {
        dotnet tool install wix --version 5.0.2
    }

    foreach ($extension in @(
        'WixToolset.UI.wixext/5.0.2',
        'WixToolset.Util.wixext/5.0.2',
        'WixToolset.BootstrapperApplications.wixext/5.0.2'
    )) {
        try { dotnet wix extension add $extension | Out-Null } catch { }
    }
}
finally {
    Pop-Location
}

if (-not $SkipPrerequisiteDownload) {
    & (Join-Path $installerRoot 'Download-Prerequisites.ps1')
}

Assert-File (Join-Path $prereqDir 'NDP472-KB4054530-x86-x64-AllOS-ENU.exe') '.NET Framework prerequisite'
Assert-File (Join-Path $prereqDir 'SQLEXPR_x64_ENU.exe') 'SQL Server Express prerequisite'
Assert-File (Join-Path $prereqDir 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe') 'WebView2 prerequisite'
Assert-File $licenseRtf 'ServoERP installer terms'

Write-Host "Building ServoERP Release..."
dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Application build failed."
}

if (Test-Path -LiteralPath $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
New-Item -ItemType Directory -Force -Path $configStageDir | Out-Null

Write-Host "Staging application payload..."
Copy-Item -Path (Join-Path $releaseDir '*') -Destination $stageDir -Recurse -Force
Remove-Item -LiteralPath (Join-Path $stageDir 'HVACPro.config') -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath (Join-Path $sourceRoot 'HVACPro.config') -Destination (Join-Path $configStageDir 'HVACPro.config') -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'app.ico') -Destination (Join-Path $stageDir 'app.ico') -Force

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$msiPath = Join-Path $outputRoot "ServoERP.App.$version.msi"
$bundlePath = Join-Path $outputRoot "ServoERP.Setup.$version.exe"
$defaultConfigPath = Join-Path $configStageDir 'HVACPro.config'
$sqlPrereqHelperPath = Join-Path $outputRoot 'SqlExpressPrereqInstaller.exe'

Write-Host "Compiling SQL Server Express prerequisite helper: $sqlPrereqHelperPath"
Add-Type `
    -Path $sqlPrereqHelperSource `
    -OutputAssembly $sqlPrereqHelperPath `
    -OutputType ConsoleApplication

Write-Host "Compiling MSI: $msiPath"
dotnet wix build $appWxs `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Util.wixext `
    -d AppVersion=$version `
    -d AppSource=$stageDir `
    -d DefaultConfig=$defaultConfigPath `
    -d LicenseRtf=$licenseRtf `
    -out $msiPath
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed."
}

if (-not $MsiOnly) {
    Write-Host "Compiling Burn bootstrapper: $bundlePath"
    dotnet wix build $bundleWxs `
        -ext WixToolset.BootstrapperApplications.wixext `
        -ext WixToolset.Util.wixext `
        -d AppVersion=$version `
        -d AppSource=$stageDir `
        -d PrereqDir=$prereqDir `
        -d SqlPrereqHelper=$sqlPrereqHelperPath `
        -d MsiPath=$msiPath `
        -d LicenseRtf=$licenseRtf `
        -out $bundlePath
    if ($LASTEXITCODE -ne 0) {
        throw "Burn bundle build failed."
    }
}

Write-Host "Enterprise installer output:"
Get-ChildItem -LiteralPath $outputRoot -File |
    Where-Object { $_.Extension -in '.msi', '.exe' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object FullName, Length, LastWriteTime
