[CmdletBinding()]
param(
    [ValidateSet('Developer', 'Client')]
    [string]$Mode = 'Developer',
    [string]$InstallRoot = 'C:\HVAC_PRO_MSE',
    [string]$RepositoryUrl = 'https://github.com/harshals499/ServoERP.git',
    [string]$Branch = 'main',
    [switch]$InstallPrerequisites,
    [switch]$InstallSqlExpress,
    [switch]$ConfigureDatabase,
    [string]$SqlServer,
    [string]$DatabaseName = 'HVAC_PRO',
    [switch]$LaunchApp
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Get-CommandPath {
    param([string]$Name)
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    return $null
}

function Confirm-Action {
    param([string]$Question)
    $answer = Read-Host "$Question [Y/N]"
    return $answer -match '^(y|yes)$'
}

function Install-WingetPackage {
    param([string]$Id, [string]$Label)
    if (-not (Get-CommandPath 'winget.exe')) {
        throw "Windows Package Manager (winget) is required to install $Label automatically. Install App Installer from Microsoft Store, then run this setup again."
    }

    Write-Step "Installing $Label"
    & winget install --id $Id --exact --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw "Installation failed for $Label (winget exit code $LASTEXITCODE)." }
}

function Get-MsBuildPath {
    $vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vsWhere) {
        $path = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($path -and (Test-Path -LiteralPath $path)) { return $path }
    }

    return Get-CommandPath 'msbuild.exe'
}

function Get-NuGetPath {
    $existing = Get-CommandPath 'nuget.exe'
    if ($existing) { return $existing }

    $toolDirectory = Join-Path $env:LOCALAPPDATA 'ServoERP\SetupTools'
    $nugetPath = Join-Path $toolDirectory 'nuget.exe'
    if (-not (Test-Path -LiteralPath $nugetPath)) {
        New-Item -ItemType Directory -Force -Path $toolDirectory | Out-Null
        Write-Step 'Downloading NuGet command line tool from nuget.org'
        Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nugetPath -UseBasicParsing
    }

    return $nugetPath
}

function Set-LocalDatabaseConfiguration {
    param([string]$ConfigPath, [string]$Server, [string]$Database)
    if ([string]::IsNullOrWhiteSpace($Server)) {
        $Server = Read-Host 'SQL Server instance (for example .\SQLEXPRESS or SERVERPC\SQLEXPRESS)'
    }
    if ([string]::IsNullOrWhiteSpace($Server)) {
        throw 'A SQL Server instance is required when ConfigureDatabase is selected.'
    }

    [xml]$config = Get-Content -LiteralPath $ConfigPath
    $config.HVACProConfig.Database.Server = $Server.Trim()
    $config.HVACProConfig.Database.DatabaseName = $Database.Trim()
    $config.HVACProConfig.Database.UseWindowsAuth = 'true'
    $config.HVACProConfig.Database.Username = ''
    $config.HVACProConfig.Database.Password = ''
    $config.Save($ConfigPath)
    Write-Host "Configured integrated-security database access to $Server / $Database." -ForegroundColor Green
}

function Install-Client {
    $installerUrl = 'https://downloads.servoerp.in/ServoERP_Setup_1.1.400.0.exe'
    $installerPath = Join-Path $env:TEMP 'ServoERP_Setup_1.1.400.0.exe'
    Write-Step 'Downloading the public ServoERP installer'
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    Write-Step 'Starting the ServoERP installer'
    Start-Process -FilePath $installerPath -Wait
    Write-Host 'Client installation finished. Configure this PC to reach the authorised ServoERP SQL Server before using live business data.' -ForegroundColor Yellow
}

Write-Host 'ServoERP New-PC Setup' -ForegroundColor Green
Write-Host 'This package never copies database credentials, licenses, or business data.' -ForegroundColor Yellow

if ($Mode -eq 'Client') {
    Install-Client
    exit 0
}

