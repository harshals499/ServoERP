param(
    [string]$VmName = 'ServoERP-QA-Win11',
    [string]$Username = 'servoqa',
    [string]$Password = 'ServoQA!2026',
    [string]$InstallerPath = 'C:\HVAC_PRO_MSE\installer_output\enterprise\ServoERP.Setup.1.0.20.0.exe',
    [string]$HostLogDir = 'C:\HVAC_PRO_MSE\installer_output\enterprise\vm-smoke',
    [int]$ReadyTimeoutMinutes = 20,
    [switch]$Headless
)

$ErrorActionPreference = 'Stop'

$VBoxManage = @(
    'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe',
    'C:\Program Files\VirtualBox\VBoxManage.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $VBoxManage) {
    throw 'VBoxManage.exe was not found.'
}

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "Installer not found: $InstallerPath"
}

function Invoke-VBox {
    & $VBoxManage @args
    if ($LASTEXITCODE -ne 0) {
        throw "VBoxManage failed: $($args -join ' ')"
    }
}

function Invoke-GuestPowerShell {
    param(
        [string]$Script,
        [int]$TimeoutMs = 600000
    )

    $encodedScript = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Script))

    & $VBoxManage guestcontrol $VmName run `
        --exe 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
        --username $Username `
        --password $Password `
        --wait-stdout `
        --wait-stderr `
        --timeout $TimeoutMs `
        -- `
        -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encodedScript

    if ($LASTEXITCODE -ne 0) {
        throw "Guest PowerShell failed with exit code $LASTEXITCODE."
    }
}

$registered = & $VBoxManage list vms
if (-not ($registered | Where-Object { $_ -match [regex]::Escape("`"$VmName`"") })) {
    throw "VirtualBox VM '$VmName' is not registered."
}

$running = & $VBoxManage list runningvms
if (-not ($running | Where-Object { $_ -match [regex]::Escape("`"$VmName`"") })) {
    $type = if ($Headless) { 'headless' } else { 'gui' }
    Invoke-VBox startvm $VmName --type $type
}

$deadline = (Get-Date).AddMinutes($ReadyTimeoutMinutes)
do {
    try {
        & $VBoxManage guestcontrol $VmName run --exe 'C:\Windows\System32\cmd.exe' --username $Username --password $Password --wait-stdout --timeout 30000 -- /c ver | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
    }
    catch { }
    Start-Sleep -Seconds 15
} while ((Get-Date) -lt $deadline)

if ((Get-Date) -ge $deadline) {
    throw "VM '$VmName' did not become ready for Guest Control within $ReadyTimeoutMinutes minutes."
}

New-Item -ItemType Directory -Force -Path $HostLogDir | Out-Null

Write-Host 'Preparing guest test workspace...'
Invoke-GuestPowerShell -Script 'New-Item -ItemType Directory -Force -Path C:\ServoERP-QA,C:\ProgramData\ServoERP\Logs | Out-Null'

$installerName = Split-Path $InstallerPath -Leaf
$guestInstaller = "C:\ServoERP-QA\$installerName"
$sharedInstaller = "\\VBOXSVR\ServoERPInstaller\$installerName"
Invoke-GuestPowerShell -Script "Copy-Item -LiteralPath '$sharedInstaller' -Destination '$guestInstaller' -Force"
$bundleLog = 'C:\ProgramData\ServoERP\Logs\ServoERP-Bundle-Smoke.log'
$resultPath = 'C:\ServoERP-QA\smoke-result.json'

$smokeScript = @"
`$ErrorActionPreference = 'Stop'
`$result = [ordered]@{
    StartedAt = (Get-Date).ToString('o')
    Installer = '$guestInstaller'
}

`$install = Start-Process -FilePath '$guestInstaller' -ArgumentList '/quiet /norestart /log $bundleLog' -Wait -PassThru
`$result.InstallExitCode = `$install.ExitCode
if (`$install.ExitCode -notin 0,3010) { throw "Installer failed with exit code `$(`$install.ExitCode)" }

`$result.InstallFolderExists = Test-Path 'C:\HVAC_PRO_MSE\HVAC_Pro_Desktop.exe'
`$result.DesktopShortcutExists = Test-Path "`$env:PUBLIC\Desktop\ServoERP.lnk"
`$result.RegistryInstallDir = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\ServoERP' -ErrorAction SilentlyContinue).InstallDir
`$services = Get-Service -ErrorAction SilentlyContinue
`$result.SqlServiceExists = [bool](`$services | Where-Object { `$_.Name -eq 'MSSQLSERVER' -or `$_.Name -like 'MSSQL*SQLEXPRESS' })
`$result.SqlBrowserExists = [bool](`$services | Where-Object { `$_.Name -eq 'SQLBrowser' })
`$result.WebView2RegistryExists = Test-Path 'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
`$app = Start-Process -FilePath 'C:\HVAC_PRO_MSE\HVAC_Pro_Desktop.exe' -PassThru
Start-Sleep -Seconds 20
`$result.AppStillRunningAfter20s = -not `$app.HasExited
if (-not `$app.HasExited) { Stop-Process -Id `$app.Id -Force }

`$repair = Start-Process -FilePath '$guestInstaller' -ArgumentList '/repair /quiet /norestart /log C:\ProgramData\ServoERP\Logs\ServoERP-Bundle-Repair.log' -Wait -PassThru
`$result.RepairExitCode = `$repair.ExitCode

`$result.CompletedAt = (Get-Date).ToString('o')
`$result | ConvertTo-Json -Depth 5 | Set-Content -Path '$resultPath' -Encoding UTF8
"@

Write-Host 'Running install, dependency, runtime, and repair smoke test inside VM...'
Invoke-GuestPowerShell -Script $smokeScript -TimeoutMs 7200000

Write-Host 'Collecting logs from guest...'
Invoke-VBox guestcontrol $VmName copyfrom --username $Username --password $Password --recursive 'C:\ProgramData\ServoERP\Logs' $HostLogDir
Invoke-VBox guestcontrol $VmName copyfrom --username $Username --password $Password $resultPath $HostLogDir

Get-Content -LiteralPath (Join-Path $HostLogDir 'smoke-result.json') -Raw
