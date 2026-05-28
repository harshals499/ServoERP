$ErrorActionPreference = 'Stop'

$appPath = 'C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
$appDir = Split-Path -Parent $appPath
$licensePath = 'C:\HVAC_PRO_MSE\issued_licenses\Madhusuman_Enterprises_Business_AMC_Enterprise_2026.servoerp-license'

Get-Process HVAC_Pro_Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

Set-Location $appDir
[Reflection.Assembly]::LoadFrom($appPath) | Out-Null

$service = New-Object HVAC_Pro_Desktop.Services.Licensing.LicenseService
$result = $service.ActivateOffline($licensePath)
$snapshot = $result.Snapshot

[pscustomobject]@{
    Success = $result.Success
    Message = $result.Message
    Company = $snapshot.CompanyName
    Plan = $snapshot.PlanType
    Status = $snapshot.Status
    ExpiryUtc = $snapshot.ExpiryDateUtc
    CacheCommon = Test-Path 'C:\ProgramData\ServoERP\license.cache'
    CacheUser = Test-Path (Join-Path $env:LOCALAPPDATA 'ServoERP\license.cache')
} | Format-List
