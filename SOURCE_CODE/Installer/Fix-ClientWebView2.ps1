param(
    [string]$AppPath = "C:\Program Files\HVAC_PRO_MSE\APP",
    [string]$BuildPath = "C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Release",
    [string]$WebViewInstallerPath = "C:\HVAC_PRO_MSE\SOURCE_CODE\Installer\Prerequisites\MicrosoftEdgeWebview2Setup.exe",
    [switch]$LaunchApp
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-FileVersionSafe {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return "<missing>"
    }

    try {
        return (Get-Item $Path).VersionInfo.FileVersion
    }
    catch {
        return "<unknown>"
    }
}

function Get-WebView2RuntimeVersion {
    $keys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    )

    foreach ($key in $keys) {
        if (Test-Path $key) {
            $value = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).pv
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                return $value
            }
        }
    }

    return $null
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Copy-WebView2Payload {
    param(
        [string]$SourceRoot,
        [string]$TargetRoot
    )

    $copyMap = @(
        @{
            Source = Join-Path $SourceRoot "Microsoft.Web.WebView2.Core.dll"
            Target = Join-Path $TargetRoot "Microsoft.Web.WebView2.Core.dll"
        },
        @{
            Source = Join-Path $SourceRoot "Microsoft.Web.WebView2.WinForms.dll"
            Target = Join-Path $TargetRoot "Microsoft.Web.WebView2.WinForms.dll"
        },
        @{
            Source = Join-Path $SourceRoot "runtimes\win-x64\native\WebView2Loader.dll"
            Target = Join-Path $TargetRoot "runtimes\win-x64\native\WebView2Loader.dll"
        }
    )

    foreach ($item in $copyMap) {
        if (-not (Test-Path $item.Source)) {
            throw "Required source file not found: $($item.Source)"
        }

        Ensure-Directory -Path (Split-Path $item.Target -Parent)
        Copy-Item -Path $item.Source -Destination $item.Target -Force
        Write-Host ("Copied: " + $item.Target) -ForegroundColor Green
    }
}

try {
    Write-Step "Checking packaged HVAC PRO WebView2 files"

    $expectedCorePath = Join-Path $BuildPath "Microsoft.Web.WebView2.Core.dll"
    $expectedWinFormsPath = Join-Path $BuildPath "Microsoft.Web.WebView2.WinForms.dll"
    $expectedLoaderPath = Join-Path $BuildPath "runtimes\win-x64\native\WebView2Loader.dll"

    $expectedVersion = Get-FileVersionSafe -Path $expectedCorePath
    Write-Host ("Expected app WebView2 version: " + $expectedVersion) -ForegroundColor Yellow

    Write-Step "Checking installed Microsoft WebView2 Runtime"
    $runtimeVersion = Get-WebView2RuntimeVersion
    if ($runtimeVersion) {
        Write-Host ("Installed runtime version: " + $runtimeVersion) -ForegroundColor Green
    }
    else {
        Write-Host "WebView2 Runtime not found in registry." -ForegroundColor Yellow
        if (Test-Path $WebViewInstallerPath) {
            Write-Host "Running WebView2 runtime installer silently..." -ForegroundColor Yellow
            Start-Process -FilePath $WebViewInstallerPath -ArgumentList "/silent /install" -Wait
            $runtimeVersion = Get-WebView2RuntimeVersion
            if ($runtimeVersion) {
                Write-Host ("Runtime version after install: " + $runtimeVersion) -ForegroundColor Yellow
            }
            else {
                Write-Host "Runtime version after install: <still not detected>" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host ("Installer not found: " + $WebViewInstallerPath) -ForegroundColor Red
        }
    }

    Write-Step "Checking app-local WebView2 files on client PC"
    $targetCorePath = Join-Path $AppPath "Microsoft.Web.WebView2.Core.dll"
    $targetWinFormsPath = Join-Path $AppPath "Microsoft.Web.WebView2.WinForms.dll"
    $targetLoaderPath = Join-Path $AppPath "runtimes\win-x64\native\WebView2Loader.dll"

    Write-Host ("Current app Core DLL version     : " + (Get-FileVersionSafe -Path $targetCorePath))
    Write-Host ("Current app WinForms DLL version : " + (Get-FileVersionSafe -Path $targetWinFormsPath))
    Write-Host ("Current app Loader DLL version   : " + (Get-FileVersionSafe -Path $targetLoaderPath))

    Write-Step "Stopping HVAC PRO if running"
    $running = Get-Process -Name "HVAC_Pro_Desktop" -ErrorAction SilentlyContinue
    if ($running) {
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
        Write-Host "Stopped HVAC PRO." -ForegroundColor Yellow
    }
    else {
        Write-Host "HVAC PRO is not running." -ForegroundColor DarkGray
    }

    Write-Step "Repairing app-local WebView2 files"
    Copy-WebView2Payload -SourceRoot $BuildPath -TargetRoot $AppPath

    Write-Step "Final version check"
    Write-Host ("Repaired app Core DLL version     : " + (Get-FileVersionSafe -Path $targetCorePath)) -ForegroundColor Green
    Write-Host ("Repaired app WinForms DLL version : " + (Get-FileVersionSafe -Path $targetWinFormsPath)) -ForegroundColor Green
    Write-Host ("Repaired app Loader DLL version   : " + (Get-FileVersionSafe -Path $targetLoaderPath)) -ForegroundColor Green

    Write-Step "Done"
    Write-Host "HVAC PRO WebView2 repair completed." -ForegroundColor Green

    if ($LaunchApp) {
        $appExe = Join-Path $AppPath "HVAC_Pro_Desktop.exe"
        if (Test-Path $appExe) {
            Write-Step "Launching HVAC PRO"
            Start-Process -FilePath $appExe
        }
    }
}
catch {
    Write-Host ""
    Write-Host ("Repair failed: " + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