Write-Step 'Checking developer prerequisites'
$git = Get-CommandPath 'git.exe'
if (-not $git -and ($InstallPrerequisites -or (Confirm-Action 'Git is missing. Install Git for Windows now?'))) {
    Install-WingetPackage -Id 'Git.Git' -Label 'Git for Windows'
    $git = Get-CommandPath 'git.exe'
}
if (-not $git) { throw 'Git is required. Install Git for Windows, reopen PowerShell, and rerun this setup.' }

$msbuild = Get-MsBuildPath
if (-not $msbuild -and ($InstallPrerequisites -or (Confirm-Action 'Visual Studio Build Tools are missing. Install Visual Studio Community with .NET desktop development now?'))) {
    Install-WingetPackage -Id 'Microsoft.VisualStudio.2022.Community' -Label 'Visual Studio 2022 Community'
    Write-Host 'In the Visual Studio installer, select the .NET desktop development workload, then rerun this setup.' -ForegroundColor Yellow
    exit 0
}
if (-not $msbuild) { throw 'MSBuild is required. Install Visual Studio 2022 with the .NET desktop development workload, then rerun this setup.' }

Write-Step 'Cloning or updating ServoERP source'
if (-not (Test-Path -LiteralPath $InstallRoot)) {
    & $git clone --branch $Branch --single-branch $RepositoryUrl $InstallRoot
    if ($LASTEXITCODE -ne 0) { throw "Git clone failed with exit code $LASTEXITCODE." }
}
elseif (Test-Path -LiteralPath (Join-Path $InstallRoot '.git')) {
    & $git -C $InstallRoot fetch origin $Branch
    if ($LASTEXITCODE -ne 0) { throw "Git fetch failed with exit code $LASTEXITCODE." }
    & $git -C $InstallRoot checkout $Branch
    if ($LASTEXITCODE -ne 0) { throw "Git checkout failed with exit code $LASTEXITCODE." }
    & $git -C $InstallRoot pull --ff-only origin $Branch
    if ($LASTEXITCODE -ne 0) { throw "Git pull failed. Commit or stash local work, then rerun this setup." }
}
else {
    throw "$InstallRoot already exists but is not a ServoERP Git checkout. Choose a different InstallRoot."
}

$solution = Join-Path $InstallRoot 'SOURCE_CODE\HVAC_Pro_Desktop.sln'
$configPath = Join-Path $InstallRoot 'SOURCE_CODE\HVACPro.config'
if (-not (Test-Path -LiteralPath $solution)) { throw "ServoERP solution was not found at $solution." }

Write-Step 'Restoring NuGet dependencies'
$nuget = Get-NuGetPath
& $nuget restore $solution -NonInteractive
if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed with exit code $LASTEXITCODE." }

Write-Step 'Building ServoERP Release'
& $msbuild $solution /m /t:Rebuild /p:Configuration=Release /p:Platform='Any CPU'
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }

$exe = Join-Path $InstallRoot 'SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Build finished but the expected executable was not created: $exe" }

if ($InstallSqlExpress) {
    Install-WingetPackage -Id 'Microsoft.SQLServer.2022.Express' -Label 'SQL Server Express 2022'
}
if ($ConfigureDatabase) {
    Set-LocalDatabaseConfiguration -ConfigPath $configPath -Server $SqlServer -Database $DatabaseName
}

Write-Host "`nDeveloper setup is complete." -ForegroundColor Green
Write-Host "Source folder: $InstallRoot"
Write-Host "Application:   $exe"
Write-Host 'Database note: restore an authorised HVAC_PRO backup or connect this PC to your existing SQL Server before using live data.' -ForegroundColor Yellow
Write-Host 'ChatGPT note: sign in to the ChatGPT desktop app with your Harshal account, then open this source folder as a local Codex project.' -ForegroundColor Yellow

if ($LaunchApp -or (Confirm-Action 'Launch the built ServoERP application now?')) {
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
}
